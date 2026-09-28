using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakePlaybackController : IPlaybackController
{
  public bool IsPlaying { get; private set; }

  public double PositionSeconds { get; set; }

  public double TotalSeconds { get; set; } = 60;

  public double Volume { get; set; } = 1.0;

  public string? PreparedPath { get; private set; }

  public List<(double Start, double End)> Selections { get; } = new();

  public int PlayCount { get; private set; }

  public int PauseCount { get; private set; }

  public int StopCount { get; private set; }

  public Exception? PrepareException { get; set; }

  public Exception? PrepareResultException { get; set; }

  public event EventHandler? PositionChanged;

  public Task PrepareAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (PrepareException is not null)
    {
      throw PrepareException;
    }

    PreparedPath = sourcePath;
    return Task.CompletedTask;
  }

  public Task PrepareResultAsync(
    ConversionWorkItem item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (PrepareResultException is not null)
    {
      throw PrepareResultException;
    }

    PreparedPath = item.SourcePath + ".result.wav";
    return Task.CompletedTask;
  }

  public void Play()
  {
    IsPlaying = true;
    PlayCount++;
  }

  public void PlaySelection(double startSeconds, double endSeconds)
  {
    Selections.Add((startSeconds, endSeconds));
    IsPlaying = true;
  }

  public void Pause()
  {
    IsPlaying = false;
    PauseCount++;
  }

  public void Stop()
  {
    IsPlaying = false;
    StopCount++;
  }

  public void Dispose()
  {
  }

  public void RaisePositionChanged()
  {
    PositionChanged?.Invoke(this, EventArgs.Empty);
  }
}
