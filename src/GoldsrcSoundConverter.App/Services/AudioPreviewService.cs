using NAudio.Wave;

namespace GoldsrcSoundConverter.App.Services;

public sealed class AudioPreviewService : IDisposable
{
  private WaveOut? _output;
  private AudioFileReader? _reader;
  private string? _path;

  public event EventHandler? PlaybackStopped;

  public string? CurrentPath => _path;

  public bool IsPlaying => _output?.PlaybackState == PlaybackState.Playing;

  public bool HasTrack => _reader is not null;

  public TimeSpan TotalTime => _reader?.TotalTime ?? TimeSpan.Zero;

  public TimeSpan Position
  {
    get => _reader?.CurrentTime ?? TimeSpan.Zero;
    set
    {
      if (_reader is null)
      {
        return;
      }

      _reader.CurrentTime = value < TimeSpan.Zero
        ? TimeSpan.Zero
        : value > _reader.TotalTime ? _reader.TotalTime : value;
    }
  }

  public double Volume
  {
    get => _reader?.Volume ?? 1.0;
    set
    {
      if (_reader is not null)
      {
        _reader.Volume = (float)Math.Clamp(value, 0, 1);
      }
    }
  }

  public void Load(string path)
  {
    if (string.Equals(_path, path, StringComparison.OrdinalIgnoreCase) && _reader is not null)
    {
      return;
    }

    Unload();
    _reader = new AudioFileReader(path);
    _output = new WaveOut
    {
      BufferMilliseconds = 100,
      NumberOfBuffers = 3,
    };
    _output.Init(_reader);
    _output.PlaybackStopped += (_, _) => PlaybackStopped?.Invoke(this, EventArgs.Empty);
    _path = path;
  }

  public void Play()
  {
    _output?.Play();
  }

  public void Pause()
  {
    _output?.Pause();
  }

  public void Stop()
  {
    if (_output is null)
    {
      return;
    }

    _output.Stop();
    if (_reader is not null)
    {
      _reader.CurrentTime = TimeSpan.Zero;
    }
  }

  public void Unload()
  {
    if (_output is not null)
    {
      _output.Stop();
      _output.Dispose();
      _output = null;
    }

    _reader?.Dispose();
    _reader = null;
    _path = null;
  }

  public void Dispose()
  {
    Unload();
  }
}
