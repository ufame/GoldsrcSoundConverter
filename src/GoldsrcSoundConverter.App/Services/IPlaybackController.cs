using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.App.Services;

public interface IPlaybackController : IDisposable
{
  bool IsPlaying { get; }

  double PositionSeconds { get; }

  double TotalSeconds { get; }

  double Volume { get; set; }

  event EventHandler? PositionChanged;

  Task PrepareAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default);

  Task PrepareResultAsync(
    ConversionWorkItem item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default);

  void Play();

  void PlaySelection(double startSeconds, double endSeconds);

  void Pause();

  void Stop();
}
