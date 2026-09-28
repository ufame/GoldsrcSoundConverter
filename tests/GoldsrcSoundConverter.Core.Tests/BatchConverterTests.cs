using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class BatchConverterTests
{
  private static ConversionOptions Options(int parallelism = 4)
  {
    return new ConversionOptions
    {
      OutputDirectory = @"C:\out",
      Parallelism = parallelism,
    };
  }

  private static ConversionJob Job(int index)
  {
    return new ConversionJob(Guid.NewGuid(), $@"C:\in\{index}.wav", null, null, null)
    {
      OutputPath = $@"C:\out\{index}.wav",
    };
  }

  [Fact]
  public async Task HandlesSingleJob()
  {
    var converter = new FakeAudioConverter();
    var job = Job(0);

    var outcomes = await new BatchConverter(converter).RunAsync(new[] { job }, Options());

    Assert.Single(outcomes);
    Assert.Equal(job.Id, outcomes[0].JobId);
    Assert.True(outcomes[0].Success);
    Assert.Equal(job.Id, Assert.Single(converter.Calls).Id);
  }

  [Fact]
  public async Task ReturnsOutcomesInInputOrder()
  {
    var converter = new FakeAudioConverter { Delay = TimeSpan.FromMilliseconds(5) };
    var jobs = Enumerable.Range(0, 10).Select(Job).ToArray();

    var outcomes = await new BatchConverter(converter).RunAsync(jobs, Options());

    Assert.Equal(jobs.Select(j => j.Id), outcomes.Select(o => o.JobId));
  }

  [Fact]
  public async Task RunsAllJobsWithParallelismOne()
  {
    var converter = new FakeAudioConverter();
    var jobs = Enumerable.Range(0, 10).Select(Job).ToArray();

    var outcomes = await new BatchConverter(converter).RunAsync(jobs, Options(parallelism: 1));

    Assert.Equal(10, outcomes.Count);
    Assert.Equal(10, converter.Calls.Count);
  }

  [Fact]
  public async Task RespectsParallelismLimit()
  {
    var current = 0;
    var maxObserved = 0;
    var gate = new object();
    var converter = new FakeAudioConverter
    {
      Handler = async (job, _, token) =>
      {
        lock (gate)
        {
          current++;
          maxObserved = Math.Max(maxObserved, current);
        }

        await Task.Delay(30, token).ConfigureAwait(false);

        lock (gate)
        {
          current--;
        }

        return new ConversionOutcome(job.Id, true, false, job.OutputPath, null, null);
      },
    };

    var jobs = Enumerable.Range(0, 12).Select(Job).ToArray();

    await new BatchConverter(converter).RunAsync(jobs, Options(parallelism: 3));

    Assert.InRange(maxObserved, 1, 3);
    Assert.Equal(12, converter.Calls.Count);
  }

  [Fact]
  public async Task OneFailureDoesNotStopOtherJobs()
  {
    var converter = new FakeAudioConverter
    {
      OutcomeFactory = job => job.SourcePath.EndsWith("2.wav", StringComparison.Ordinal)
        ? new ConversionOutcome(job.Id, false, false, null, "boom", null)
        : new ConversionOutcome(job.Id, true, false, job.OutputPath, null, null),
    };

    var jobs = Enumerable.Range(0, 5).Select(Job).ToArray();

    var outcomes = await new BatchConverter(converter).RunAsync(jobs, Options());

    Assert.Equal(5, outcomes.Count);
    Assert.Equal(4, outcomes.Count(o => o.Success));
    Assert.Equal("boom", outcomes.Single(o => !o.Success).Error);
  }

  [Fact]
  public async Task AllFailuresAreReported()
  {
    var converter = new FakeAudioConverter
    {
      OutcomeFactory = job => new ConversionOutcome(job.Id, false, false, null, "fail", null),
    };

    var jobs = Enumerable.Range(0, 3).Select(Job).ToArray();

    var outcomes = await new BatchConverter(converter).RunAsync(jobs, Options());

    Assert.Equal(3, outcomes.Count);
    Assert.All(outcomes, outcome => Assert.False(outcome.Success));
  }

  [Fact]
  public async Task ReportsProgressForEveryJob()
  {
    var converter = new FakeAudioConverter();
    var jobs = Enumerable.Range(0, 4).Select(Job).ToArray();
    var reported = new List<ConversionProgress>();
    var progress = new Progress<ConversionProgress>(reported.Add);

    var outcomes = await new BatchConverter(converter)
      .RunAsync(jobs, Options(parallelism: 1), progress: progress);

    Assert.Equal(4, outcomes.Count);
    Assert.Equal(4, reported.Count);
    Assert.Equal(4, reported.Select(r => r.JobId).Distinct().Count());
  }

  [Fact]
  public async Task PropagatesCancellation()
  {
    using var cts = new CancellationTokenSource();
    var converter = new FakeAudioConverter { Delay = TimeSpan.FromMilliseconds(40) };
    var jobs = Enumerable.Range(0, 20).Select(Job).ToArray();

    var task = new BatchConverter(converter).RunAsync(jobs, Options(parallelism: 1), cancellationToken: cts.Token);
    await Task.Delay(30);
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
  }

  [Fact]
  public async Task RejectsInvalidOptions()
  {
    var converter = new FakeAudioConverter();
    var options = new ConversionOptions { Parallelism = 0 };

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
      () => new BatchConverter(converter).RunAsync(Array.Empty<ConversionJob>(), options));
  }
}
