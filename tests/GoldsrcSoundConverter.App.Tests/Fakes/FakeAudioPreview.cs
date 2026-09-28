using GoldsrcSoundConverter.Core.Audio;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeAudioPreview : IAudioPreview
{
  public bool HasTrack { get; private set; }

  public bool IsPlaying { get; private set; }

  public TimeSpan Position { get; set; }

  public TimeSpan TotalTime { get; set; } = TimeSpan.FromSeconds(60);

  public double Volume { get; set; } = 1.0;

  public string? LoadedPath { get; private set; }

  public int LoadCount { get; private set; }

  public int PlayCount { get; private set; }

  public int PauseCount { get; private set; }

  public int StopCount { get; private set; }

  public event EventHandler? PlaybackStopped;

  public void Load(string path)
  {
    LoadedPath = path;
    HasTrack = true;
    LoadCount++;
  }

  public void Play()
  {
    IsPlaying = true;
    PlayCount++;
  }

  public void Pause()
  {
    IsPlaying = false;
    PauseCount++;
  }

  public void Stop()
  {
    IsPlaying = false;
    Position = TimeSpan.Zero;
    StopCount++;
  }

  public void Unload()
  {
    HasTrack = false;
    LoadedPath = null;
    IsPlaying = false;
  }

  public void Dispose()
  {
    Unload();
  }

  public void RaisePlaybackStopped()
  {
    PlaybackStopped?.Invoke(this, EventArgs.Empty);
  }
}
