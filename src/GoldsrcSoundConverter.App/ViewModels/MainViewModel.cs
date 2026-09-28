using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  private readonly ISettingsStore _settingsStore;
  private readonly IFilePicker _filePicker;
  private readonly IFolderLauncher _folderLauncher;
  private readonly IConversionService _conversionService;
  private readonly IPlaybackCoordinator _playback;
  private readonly IQueueManager _queue;
  private readonly IConversionRequestFactory _requestFactory;
  private readonly IPresetCatalog _presetCatalog;
  private readonly IConversionRunController _runController;
  private readonly IQueueConversionPresenter _presenter;
  private readonly IWaveformLoader _waveforms;
  private readonly ILogBuffer _log;

  private bool _applyingPreset;

  public MainViewModel(
    ISettingsStore settingsStore,
    IFilePicker filePicker,
    IFolderLauncher folderLauncher,
    IConversionService conversionService,
    IPlaybackCoordinator playback,
    IQueueManager queue,
    IConversionRequestFactory requestFactory,
    IPresetCatalog presetCatalog,
    IConversionRunController runController,
    IQueueConversionPresenter presenter,
    IWaveformLoader waveforms,
    ILogBuffer log)
  {
    _settingsStore = settingsStore;
    _filePicker = filePicker;
    _folderLauncher = folderLauncher;
    _conversionService = conversionService;
    _playback = playback;
    _queue = queue;
    _requestFactory = requestFactory;
    _presetCatalog = presetCatalog;
    _runController = runController;
    _presenter = presenter;
    _waveforms = waveforms;
    _log = log;
    Items.CollectionChanged += (_, _) => StartCommand.NotifyCanExecuteChanged();
    _playback.PositionChanged += OnPlaybackPositionChanged;
    _runController.BusyChanged += OnRunControllerBusyChanged;

    var settings = _settingsStore.Load();
    InitialWindowWidth = settings.WindowWidth > 400 ? settings.WindowWidth : 1400;
    InitialWindowHeight = settings.WindowHeight > 300 ? settings.WindowHeight : 900;
    ApplySettings(settings);

    UpdateFfmpegStatus();
    AppendLog("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public ObservableCollection<QueueItemViewModel> Items => _queue.Items;

  public ObservableCollection<string> LogEntries => _log.Entries;

  public ObservableCollection<Cs16Preset> Presets { get; } = new();

  public IReadOnlyList<int> SampleRates { get; } = new[] { 11025, 22050, 44100 };

  public IReadOnlyList<int> Mp3Bitrates { get; } = new[] { 64, 96, 112, 128, 160, 192, 224, 256, 320 };

  public IReadOnlyList<int> ParallelismOptions { get; } = new[] { 1, 2, 3, 4, 6, 8, 12, 16 };

  public IReadOnlyList<DisplayOption<TargetChannels>> ChannelOptions { get; } = new[]
  {
    new DisplayOption<TargetChannels>("mono (3D-звуки, обязательно)", TargetChannels.Mono),
    new DisplayOption<TargetChannels>("stereo (музыка/2D)", TargetChannels.Stereo),
  };

  public IReadOnlyList<DisplayOption<TargetBitDepth>> BitDepthOptions { get; } = new[]
  {
    new DisplayOption<TargetBitDepth>("16-bit (рекомендуется)", TargetBitDepth.Sixteen),
    new DisplayOption<TargetBitDepth>("8-bit (меньше размер)", TargetBitDepth.Eight),
  };

  public IReadOnlyList<DisplayOption<CollisionPolicy>> CollisionOptions { get; } = new[]
  {
    new DisplayOption<CollisionPolicy>("Переименовать (_1, _2…)", CollisionPolicy.Rename),
    new DisplayOption<CollisionPolicy>("Перезаписать", CollisionPolicy.Overwrite),
    new DisplayOption<CollisionPolicy>("Пропустить", CollisionPolicy.Skip),
  };

  public double InitialWindowWidth { get; }

  public double InitialWindowHeight { get; }

  [ObservableProperty]
  private OutputAudioFormat _format = OutputAudioFormat.Wav;

  [ObservableProperty]
  private Cs16Preset? _selectedPreset;

  [ObservableProperty]
  private int _sampleRate = 22050;

  [ObservableProperty]
  private TargetChannels _channels = TargetChannels.Mono;

  [ObservableProperty]
  private TargetBitDepth _bitDepth = TargetBitDepth.Sixteen;

  [ObservableProperty]
  private int _mp3BitrateKbps = 128;

  [ObservableProperty]
  private bool _normalizePeak;

  [ObservableProperty]
  private bool _asciiNames = true;

  [ObservableProperty]
  private bool _lowercaseNames = true;

  [ObservableProperty]
  private bool _preserveStructure;

  [ObservableProperty]
  private string _outputDirectory = string.Empty;

  [ObservableProperty]
  private CollisionPolicy _collisionPolicy = CollisionPolicy.Rename;

  [ObservableProperty]
  private int _parallelism = 4;

  [ObservableProperty]
  private string? _ffmpegCustomPath;

  [ObservableProperty]
  private string _ffmpegStatusText = string.Empty;

  [ObservableProperty]
  private string _ffmpegDownloadText = string.Empty;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
  [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
  [NotifyCanExecuteChangedFor(nameof(StopCommand))]
  [NotifyCanExecuteChangedFor(nameof(PlaySelectionCommand))]
  [NotifyCanExecuteChangedFor(nameof(PreviewResultCommand))]
  [NotifyCanExecuteChangedFor(nameof(SetTrimStartCommand))]
  [NotifyCanExecuteChangedFor(nameof(SetTrimEndCommand))]
  [NotifyCanExecuteChangedFor(nameof(ResetTrimCommand))]
  private QueueItemViewModel? _selectedItem;

  [ObservableProperty]
  private double _overallProgress;

  [ObservableProperty]
  private string _statusText = "Готово";

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartCommand))]
  [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
  [NotifyCanExecuteChangedFor(nameof(AddFilesCommand))]
  [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
  [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
  [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
  [NotifyPropertyChangedFor(nameof(CanEditQueue))]
  private bool _isBusy;

  [ObservableProperty]
  private double _playbackPositionSeconds;

  [ObservableProperty]
  private double _playbackVolume = 1.0;

  [ObservableProperty]
  private string _playbackPositionText = "00:00.000 / 00:00.000";

  [ObservableProperty]
  private string _editorTitle = "Выберите файл в очереди";

  public bool IsWavFormat
  {
    get => Format == OutputAudioFormat.Wav;
    set
    {
      if (value)
      {
        SetFormat(OutputAudioFormat.Wav);
      }
    }
  }

  public bool IsMp3Format
  {
    get => Format == OutputAudioFormat.Mp3;
    set
    {
      if (value)
      {
        SetFormat(OutputAudioFormat.Mp3);
      }
    }
  }

  public bool HasSelection => SelectedItem is not null;

  public bool CanStart => !IsBusy && Items.Count > 0;

  public bool CanEditQueue => !IsBusy;

  public void Dispose()
  {
    _playback.PositionChanged -= OnPlaybackPositionChanged;
    _runController.BusyChanged -= OnRunControllerBusyChanged;
    _runController.Dispose();
    GC.SuppressFinalize(this);
  }

  public void AddPaths(IEnumerable<string> paths)
  {
    if (IsBusy)
    {
      StatusText = "Дождитесь окончания конвертации";
      return;
    }

    var added = _queue.Add(paths, BuildOptions());

    StatusText = added > 0
      ? $"Добавлено файлов: {added}"
      : "Новые файлы не найдены";

    if (added > 0 && !_conversionService.TryResolveFfmpeg(out _, out _))
    {
      AppendLog("FFmpeg ещё не установлен — анализ и волновая форма появятся после первой конвертации.");
    }
  }

  public void SaveSettingsWithWindow(double width, double height)
  {
    SaveSettingsCore(width, height);
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void AddFiles()
  {
    var files = _filePicker.PickFiles();
    if (files.Count > 0)
    {
      AddPaths(files);
    }
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void AddFolder()
  {
    var folder = _filePicker.PickFolder("Выберите папку со звуками");
    if (folder is not null)
    {
      AddPaths(new[] { folder });
    }
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void RemoveSelected()
  {
    if (SelectedItem is null)
    {
      return;
    }

    _queue.Remove(SelectedItem);
    SelectedItem = null;
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void Clear()
  {
    _queue.Clear();
    SelectedItem = null;
    StatusText = "Очередь очищена";
  }

  [RelayCommand(CanExecute = nameof(CanStart))]
  private async Task StartAsync()
  {
    if (Items.Count == 0)
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(OutputDirectory))
    {
      StatusText = "Укажите папку результатов";
      return;
    }

    try
    {
      Directory.CreateDirectory(OutputDirectory);
    }
    catch (Exception ex)
    {
      StatusText = "Не удалось создать папку результатов: " + ex.Message;
      return;
    }

    OverallProgress = 0;
    StatusText = "Подготовка FFmpeg…";

    var options = BuildOptions();

    if (SelectedItem is not null)
    {
      _ = _waveforms.EnsureLoadedAsync(SelectedItem, options);
    }

    StatusText = $"Конвертация: {Items.Count} файл(ов)…";

    try
    {
      var workItems = _presenter.BeginRun();

      var result = await _runController
        .RunAsync(
          workItems,
          options,
          _presenter.CreateProgressHandler(value => OverallProgress = value),
          _presenter.CreateProbeHandler(options),
          new Progress<BootstrapProgress>(ApplyBootstrapProgress))
        .ConfigureAwait(true);

      _presenter.Complete(result);
      UpdateFfmpegStatus();

      if (result.Summary.Cancelled)
      {
        StatusText = "Конвертация отменена";
      }
      else
      {
        OverallProgress = 1;
        StatusText = $"Готово: успешно {result.Summary.Completed}, "
          + $"ошибок {result.Summary.Failed}, пропущено {result.Summary.Skipped}";
      }

      AppendLog(StatusText);
    }
    catch (Exception ex)
    {
      StatusText = "Ошибка: " + ex.Message;
      AppendLog(StatusText);
    }
    finally
    {
      SaveSettingsCore(null, null);
    }
  }

  [RelayCommand(CanExecute = nameof(IsBusy))]
  private void Cancel()
  {
    StatusText = "Отмена…";
    _runController.Cancel();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlayAsync()
  {
    if (SelectedItem is not { } item)
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
    if (SelectedItem is not { } item)
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
    if (SelectedItem is not { } item)
    {
      return;
    }

    StatusText = "Рендер результата…";

    var previewDirectory = Path.Combine(
      Path.GetTempPath(), "GoldsrcSoundConverter", "preview", "result");
    var options = _requestFactory.CreatePreviewOptions(SnapshotSettings(), previewDirectory);
    Directory.CreateDirectory(options.OutputDirectory);

    ApplyPlaybackResult(await _playback
      .PreviewResultAsync(item, options, new Progress<BootstrapProgress>(ApplyBootstrapProgress))
      .ConfigureAwait(true));
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimStart()
  {
    if (SelectedItem is { } item)
    {
      StatusText = _playback.SetTrimStart(item);
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimEnd()
  {
    if (SelectedItem is { } item)
    {
      StatusText = _playback.SetTrimEnd(item);
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void ResetTrim()
  {
    if (SelectedItem is { } item)
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
  private void BrowseFfmpeg()
  {
    var file = _filePicker.PickFile("Выберите ffmpeg.exe", "ffmpeg.exe|ffmpeg.exe|Все файлы|*.*");
    if (file is not null)
    {
      FfmpegCustomPath = file;
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
    FfmpegStatusText = progress.Percent is double percent
      ? $"{progress.Stage} {percent:P0}"
      : progress.Stage;

    if (progress.Percent is double value)
    {
      OverallProgress = value;
    }
  }

  private void OnRunControllerBusyChanged(object? sender, EventArgs e)
  {
    IsBusy = _runController.IsBusy;
  }

  private void OnPlaybackPositionChanged(object? sender, EventArgs e)
  {
    PlaybackPositionSeconds = _playback.PositionSeconds;
    PlaybackPositionText = _playback.TotalSeconds > 0
      ? $"{TimeText.Format(PlaybackPositionSeconds)} / {TimeText.Format(_playback.TotalSeconds)}"
      : "00:00.000 / 00:00.000";
  }

  private ConversionOptions BuildOptions()
  {
    return _requestFactory.CreateOptions(SnapshotSettings());
  }

  private ConversionSettings SnapshotSettings()
  {
    return new ConversionSettings(
      Format,
      SampleRate,
      Channels,
      BitDepth,
      Mp3BitrateKbps,
      NormalizePeak,
      AsciiNames,
      LowercaseNames,
      PreserveStructure,
      CollisionPolicy,
      Parallelism,
      OutputDirectory);
  }

  private void SetFormat(OutputAudioFormat format)
  {
    if (Format == format)
    {
      return;
    }

    Format = format;
    OnPropertyChanged(nameof(IsWavFormat));
    OnPropertyChanged(nameof(IsMp3Format));
    RefreshPresets();
    RecomputeTargetSizes();
  }

  private void RefreshPresets()
  {
    _applyingPreset = true;
    Presets.Clear();
    foreach (var preset in _presetCatalog.ForFormat(Format))
    {
      Presets.Add(preset);
    }

    SelectedPreset = _presetCatalog.Resolve(BuildOptions());
    _applyingPreset = false;
  }

  private void EnsureCustomPreset()
  {
    _applyingPreset = true;
    SelectedPreset = _presetCatalog.Resolve(BuildOptions());
    _applyingPreset = false;
  }

  private void RecomputeTargetSizes()
  {
    _queue.RecalculateTargetSizes(BuildOptions());
  }

  private void ApplySettings(AppSettings settings)
  {
    _applyingPreset = true;
    Format = settings.Format;
    SampleRate = settings.SampleRate;
    Channels = settings.Channels;
    BitDepth = settings.BitDepth;
    Mp3BitrateKbps = settings.Mp3BitrateKbps;
    NormalizePeak = settings.NormalizePeak;
    AsciiNames = settings.AsciiNames;
    LowercaseNames = settings.LowercaseNames;
    PreserveStructure = settings.PreserveStructure;
    OutputDirectory = settings.OutputDirectory;
    CollisionPolicy = settings.CollisionPolicy;
    Parallelism = settings.Parallelism;
    FfmpegCustomPath = settings.FfmpegCustomPath;
    _applyingPreset = false;

    if (string.IsNullOrWhiteSpace(OutputDirectory))
    {
      OutputDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "GoldsrcSoundConverter");
    }

    OnPropertyChanged(nameof(IsWavFormat));
    OnPropertyChanged(nameof(IsMp3Format));
    RefreshPresets();
  }

  private void SaveSettingsCore(double? windowWidth, double? windowHeight)
  {
    try
    {
      var settings = new AppSettings
      {
        FfmpegCustomPath = FfmpegCustomPath,
        OutputDirectory = OutputDirectory,
        Format = Format,
        PresetId = SelectedPreset?.Id ?? Cs16Presets.CustomId,
        SampleRate = SampleRate,
        Channels = Channels,
        BitDepth = BitDepth,
        Mp3BitrateKbps = Mp3BitrateKbps,
        NormalizePeak = NormalizePeak,
        AsciiNames = AsciiNames,
        LowercaseNames = LowercaseNames,
        PreserveStructure = PreserveStructure,
        CollisionPolicy = CollisionPolicy,
        Parallelism = Parallelism,
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

  private void UpdateFfmpegStatus()
  {
    if (_conversionService.FfmpegPath is not null && _conversionService.FfprobePath is not null)
    {
      FfmpegStatusText = "FFmpeg: готов";
      FfmpegDownloadText = _conversionService.FfmpegPath;
      return;
    }

    FfmpegStatusText = "FFmpeg: будет загружен при первой конвертации";
    FfmpegDownloadText = "Автозагрузка (~80 МБ) при первом запуске конвертации "
      + "или укажите свой ffmpeg.exe.";
  }

  private void AppendLog(string message)
  {
    _log.Add(message);
  }

  partial void OnSelectedItemChanged(QueueItemViewModel? value)
  {
    OnPropertyChanged(nameof(HasSelection));
    _playback.Stop();
    PlaybackPositionSeconds = 0;
    PlaybackPositionText = "00:00.000 / 00:00.000";
    EditorTitle = value is null ? "Выберите файл в очереди" : value.FileName;

    if (value is not null)
    {
      _ = _waveforms.EnsureLoadedAsync(value, BuildOptions());
    }
  }

  partial void OnSelectedPresetChanged(Cs16Preset? value)
  {
    if (_applyingPreset || value is null)
    {
      return;
    }

    _applyingPreset = true;
    SampleRate = value.SampleRate;
    Channels = value.Channels;
    BitDepth = value.BitDepth;
    Mp3BitrateKbps = value.Mp3BitrateKbps;
    _applyingPreset = false;
    RecomputeTargetSizes();
  }

  partial void OnSampleRateChanged(int value)
  {
    if (!_applyingPreset)
    {
      EnsureCustomPreset();
    }

    RecomputeTargetSizes();
  }

  partial void OnChannelsChanged(TargetChannels value)
  {
    if (!_applyingPreset)
    {
      EnsureCustomPreset();
    }

    RecomputeTargetSizes();
  }

  partial void OnBitDepthChanged(TargetBitDepth value)
  {
    if (!_applyingPreset)
    {
      EnsureCustomPreset();
    }

    RecomputeTargetSizes();
  }

  partial void OnMp3BitrateKbpsChanged(int value)
  {
    if (!_applyingPreset)
    {
      EnsureCustomPreset();
    }

    RecomputeTargetSizes();
  }

  partial void OnOutputDirectoryChanged(string value)
  {
    RecomputeTargetSizes();
  }

  partial void OnFfmpegCustomPathChanged(string? value)
  {
    _conversionService.SetCustomFfmpegPath(value);
    UpdateFfmpegStatus();
  }

  partial void OnPlaybackVolumeChanged(double value)
  {
    _playback.Volume = value;
  }
}
