using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class AudioConverterTests : IDisposable
{
  private const string ProbeJson =
    """{"streams":[{"codec_name":"pcm_s16le","sample_rate":"22050","channels":1,"bit_rate":"352800","duration":"5.0"}],"format":{"format_name":"wav","duration":"5.0","bit_rate":"352800"}}""";

  private const string VolumeStderr = "[Parsed_volumedetect_0 @ 0000] max_volume: -6.0 dB";

  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public async Task ConvertsWavSuccessfully()
  {
    var runner = CreateRunner();
    var job = CreateJob();

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.True(outcome.Success, outcome.Error);
    Assert.False(outcome.Skipped);
    Assert.Equal(job.OutputPath, outcome.OutputPath);
    Assert.NotNull(outcome.OutputInfo);
    Assert.Contains(runner.Calls, call => !call.IsProbe && call.Arguments.Contains("pcm_s16le"));
  }

  [Fact]
  public async Task ReturnsFailedOutcomeOnFfmpegFailure()
  {
    var runner = CreateRunner(convertSuccess: false, convertError: "boom");
    var job = CreateJob();

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.False(outcome.Success);
    Assert.Equal(job.Id, outcome.JobId);
    Assert.Equal("boom", outcome.Error);
  }

  [Fact]
  public async Task SkipsWhenOutputPathIsNull()
  {
    var runner = CreateRunner();
    var job = CreateJob() with { OutputPath = null };

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.True(outcome.Success);
    Assert.True(outcome.Skipped);
    Assert.DoesNotContain(runner.Calls, call => !call.IsProbe);
  }

  [Fact]
  public async Task InvalidProbeOutputFails()
  {
    var runner = new FakeProcessRunner(call => call.IsProbe
      ? new FakeProcessRunner.Response(1, "", "probe failed")
      : new FakeProcessRunner.Response(0));
    var job = CreateJob();

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.False(outcome.Success);
    Assert.NotNull(outcome.Error);
  }

  [Fact]
  public async Task ProbesOnlyResultWhenKnownInfoProvided()
  {
    var runner = CreateRunner();
    var job = CreateJob();
    var knownInfo = new AudioInfo(job.SourcePath, "wav", "pcm_s16le", TimeSpan.FromSeconds(5), 22050, 1, 352800, 10);

    var outcome = await new AudioConverter(runner, "ffmpeg.exe", "ffprobe.exe")
      .ConvertAsync(job, Options(), knownInfo);

    Assert.True(outcome.Success, outcome.Error);
    Assert.Single(runner.Calls, call => call.IsProbe);
  }

  [Fact]
  public async Task CancellationIsPropagated()
  {
    var runner = CreateRunner();
    var job = CreateJob();
    using var cts = new CancellationTokenSource();
    cts.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(
      () => ConvertAsync(runner, job, Options(), cancellationToken: cts.Token));
  }

  [Fact]
  public async Task NormalizationEnabledMeasuresVolumeAndAddsFilter()
  {
    var runner = CreateRunner();
    var job = CreateJob();
    var options = Options() with { NormalizePeak = true };

    var outcome = await ConvertAsync(runner, job, options);

    Assert.True(outcome.Success, outcome.Error);
    Assert.Contains(runner.Calls, call => call.Arguments.Contains("volumedetect"));

    var convert = runner.Calls.Last(call => !call.IsProbe);
    var filterIndex = convert.Arguments.ToList().IndexOf("-af");
    Assert.True(filterIndex >= 0, "Ожидался фильтр -af при нормализации");
    Assert.StartsWith("volume=", convert.Arguments[filterIndex + 1]);
  }

  [Fact]
  public async Task NormalizationDisabledSkipsVolumeMeasure()
  {
    var runner = CreateRunner();
    var job = CreateJob();

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.True(outcome.Success, outcome.Error);
    Assert.DoesNotContain(runner.Calls, call => call.Arguments.Contains("volumedetect"));
  }

  [Fact]
  public async Task Mp3FormatUsesLameEncoder()
  {
    var runner = CreateRunner();
    var job = CreateJob();
    var options = Options() with { Format = OutputAudioFormat.Mp3, Mp3BitrateKbps = 192 };

    var outcome = await ConvertAsync(runner, job, options);

    Assert.True(outcome.Success, outcome.Error);
    var convert = runner.Calls.Last(call => !call.IsProbe);
    Assert.Contains("libmp3lame", convert.Arguments);
    Assert.Contains("192k", convert.Arguments);
  }

  [Fact]
  public async Task TrimAddsSeekAndDuration()
  {
    var runner = CreateRunner();
    var job = CreateJob(trimStart: TimeSpan.FromSeconds(1), trimEnd: TimeSpan.FromSeconds(3));

    var outcome = await ConvertAsync(runner, job, Options());

    Assert.True(outcome.Success, outcome.Error);
    var convert = runner.Calls.Last(call => !call.IsProbe);
    var args = convert.Arguments.ToList();
    Assert.Equal("1", args[args.IndexOf("-ss") + 1]);
    Assert.Equal("2", args[args.IndexOf("-t") + 1]);
  }

  private static ConversionOptions Options()
  {
    return new ConversionOptions
    {
      OutputDirectory = @"C:\out",
      AsciiNames = true,
      LowercaseNames = true,
    };
  }

  private ConversionJob CreateJob(
    TimeSpan? trimStart = null,
    TimeSpan? trimEnd = null)
  {
    var source = Path.Combine(_temp.Path, "input.wav");
    File.WriteAllText(source, "x");

    return new ConversionJob(Guid.NewGuid(), source, null, trimStart, trimEnd)
    {
      OutputPath = Path.Combine(_temp.Path, "out", "input.wav"),
    };
  }

  private static FakeProcessRunner CreateRunner(bool convertSuccess = true, string convertError = "")
  {
    return new FakeProcessRunner(call =>
    {
      if (call.IsProbe)
      {
        return new FakeProcessRunner.Response(0, ProbeJson, "");
      }

      if (call.Arguments.Contains("volumedetect"))
      {
        return new FakeProcessRunner.Response(0, "", VolumeStderr);
      }

      return new FakeProcessRunner.Response(convertSuccess ? 0 : 1, "", convertError);
    });
  }

  private static async Task<ConversionOutcome> ConvertAsync(
    FakeProcessRunner runner,
    ConversionJob job,
    ConversionOptions options,
    CancellationToken cancellationToken = default)
  {
    var converter = new AudioConverter(runner, @"C:\ffmpeg\ffmpeg.exe", @"C:\ffmpeg\ffprobe.exe");
    return await converter.ConvertAsync(job, options, null, cancellationToken: cancellationToken);
  }
}
