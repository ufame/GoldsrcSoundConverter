using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  private readonly ISettingsStore _settingsStore;
  private readonly IFilePicker _filePicker;
  private readonly IFolderLauncher _folderLauncher;
  private readonly IPresetCatalog _presetCatalog;
  private readonly ILogBuffer _log;
  private readonly OutputDirectoryProvider _outputDirectoryProvider;

  private bool _applyingPreset;

  public MainViewModel(
    ISettingsStore settingsStore,
    IFilePicker filePicker,
    IFolderLauncher folderLauncher,
    QueueViewModel queue,
    ConversionViewModel conversion,
    PlaybackViewModel playback,
    IPresetCatalog presetCatalog,
    ILogBuffer log,
    OutputDirectoryProvider outputDirectoryProvider)
  {
    _settingsStore = settingsStore;
    _filePicker = filePicker;
    _folderLauncher = folderLauncher;
    Queue = queue;
    Conversion = conversion;
    Playback = playback;
    _presetCatalog = presetCatalog;
    _log = log;
    _outputDirectoryProvider = outputDirectoryProvider;

    Queue.StatusChanged += OnQueueStatusChanged;
    Conversion.StatusChanged += OnConversionStatusChanged;
    Conversion.FormatChanged += OnConversionFormatChanged;
    Conversion.OptionsChanged += OnConversionOptionsChanged;
    Conversion.RunFinished += OnConversionRunFinished;
    Playback.StatusChanged += OnPlaybackStatusChanged;

    var settings = _settingsStore.Load();
    InitialWindowWidth = settings.WindowWidth > 400 ? settings.WindowWidth : 1400;
    InitialWindowHeight = settings.WindowHeight > 300 ? settings.WindowHeight : 900;
    ApplySettings(settings);

    AppendLog("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public QueueViewModel Queue { get; }

  public ConversionViewModel Conversion { get; }

  public PlaybackViewModel Playback { get; }

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

  public void Dispose()
  {
    Queue.StatusChanged -= OnQueueStatusChanged;
    Conversion.StatusChanged -= OnConversionStatusChanged;
    Conversion.FormatChanged -= OnConversionFormatChanged;
    Conversion.OptionsChanged -= OnConversionOptionsChanged;
    Conversion.RunFinished -= OnConversionRunFinished;
    Playback.StatusChanged -= OnPlaybackStatusChanged;
    GC.SuppressFinalize(this);
  }

  public void SaveSettingsWithWindow(double width, double height)
  {
    SaveSettingsCore(width, height);
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
}
