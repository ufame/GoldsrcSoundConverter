using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeConversionService : IConversionService
{
  public string? FfmpegPath { get; set; } = @"C:\ffmpeg\ffmpeg.exe";

  public string? FfprobePath { get; set; } = @"C:\ffmpeg\ffprobe.exe";

  public bool FfmpegAvailable { get; set; } = true;

  public List<string?> CustomPaths { get; } = new();

  public List<(IReadOnlyList<ConversionWorkItem> WorkItems, ConversionOptions Options)> ConvertCalls { get; } = new();

  public List<(ConversionWorkItem Item, ConversionOptions Options)> SingleCalls { get; } = new();

  public List<string> PlayableRequests { get; } = new();

  public Func<ConversionWorkItem, ConversionOutcome>? OutcomeFactory { get; set; }

  public Func<ConversionWorkItem, ConversionOutcome>? SingleOutcomeFactory { get; set; }

  public TaskCompletionSource? ConversionGate { get; set; }

  public List<ProbeResult> ProbeResults { get; } = new();

  public Exception? PrepareException { get; set; }

  public WaveformData? Waveform { get; set; }

  public int WaveformRequests { get; private set; }

  public Exception? WaveformException { get; set; }

  public void SetCustomFfmpegPath(string? customPath)
  {
    CustomPaths.Add(customPath);
  }

  public bool TryResolveFfmpeg(out string ffmpeg, out string ffprobe)
  {
    ffmpeg = FfmpegPath ?? string.Empty;
    ffprobe = FfprobePath ?? string.Empty;
    return FfmpegAvailable && FfmpegPath is not null && FfprobePath is not null;
  }

  public Task<(string Ffmpeg, string Ffprobe)> EnsureFfmpegAsync(
    IProgress<BootstrapProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (!FfmpegAvailable)
    {
      throw new InvalidOperationException("FFmpeg недоступен в тесте.");
    }

    return Task.FromResult((FfmpegPath!, FfprobePath!));
  }

  public Task<ProbeResult?> TryProbeAsync(Guid id, string path, CancellationToken cancellationToken = default)
  {
    if (!FfmpegAvailable)
    {
      return Task.FromResult<ProbeResult?>(null);
    }

    return Task.FromResult<ProbeResult?>(new ProbeResult(id, new AudioInfo(
      path,
      "wav",
      "pcm_s16le",
      TimeSpan.FromSeconds(5),
      22050,
      1,
      352800,
      100)));
  }

  public Task<WaveformData?> TryExtractWaveformAsync(string path, CancellationToken cancellationToken = default)
  {
    WaveformRequests++;

    if (WaveformException is not null)
    {
      throw WaveformException;
    }

    return Task.FromResult(Waveform);
  }

  public async Task<string> PreparePlayableAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    PlayableRequests.Add(sourcePath);

    if (PrepareException is not null)
    {
      throw PrepareException;
    }

    await Task.Yield();
    return sourcePath + ".preview.wav";
  }

  public async Task<ConversionOutcome> ConvertSingleAsync(
    ConversionWorkItem item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    SingleCalls.Add((item, options));
    await Task.Yield();

    if (SingleOutcomeFactory is not null)
    {
      return SingleOutcomeFactory(item);
    }

    return new ConversionOutcome(item.Id, true, false, @"C:\temp\preview.wav", null, null);
  }

  public async Task<ConversionBatchResult> ConvertAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    ConversionOptions options,
    IProgress<ConversionProgress>? progress,
    IProgress<ProbeResult>? probeProgress,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    ConvertCalls.Add((workItems, options));

    if (ConversionGate is not null)
    {
      await ConversionGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    foreach (var probe in ProbeResults)
    {
      probeProgress?.Report(probe);
    }

    var outcomes = new List<ConversionOutcome>(workItems.Count);
    foreach (var workItem in workItems)
    {
      cancellationToken.ThrowIfCancellationRequested();

      progress?.Report(new ConversionProgress(workItem.Id, ConversionStage.Converting, 0.5, null));

      var outcome = OutcomeFactory?.Invoke(workItem)
        ?? new ConversionOutcome(workItem.Id, true, false, workItem.SourcePath + ".out.wav", null, null);
      outcomes.Add(outcome);

      var stage = outcome.Success
        ? outcome.Skipped ? ConversionStage.Skipped : ConversionStage.Completed
        : ConversionStage.Failed;
      progress?.Report(new ConversionProgress(workItem.Id, stage, 1, TimeSpan.Zero));
    }

    return new ConversionBatchResult(outcomes, FfmpegPath ?? string.Empty, FfprobePath ?? string.Empty);
  }
}
