using CommunityToolkit.Mvvm.ComponentModel;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  public MainViewModel(
    QueueViewModel queue,
    ConversionViewModel conversion,
    PlaybackViewModel playback,
    PresetViewModel presets,
    SettingsViewModel settings,
    LogViewModel log)
  {
    Queue = queue;
    Conversion = conversion;
    Playback = playback;
    Presets = presets;
    Settings = settings;
    Log = log;

    Queue.StatusChanged += OnQueueStatusChanged;
    Conversion.StatusChanged += OnConversionStatusChanged;
    Conversion.RunFinished += OnConversionRunFinished;
    Playback.StatusChanged += OnPlaybackStatusChanged;
    Settings.StatusChanged += OnSettingsStatusChanged;

    Log.Append("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public QueueViewModel Queue { get; }

  public ConversionViewModel Conversion { get; }

  public PlaybackViewModel Playback { get; }

  public PresetViewModel Presets { get; }

  public SettingsViewModel Settings { get; }

  public LogViewModel Log { get; }

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
