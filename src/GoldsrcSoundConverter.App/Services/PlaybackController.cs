using System.Windows.Threading;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class PlaybackController : IPlaybackController
{
  private readonly IAudioPreview _preview;
  private readonly IConversionService _conversionService;
  private readonly DispatcherTimer _timer;

  private bool _playSelection;
  private double? _stopAtSeconds;

  public PlaybackController(IAudioPreview preview, IConversionService conversionService)
  {
    _preview = preview;
    _conversionService = conversionService;

    _timer = new DispatcherTimer(DispatcherPriority.Background)
    {
      Interval = TimeSpan.FromMilliseconds(60),
    };
    _timer.Tick += (_, _) => OnTick();
    _timer.Start();

    _preview.PlaybackStopped += (_, _) => RaisePositionChanged();
  }

  public bool IsPlaying => _preview.IsPlaying;

  public double PositionSeconds => _preview.Position.TotalSeconds;

  public double TotalSeconds => _preview.TotalTime.TotalSeconds;

  public double Volume
  {
    get => _preview.Volume;
    set
    {
      if (_preview.HasTrack)
      {
        _preview.Volume = value;
      }
    }
  }

  public event EventHandler? PositionChanged;

  public async Task PrepareAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    var playable = await _conversionService
      .PreparePlayableAsync(sourcePath, bootstrapProgress, log, cancellationToken)
      .ConfigureAwait(true);

    _preview.Load(playable);
  }

  public async Task PrepareResultAsync(
    ConversionWorkItem item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    var outcome = await _conversionService
      .ConvertSingleAsync(item, options, bootstrapProgress, log, cancellationToken)
      .ConfigureAwait(true);

    if (!outcome.Success || outcome.OutputPath is null)
    {
      throw new InvalidOperationException(outcome.Error ?? "Не удалось создать результат.");
    }

    _preview.Load(outcome.OutputPath);
  }

  public void Play()
  {
    _playSelection = false;
    _stopAtSeconds = null;
    _preview.Play();
  }

  public void PlaySelection(double startSeconds, double endSeconds)
  {
    _preview.Position = TimeSpan.FromSeconds(startSeconds);
    _playSelection = true;
    _stopAtSeconds = endSeconds;
    _preview.Play();
  }

  public void Pause()
  {
    _preview.Pause();
  }

  public void Stop()
  {
    _preview.Stop();
    _playSelection = false;
    _stopAtSeconds = null;
    RaisePositionChanged();
  }

  public void Dispose()
  {
    _timer.Stop();
    _preview.Dispose();
  }

  private void OnTick()
  {
    if (!_preview.HasTrack)
    {
      return;
    }

    if (_playSelection
      && _stopAtSeconds is double stopAt
      && _preview.Position.TotalSeconds >= stopAt)
    {
      _preview.Pause();
      _playSelection = false;
      _stopAtSeconds = null;
    }

    RaisePositionChanged();
  }

  private void RaisePositionChanged()
  {
    PositionChanged?.Invoke(this, EventArgs.Empty);
  }
}
