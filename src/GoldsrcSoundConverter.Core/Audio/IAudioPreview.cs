namespace GoldsrcSoundConverter.Core.Audio;

public interface IAudioPreview : IDisposable
{
  bool HasTrack { get; }

  bool IsPlaying { get; }

  TimeSpan Position { get; set; }

  TimeSpan TotalTime { get; }

  double Volume { get; set; }

  event EventHandler? PlaybackStopped;

  void Load(string path);

  void Play();

  void Pause();

  void Stop();

  void Unload();
}
