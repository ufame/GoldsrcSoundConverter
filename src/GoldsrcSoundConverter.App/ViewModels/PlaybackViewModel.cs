using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed partial class PlaybackViewModel : ObservableObject, IDisposable
{
  private readonly QueueViewModel _queue;
  private readonly ConversionViewModel _conversion;
  private readonly IPlaybackCoordinator _playback;

  public PlaybackViewModel(QueueViewModel queue, ConversionViewModel conversion, IPlaybackCoordinator playback)
  {
    _queue = queue;
    _conversion = conversion;
    _playback = playback;

    _queue.PropertyChanged += OnQueuePropertyChanged;
    _playback.PositionChanged += OnPlaybackPositionChanged;
    _playback.Volume = PlaybackVolume;
  }

  [ObservableProperty]
  private double _playbackPositionSeconds;

  [ObservableProperty]
  private double _playbackVolume = 1.0;

  [ObservableProperty]
  private string _playbackPositionText = "00:00.000 / 00:00.000";

  [ObservableProperty]
  private string _editorTitle = "Выберите файл в очереди";

  public bool HasSelection => _queue.SelectedItem is not null;

  public event EventHandler<string>? StatusChanged;

  public void Dispose()
  {
    _queue.PropertyChanged -= OnQueuePropertyChanged;
    _playback.PositionChanged -= OnPlaybackPositionChanged;
    GC.SuppressFinalize(this);
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlayAsync()
  {
    if (_queue.SelectedItem is not { } item)
    {
      return;
    }

    ApplyPlaybackResult(await _playback
      .PlayAsync(item, new Progress<BootstrapProgress>(ApplyBootstrapProgress))
      .ConfigureAwait(true));
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void Pause()
  {
    _playback.Pause();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void Stop()
  {
    _playback.Stop();
    PlaybackPositionSeconds = 0;
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlaySelectionAsync()
  {
    if (_queue.SelectedItem is not { } item)
    {
      return;
    }

    ApplyPlaybackResult(await _playback
      .PlaySelectionAsync(item, new Progress<BootstrapProgress>(ApplyBootstrapProgress))
      .ConfigureAwait(true));
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PreviewResultAsync()
  {
    if (_queue.SelectedItem is not { } item)
    {
      return;
    }

    StatusChanged?.Invoke(this, "Рендер результата…");

    var previewDirectory = Path.Combine(
      Path.GetTempPath(), "GoldsrcSoundConverter", "preview", "result");
    var options = _conversion.CreatePreviewOptions(previewDirectory);
    Directory.CreateDirectory(options.OutputDirectory);

    ApplyPlaybackResult(await _playback
      .PreviewResultAsync(item, options, new Progress<BootstrapProgress>(ApplyBootstrapProgress))
      .ConfigureAwait(true));
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimStart()
  {
    if (_queue.SelectedItem is { } item)
    {
      StatusChanged?.Invoke(this, _playback.SetTrimStart(item));
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimEnd()
  {
    if (_queue.SelectedItem is { } item)
    {
      StatusChanged?.Invoke(this, _playback.SetTrimEnd(item));
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void ResetTrim()
  {
    if (_queue.SelectedItem is { } item)
    {
      StatusChanged?.Invoke(this, _playback.ResetTrim(item));
    }
  }

  private void ApplyPlaybackResult(PlaybackResult result)
  {
    if (result.Status is not null)
    {
      StatusChanged?.Invoke(this, result.Status);
    }
  }

  private void ApplyBootstrapProgress(BootstrapProgress progress)
  {
    if (progress.Percent is double percent)
    {
      _conversion.OverallProgress = percent;
    }
  }

  private void OnPlaybackPositionChanged(object? sender, EventArgs e)
  {
    PlaybackPositionSeconds = _playback.PositionSeconds;
    PlaybackPositionText = _playback.TotalSeconds > 0
      ? $"{TimeText.Format(PlaybackPositionSeconds)} / {TimeText.Format(_playback.TotalSeconds)}"
      : "00:00.000 / 00:00.000";
  }

  private void OnQueuePropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(QueueViewModel.SelectedItem))
    {
      OnSelectionChanged();
    }
  }

  private void OnSelectionChanged()
  {
    OnPropertyChanged(nameof(HasSelection));
    PlayCommand.NotifyCanExecuteChanged();
    PauseCommand.NotifyCanExecuteChanged();
    StopCommand.NotifyCanExecuteChanged();
    PlaySelectionCommand.NotifyCanExecuteChanged();
    PreviewResultCommand.NotifyCanExecuteChanged();
    SetTrimStartCommand.NotifyCanExecuteChanged();
    SetTrimEndCommand.NotifyCanExecuteChanged();
    ResetTrimCommand.NotifyCanExecuteChanged();

    _playback.Stop();
    PlaybackPositionSeconds = 0;
    PlaybackPositionText = "00:00.000 / 00:00.000";
    EditorTitle = _queue.SelectedItem is null ? "Выберите файл в очереди" : _queue.SelectedItem.FileName;
  }

  partial void OnPlaybackVolumeChanged(double value)
  {
    _playback.Volume = value;
  }
}
