using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IPlaybackCoordinator
{
  double PositionSeconds { get; }

  double TotalSeconds { get; }

  double Volume { get; set; }

  event EventHandler? PositionChanged;

  Task<PlaybackResult> PlayAsync(
    QueueItemViewModel item,
    IProgress<BootstrapProgress>? bootstrapProgress);

  Task<PlaybackResult> PlaySelectionAsync(
    QueueItemViewModel item,
    IProgress<BootstrapProgress>? bootstrapProgress);

  Task<PlaybackResult> PreviewResultAsync(
    QueueItemViewModel item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress);

  void Pause();

  void Stop();

  string SetTrimStart(QueueItemViewModel item);

  string SetTrimEnd(QueueItemViewModel item);

  string ResetTrim(QueueItemViewModel item);
}
