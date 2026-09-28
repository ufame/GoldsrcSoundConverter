using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Audio;

public interface IConversionService
{
  string? FfmpegPath { get; }

  string? FfprobePath { get; }

  void SetCustomFfmpegPath(string? customPath);

  bool TryResolveFfmpeg(out string ffmpeg, out string ffprobe);

  Task<(string Ffmpeg, string Ffprobe)> EnsureFfmpegAsync(
    IProgress<BootstrapProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken = default);

  Task<ProbeResult?> TryProbeAsync(Guid id, string path, CancellationToken cancellationToken = default);

  Task<WaveformData?> TryExtractWaveformAsync(string path, CancellationToken cancellationToken = default);

  Task<string> PreparePlayableAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default);

  Task<ConversionOutcome> ConvertSingleAsync(
    ConversionWorkItem item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default);

  Task<ConversionBatchResult> ConvertAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    ConversionOptions options,
    IProgress<ConversionProgress>? progress,
    IProgress<ProbeResult>? probeProgress,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default);
}
