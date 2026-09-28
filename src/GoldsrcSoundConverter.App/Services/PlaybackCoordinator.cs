using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class PlaybackCoordinator : IPlaybackCoordinator
{
  private readonly IPlaybackController _playback;
  private readonly IConversionRequestFactory _requestFactory;
  private readonly ILogBuffer _log;

  private double _volume = 1.0;

  public PlaybackCoordinator(
    IPlaybackController playback,
    IConversionRequestFactory requestFactory,
    ILogBuffer log)
  {
    _playback = playback;
    _requestFactory = requestFactory;
    _log = log;
  }

  public double PositionSeconds => _playback.PositionSeconds;

  public double TotalSeconds => _playback.TotalSeconds;

  public double Volume
  {
    get => _volume;
    set
    {
      _volume = value;
      _playback.Volume = value;
    }
  }

  public event EventHandler? PositionChanged
  {
    add => _playback.PositionChanged += value;
    remove => _playback.PositionChanged -= value;
  }

  public async Task<PlaybackResult> PlayAsync(
    QueueItemViewModel item,
    IProgress<BootstrapProgress>? bootstrapProgress)
  {
    var prepared = await PrepareAsync(item.SourcePath, bootstrapProgress).ConfigureAwait(true);
    if (!prepared.Success)
    {
      return prepared;
    }

    _playback.Play();
    return PlaybackResult.Ok;
  }

  public async Task<PlaybackResult> PlaySelectionAsync(
    QueueItemViewModel item,
    IProgress<BootstrapProgress>? bootstrapProgress)
  {
    if (item.TrimEndSeconds - item.TrimStartSeconds <= 0.01)
    {
      return await PlayAsync(item, bootstrapProgress).ConfigureAwait(true);
    }

    var prepared = await PrepareAsync(item.SourcePath, bootstrapProgress).ConfigureAwait(true);
    if (!prepared.Success)
    {
      return prepared;
    }

    _playback.PlaySelection(item.TrimStartSeconds, item.TrimEndSeconds);
    return PlaybackResult.Ok;
  }

  public async Task<PlaybackResult> PreviewResultAsync(
    QueueItemViewModel item,
    ConversionOptions options,
    IProgress<BootstrapProgress>? bootstrapProgress)
  {
    try
    {
      var workItem = _requestFactory.CreateWorkItem(item);
      await _playback
        .PrepareResultAsync(workItem, options, bootstrapProgress, _log.Add)
        .ConfigureAwait(true);

      _playback.Volume = _volume;
      _playback.Play();
      return new PlaybackResult(true, "Воспроизведение результата");
    }
    catch (Exception ex)
    {
      return Failure(ex);
    }
  }

  public void Pause()
  {
    _playback.Pause();
  }

  public void Stop()
  {
    _playback.Stop();
  }

  public string SetTrimStart(QueueItemViewModel item)
  {
    item.TrimStartSeconds = Math.Min(PositionSeconds, item.TrimEndSeconds);
    return "Начало обрезки: " + TimeText.Format(item.TrimStartSeconds);
  }

  public string SetTrimEnd(QueueItemViewModel item)
  {
    item.TrimEndSeconds = Math.Max(PositionSeconds, item.TrimStartSeconds);
    return "Конец обрезки: " + TimeText.Format(item.TrimEndSeconds);
  }

  public string ResetTrim(QueueItemViewModel item)
  {
    item.TrimStartSeconds = 0;
    item.TrimEndSeconds = item.DurationSeconds;
    return "Обрезка сброшена";
  }

  private async Task<PlaybackResult> PrepareAsync(
    string sourcePath,
    IProgress<BootstrapProgress>? bootstrapProgress)
  {
    try
    {
      await _playback
        .PrepareAsync(sourcePath, bootstrapProgress, _log.Add)
        .ConfigureAwait(true);

      _playback.Volume = _volume;
      return PlaybackResult.Ok;
    }
    catch (Exception ex)
    {
      return Failure(ex);
    }
  }

  private PlaybackResult Failure(Exception ex)
  {
    var message = "Ошибка предпросмотра: " + ex.Message;
    _log.Add(message);
    return new PlaybackResult(false, message);
  }
}
