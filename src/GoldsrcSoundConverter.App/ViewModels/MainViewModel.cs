using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  private readonly ISettingsStore _settingsStore;
  private readonly IFilePicker _filePicker;
  private readonly IFolderLauncher _folderLauncher;
  private readonly IPlaybackCoordinator _playback;
  private readonly IPresetCatalog _presetCatalog;
  private readonly ILogBuffer _log;
  private readonly OutputDirectoryProvider _outputDirectoryProvider;

  private bool _applyingPreset;

  public MainViewModel(
    ISettingsStore settingsStore,
    IFilePicker filePicker,
    IFolderLauncher folderLauncher,
    IPlaybackCoordinator playback,
    QueueViewModel queue,
    ConversionViewModel conversion,
    IPresetCatalog presetCatalog,
    ILogBuffer log,
    OutputDirectoryProvider outputDirectoryProvider)
  {
    _settingsStore = settingsStore;
    _filePicker = filePicker;
    _folderLauncher = folderLauncher;
    _playback = playback;
    _outputDirectoryProvider = outputDirectoryProvider;
    Queue = queue;
    Conversion = conversion;
    _presetCatalog = presetCatalog;
    _log = log;

    Queue.PropertyChanged += OnQueuePropertyChanged;
    Queue.StatusChanged += OnQueueStatusChanged;
    Conversion.StatusChanged += OnConversionStatusChanged;
    Conversion.FormatChanged += OnConversionFormatChanged;
    Conversion.OptionsChanged += OnConversionOptionsChanged;
    Conversion.RunFinished += OnConversionRunFinished;
    _playback.PositionChanged += OnPlaybackPositionChanged;

    var settings = _settingsStore.Load();
    InitialWindowWidth = settings.WindowWidth > 400 ? settings.WindowWidth : 1400;
    InitialWindowHeight = settings.WindowHeight > 300 ? settings.WindowHeight : 900;
    ApplySettings(settings);

    AppendLog("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public QueueViewModel Queue { get; }

  public ConversionViewModel Conversion { get; }

  public ObservableCollection<string> LogEntries => _log.Entries;

  public ObservableCollection<Cs16Preset> Presets { get; } = new();

  public double InitialWindowWidth { get; }

  public double InitialWindowHeight { get; }

  [ObservableProperty]
  private Cs16Preset? _selectedPreset;

  [ObservableProperty]
  private string _outputDirectory = string.Empty;

  [ObservableProperty]
  private string _statusText = "Готово";

  [ObservableProperty]
  private double _playbackPositionSeconds;

  [ObservableProperty]
  private double _playbackVolume = 1.0;

  [ObservableProperty]
  private string _playbackPositionText = "00:00.000 / 00:00.000";

  [ObservableProperty]
  private string _editorTitle = "Выберите файл в очереди";

  public bool HasSelection => Queue.SelectedItem is not null;

  public void Dispose()
  {
    Queue.PropertyChanged -= OnQueuePropertyChanged;
    Queue.StatusChanged -= OnQueueStatusChanged;
    Conversion.StatusChanged -= OnConversionStatusChanged;
    Conversion.FormatChanged -= OnConversionFormatChanged;
    Conversion.OptionsChanged -= OnConversionOptionsChanged;
    Conversion.RunFinished -= OnConversionRunFinished;
    _playback.PositionChanged -= OnPlaybackPositionChanged;
    GC.SuppressFinalize(this);
  }

  public void SaveSettingsWithWindow(double width, double height)
  {
    SaveSettingsCore(width, height);
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlayAsync()
  {
    if (Queue.SelectedItem is not { } item)
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
    if (Queue.SelectedItem is not { } item)
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
    if (Queue.SelectedItem is not { } item)
    {
      return;
    }

    StatusText = "Рендер результата…";

    var previewDirectory = Path.Combine(
      Path.GetTempPath(), "GoldsrcSoundConverter", "preview", "result");
    var options = Conversion.CreatePreviewOptions(previewDirectory);
    Directory.CreateDirectory(options.OutputDirectory);

    ApplyPlaybackResult(await _playback
      .PreviewResultAsync(item, options, new Progress<BootstrapProgress>(ApplyBootstrapProgress))
      .ConfigureAwait(true));
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimStart()
  {
    if (Queue.SelectedItem is { } item)
    {
      StatusText = _playback.SetTrimStart(item);
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimEnd()
  {
    if (Queue.SelectedItem is { } item)
    {
      StatusText = _playback.SetTrimEnd(item);
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void ResetTrim()
  {
    if (Queue.SelectedItem is { } item)
    {
      StatusText = _playback.ResetTrim(item);
    }
  }

  [RelayCommand]
  private void BrowseOutputDirectory()
  {
    var folder = _filePicker.PickFolder("Папка для результатов", OutputDirectory);
    if (folder is not null)
    {
      OutputDirectory = folder;
    }
  }

  [RelayCommand]
  private void OpenOutputFolder()
  {
    try
    {
      if (!Directory.Exists(OutputDirectory))
      {
        StatusText = "Папка результатов ещё не создана";
        return;
      }

      _folderLauncher.Open(OutputDirectory);
    }
    catch (Exception ex)
    {
      StatusText = "Не удалось открыть папку: " + ex.Message;
    }
  }

  [RelayCommand]
  private void SaveSettings()
  {
    SaveSettingsCore(null, null);
    StatusText = "Настройки сохранены";
  }

  private void ApplyPlaybackResult(PlaybackResult result)
  {
    if (result.Status is not null)
    {
      StatusText = result.Status;
    }
  }

  private void ApplyBootstrapProgress(BootstrapProgress progress)
  {
    if (progress.Percent is double percent)
    {
      Conversion.OverallProgress = percent;
    }
  }

  private void OnPlaybackPositionChanged(object? sender, EventArgs e)
  {
    PlaybackPositionSeconds = _playback.PositionSeconds;
    PlaybackPositionText = _playback.TotalSeconds > 0
      ? $"{TimeText.Format(PlaybackPositionSeconds)} / {TimeText.Format(_playback.TotalSeconds)}"
      : "00:00.000 / 00:00.000";
  }

  private void RefreshPresets()
  {
    _applyingPreset = true;
    Presets.Clear();
    foreach (var preset in _presetCatalog.ForFormat(Conversion.Format))
    {
      Presets.Add(preset);
    }

    SelectedPreset = _presetCatalog.Resolve(Conversion.BuildOptions());
    _applyingPreset = false;
  }

  private void EnsureCustomPreset()
  {
    _applyingPreset = true;
    SelectedPreset = _presetCatalog.Resolve(Conversion.BuildOptions());
    _applyingPreset = false;
  }

  private void ApplySettings(AppSettings settings)
  {
    _applyingPreset = true;
    Conversion.Format = settings.Format;
    Conversion.SampleRate = settings.SampleRate;
    Conversion.Channels = settings.Channels;
    Conversion.BitDepth = settings.BitDepth;
    Conversion.Mp3BitrateKbps = settings.Mp3BitrateKbps;
    Conversion.NormalizePeak = settings.NormalizePeak;
    Conversion.AsciiNames = settings.AsciiNames;
    Conversion.LowercaseNames = settings.LowercaseNames;
    Conversion.PreserveStructure = settings.PreserveStructure;
    Conversion.CollisionPolicy = settings.CollisionPolicy;
    Conversion.Parallelism = settings.Parallelism;
    Conversion.FfmpegCustomPath = settings.FfmpegCustomPath;
    OutputDirectory = settings.OutputDirectory;
    _applyingPreset = false;

    if (string.IsNullOrWhiteSpace(OutputDirectory))
    {
      OutputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GoldsrcSoundConverter");
    }

    RefreshPresets();
  }

  private void SaveSettingsCore(double? windowWidth, double? windowHeight)
  {
    try
    {
      var settings = new AppSettings
      {
        FfmpegCustomPath = Conversion.FfmpegCustomPath,
        OutputDirectory = OutputDirectory,
        Format = Conversion.Format,
        PresetId = SelectedPreset?.Id ?? Cs16Presets.CustomId,
        SampleRate = Conversion.SampleRate,
        Channels = Conversion.Channels,
        BitDepth = Conversion.BitDepth,
        Mp3BitrateKbps = Conversion.Mp3BitrateKbps,
        NormalizePeak = Conversion.NormalizePeak,
        AsciiNames = Conversion.AsciiNames,
        LowercaseNames = Conversion.LowercaseNames,
        PreserveStructure = Conversion.PreserveStructure,
        CollisionPolicy = Conversion.CollisionPolicy,
        Parallelism = Conversion.Parallelism,
      };

      if (windowWidth is > 400)
      {
        settings.WindowWidth = windowWidth.Value;
      }

      if (windowHeight is > 300)
      {
        settings.WindowHeight = windowHeight.Value;
      }

      _settingsStore.Save(settings);
    }
    catch (Exception ex)
    {
      AppendLog("Не удалось сохранить настройки: " + ex.Message);
    }
  }

  private void AppendLog(string message)
  {
    _log.Add(message);
  }

  private void OnQueuePropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
    if (e.PropertyName == nameof(QueueViewModel.SelectedItem))
    {
      OnQueueSelectionChanged();
    }
  }

  private void OnQueueSelectionChanged()
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
    EditorTitle = Queue.SelectedItem is null ? "Выберите файл в очереди" : Queue.SelectedItem.FileName;
  }

  private void OnQueueStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnConversionStatusChanged(object? sender, string message)
  {
    StatusText = message;
  }

  private void OnConversionFormatChanged(object? sender, EventArgs e)
  {
    if (!_applyingPreset)
    {
      RefreshPresets();
    }
  }

  private void OnConversionOptionsChanged(object? sender, EventArgs e)
  {
    if (!_applyingPreset)
    {
      EnsureCustomPreset();
    }
  }

  private void OnConversionRunFinished(object? sender, EventArgs e)
  {
    SaveSettingsCore(null, null);
  }

  partial void OnSelectedPresetChanged(Cs16Preset? value)
  {
    if (_applyingPreset || value is null)
    {
      return;
    }

    _applyingPreset = true;
    Conversion.SampleRate = value.SampleRate;
    Conversion.Channels = value.Channels;
    Conversion.BitDepth = value.BitDepth;
    Conversion.Mp3BitrateKbps = value.Mp3BitrateKbps;
    _applyingPreset = false;
  }

  partial void OnOutputDirectoryChanged(string value)
  {
    _outputDirectoryProvider.Value = value;
    Conversion.RecalculateTargetSizes();
  }

  partial void OnPlaybackVolumeChanged(double value)
  {
    _playback.Volume = value;
  }
}
