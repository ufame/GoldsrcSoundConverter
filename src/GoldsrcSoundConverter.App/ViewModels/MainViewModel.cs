using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GoldsrcSoundConverter.App.Services;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  private readonly ILogBuffer _log;

  public MainViewModel(
    QueueViewModel queue,
    ConversionViewModel conversion,
    PlaybackViewModel playback,
    PresetViewModel presets,
    SettingsViewModel settings,
    ILogBuffer log)
  {
    Queue = queue;
    Conversion = conversion;
    Playback = playback;
    Presets = presets;
    Settings = settings;
    _log = log;

    Queue.StatusChanged += OnQueueStatusChanged;
    Conversion.StatusChanged += OnConversionStatusChanged;
    Conversion.RunFinished += OnConversionRunFinished;
    Playback.StatusChanged += OnPlaybackStatusChanged;
    Settings.StatusChanged += OnSettingsStatusChanged;

    AppendLog("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public QueueViewModel Queue { get; }

  public ConversionViewModel Conversion { get; }

  public PlaybackViewModel Playback { get; }

  public PresetViewModel Presets { get; }

  public SettingsViewModel Settings { get; }

  public ObservableCollection<string> LogEntries => _log.Entries;

  [ObservableProperty]
  private string _statusText = "Готово";

  public void Dispose()
  {
    Queue.StatusChanged -= OnQueueStatusChanged;
    Conversion.StatusChanged -= OnConversionStatusChanged;
    Conversion.RunFinished -= OnConversionRunFinished;
    Playback.StatusChanged -= OnPlaybackStatusChanged;
    Settings.StatusChanged -= OnSettingsStatusChanged;
    GC.SuppressFinalize(this);
  }

  private void AppendLog(string message)
  {
    _log.Add(message);
  }

  private void OnQueueStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnConversionStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnPlaybackStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnSettingsStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnConversionRunFinished(object? sender, EventArgs e)
  {
    Settings.Save();
  }
}
