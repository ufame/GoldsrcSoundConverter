using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeAudioConverter : IAudioConverter
{
  private readonly object _gate = new();
  private readonly List<ConversionJob> _calls = new();

  public IReadOnlyList<ConversionJob> Calls
  {
    get
    {
      lock (_gate)
      {
        return _calls.ToArray();
      }
    }
  }

  public Func<ConversionJob, ConversionOutcome>? OutcomeFactory { get; set; }

  public Func<ConversionJob, ConversionOptions, CancellationToken, Task<ConversionOutcome>>? Handler { get; set; }

  public TimeSpan Delay { get; set; } = TimeSpan.Zero;

  public async Task<ConversionOutcome> ConvertAsync(
    ConversionJob job,
    ConversionOptions options,
    AudioInfo? knownInfo,
    IProgress<ConversionProgress>? progress = null,
    Action<string>? log = null,
    CancellationToken cancellationToken = default)
  {
    lock (_gate)
    {
      _calls.Add(job);
    }

    if (Handler is not null)
    {
      return await Handler(job, options, cancellationToken).ConfigureAwait(false);
    }

    if (Delay > TimeSpan.Zero)
    {
      await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
    }

    cancellationToken.ThrowIfCancellationRequested();

    if (OutcomeFactory is not null)
    {
      return OutcomeFactory(job);
    }

    progress?.Report(new ConversionProgress(job.Id, ConversionStage.Completed, 1, TimeSpan.Zero));
    return new ConversionOutcome(job.Id, true, false, job.OutputPath, null, null);
  }
}
