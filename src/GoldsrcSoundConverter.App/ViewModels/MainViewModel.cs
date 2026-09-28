using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Files;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
  private readonly ISettingsStore _settingsStore;
  private readonly IFilePicker _filePicker;
  private readonly AudioPreviewService _preview = new();
  private readonly SemaphoreSlim _ffmpegLock = new(1, 1);
  private readonly SemaphoreSlim _probeGate = new(3, 3);
  private readonly DispatcherTimer _timer;

  private string? _ffmpegPath;
  private string? _ffprobePath;
  private CancellationTokenSource? _conversionCts;
  private bool _applyingPreset;
  private bool _playSelection;
  private double? _stopAtSeconds;

  public MainViewModel(ISettingsStore settingsStore, IFilePicker filePicker)
  {
    _settingsStore = settingsStore;
    _filePicker = filePicker;
    Items.CollectionChanged += (_, _) => StartCommand.NotifyCanExecuteChanged();
    _preview.PlaybackStopped += OnPlaybackStopped;

    var settings = _settingsStore.Load();
    InitialWindowWidth = settings.WindowWidth > 400 ? settings.WindowWidth : 1400;
    InitialWindowHeight = settings.WindowHeight > 300 ? settings.WindowHeight : 900;
    ApplySettings(settings);

    _timer = new DispatcherTimer(DispatcherPriority.Background)
    {
      Interval = TimeSpan.FromMilliseconds(60),
    };
    _timer.Tick += (_, _) => Tick();
    _timer.Start();

    UpdateFfmpegStatus();
    AppendLog("Готово к работе. Перетащите файлы в окно или нажмите «Добавить файлы».");
  }

  public ObservableCollection<QueueItemViewModel> Items { get; } = new();

  public ObservableCollection<string> LogEntries { get; } = new();

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
    _timer.Stop();
    _preview.Dispose();
    _conversionCts?.Dispose();
  }

  public void AddPaths(IEnumerable<string> paths)
  {
    if (IsBusy)
    {
      StatusText = "Дождитесь окончания конвертации";
      return;
    }

    var added = 0;

    foreach (var path in paths)
    {
      try
      {
        if (Directory.Exists(path))
        {
          foreach (var file in Directory
                     .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .Where(AudioFileTypes.IsSupported))
          {
            if (AddFile(file, path))
            {
              added++;
            }
          }
        }
        else if (File.Exists(path) && AudioFileTypes.IsSupported(path) && AddFile(path, null))
        {
          added++;
        }
      }
      catch (Exception ex)
      {
        AppendLog($"Не удалось добавить «{path}»: {ex.Message}");
      }
    }

    StatusText = added > 0
      ? $"Добавлено файлов: {added}"
      : "Новые файлы не найдены";

    if (added > 0 && !TryGetReadyFfmpeg(out _, out _))
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

    Items.Remove(SelectedItem);
    ReindexItems();
    SelectedItem = null;
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void Clear()
  {
    Items.Clear();
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

    IsBusy = true;
    OverallProgress = 0;
    _conversionCts = new CancellationTokenSource();
    var cancellationToken = _conversionCts.Token;

    try
    {
      StatusText = "Подготовка FFmpeg…";
      var (ffmpeg, ffprobe) = await EnsureFfmpegAsync(showUiProgress: true, cancellationToken);

      await ProbeMissingItemsAsync(ffprobe, cancellationToken);
      if (SelectedItem is not null)
      {
        _ = LoadWaveformAsync(SelectedItem);
      }

      var options = BuildOptions();
      var jobs = new List<ConversionJob>();
      var knownInfos = new Dictionary<Guid, AudioInfo>();

      foreach (var item in Items)
      {
        item.Progress = 0;
        item.Error = null;
        item.OutputPath = null;
        item.Stage = ConversionStage.Pending;
        jobs.Add(new ConversionJob(
          item.Id,
          item.SourcePath,
          item.SourceRoot,
          item.HasTrim ? TimeSpan.FromSeconds(item.TrimStartSeconds) : null,
          item.HasTrim ? TimeSpan.FromSeconds(item.TrimEndSeconds) : null));

        if (item.Info is not null)
        {
          knownInfos[item.Id] = item.Info;
        }
      }

      var plannedJobs = new ConversionPlanner().Plan(jobs, options);
      var progress = new Progress<ConversionProgress>(ApplyProgress);
      StatusText = $"Конвертация: {plannedJobs.Count} файл(ов)…";

      var outcomes = await new BatchConverter(ffmpeg, ffprobe)
        .RunAsync(plannedJobs, options, knownInfos, progress, AppendLog, cancellationToken)
        .ConfigureAwait(true);

      var completed = 0;
      var failed = 0;
      var skipped = 0;

      foreach (var outcome in outcomes)
      {
        var item = Items.FirstOrDefault(i => i.Id == outcome.JobId);
        if (item is null)
        {
          continue;
        }

        if (outcome.Success && outcome.Skipped)
        {
          item.Stage = ConversionStage.Skipped;
          skipped++;
        }
        else if (outcome.Success)
        {
          item.Stage = ConversionStage.Completed;
          item.Progress = 1;
          item.OutputPath = outcome.OutputPath;
          completed++;
        }
        else
        {
          item.Stage = ConversionStage.Failed;
          item.Error = outcome.Error;
          failed++;
        }
      }

      OverallProgress = 1;
      StatusText = $"Готово: успешно {completed}, ошибок {failed}, пропущено {skipped}";
      AppendLog(StatusText);
    }
    catch (OperationCanceledException)
    {
      foreach (var item in Items.Where(i => i.Stage is ConversionStage.Pending
        or ConversionStage.Probing
        or ConversionStage.Normalizing
        or ConversionStage.Converting))
      {
        item.Stage = ConversionStage.Pending;
        item.Progress = 0;
      }

      StatusText = "Конвертация отменена";
      AppendLog(StatusText);
    }
    catch (Exception ex)
    {
      StatusText = "Ошибка: " + ex.Message;
      AppendLog("Ошибка: " + ex.Message);
    }
    finally
    {
      IsBusy = false;
      _conversionCts?.Dispose();
      _conversionCts = null;
      SaveSettingsCore(null, null);
    }
  }

  [RelayCommand(CanExecute = nameof(IsBusy))]
  private void Cancel()
  {
    StatusText = "Отмена…";
    _conversionCts?.Cancel();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlayAsync()
  {
    var item = SelectedItem;
    if (item is null || !await PreparePreviewAsync(item))
    {
      return;
    }

    _playSelection = false;
    _stopAtSeconds = null;
    _preview.Play();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void Pause()
  {
    _preview.Pause();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void Stop()
  {
    _preview.Stop();
    _stopAtSeconds = null;
    PlaybackPositionSeconds = 0;
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PlaySelectionAsync()
  {
    var item = SelectedItem;
    if (item is null)
    {
      return;
    }

    if (item.TrimEndSeconds - item.TrimStartSeconds <= 0.01)
    {
      await PlayAsync();
      return;
    }

    if (!await PreparePreviewAsync(item))
    {
      return;
    }

    _preview.Position = TimeSpan.FromSeconds(item.TrimStartSeconds);
    _stopAtSeconds = item.TrimEndSeconds;
    _playSelection = true;
    _preview.Play();
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private async Task PreviewResultAsync()
  {
    var item = SelectedItem;
    if (item is null)
    {
      return;
    }

    try
    {
      StatusText = "Рендер результата…";
      _preview.Unload();
      var (ffmpeg, ffprobe) = await EnsureFfmpegAsync(showUiProgress: true, CancellationToken.None);

      var options = BuildOptions() with
      {
        OutputDirectory = Path.Combine(
          Path.GetTempPath(), "GoldsrcSoundConverter", "preview", "result"),
        AsciiNames = false,
        LowercaseNames = false,
        CollisionPolicy = CollisionPolicy.Overwrite,
        Parallelism = 1,
      };
      Directory.CreateDirectory(options.OutputDirectory);

      var job = new ConversionPlanner().Plan(
        new[]
        {
          new ConversionJob(
            Guid.NewGuid(),
            item.SourcePath,
            null,
            item.HasTrim ? TimeSpan.FromSeconds(item.TrimStartSeconds) : null,
            item.HasTrim ? TimeSpan.FromSeconds(item.TrimEndSeconds) : null),
        },
        options)[0];

      var outcome = await new AudioConverter(ffmpeg, ffprobe)
        .ConvertAsync(job, options, item.Info, null, AppendLog)
        .ConfigureAwait(true);

      if (outcome.Success && outcome.OutputPath is not null)
      {
        _preview.Load(outcome.OutputPath);
        _preview.Volume = PlaybackVolume;
        _playSelection = false;
        _stopAtSeconds = null;
        _preview.Play();
        StatusText = "Воспроизведение результата";
      }
      else
      {
        StatusText = "Не удалось создать результат: " + outcome.Error;
      }
    }
    catch (Exception ex)
    {
      StatusText = "Ошибка предпросмотра: " + ex.Message;
      AppendLog(StatusText);
    }
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimStart()
  {
    if (SelectedItem is null)
    {
      return;
    }

    SelectedItem.TrimStartSeconds = Math.Min(PlaybackPositionSeconds, SelectedItem.TrimEndSeconds);
    StatusText = "Начало обрезки: " + TimeText.Format(SelectedItem.TrimStartSeconds);
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void SetTrimEnd()
  {
    if (SelectedItem is null)
    {
      return;
    }

    SelectedItem.TrimEndSeconds = Math.Max(PlaybackPositionSeconds, SelectedItem.TrimStartSeconds);
    StatusText = "Конец обрезки: " + TimeText.Format(SelectedItem.TrimEndSeconds);
  }

  [RelayCommand(CanExecute = nameof(HasSelection))]
  private void ResetTrim()
  {
    if (SelectedItem is null)
    {
      return;
    }

    SelectedItem.TrimStartSeconds = 0;
    SelectedItem.TrimEndSeconds = SelectedItem.DurationSeconds;
    StatusText = "Обрезка сброшена";
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
      _ffmpegPath = null;
      _ffprobePath = null;
      UpdateFfmpegStatus();
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

      Process.Start(new ProcessStartInfo
      {
        FileName = OutputDirectory,
        UseShellExecute = true,
      });
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

  private bool AddFile(string path, string? sourceRoot)
  {
    if (Items.Any(i => string.Equals(i.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
    {
      return false;
    }

    var item = new QueueItemViewModel(Items.Count, path, sourceRoot);
    Items.Add(item);
    item.UpdateTargetSize(BuildOptions());
    _ = ProbeItemAsync(item);
    return true;
  }

  private void ReindexItems()
  {
    for (var i = 0; i < Items.Count; i++)
    {
      var replacement = new QueueItemViewModel(i, Items[i].SourcePath, Items[i].SourceRoot)
      {
        Id = Items[i].Id,
        Info = Items[i].Info,
        Waveform = Items[i].Waveform,
        TrimStartSeconds = Items[i].TrimStartSeconds,
        TrimEndSeconds = Items[i].TrimEndSeconds,
        Stage = Items[i].Stage,
        Progress = Items[i].Progress,
        OutputPath = Items[i].OutputPath,
        Error = Items[i].Error,
        TargetSizeText = Items[i].TargetSizeText,
      };

      Items[i] = replacement;
    }
  }

  private async Task ProbeItemAsync(QueueItemViewModel item)
  {
    if (item.Info is not null || !TryGetReadyFfmpeg(out _, out var ffprobe))
    {
      return;
    }

    await _probeGate.WaitAsync().ConfigureAwait(true);
    try
    {
      var info = await AudioProbe.ProbeAsync(ffprobe, item.SourcePath).ConfigureAwait(true);
      item.Info = info;
      item.UpdateTargetSize(BuildOptions());
    }
    catch (Exception ex)
    {
      AppendLog($"Не удалось проанализировать {item.FileName}: {ex.Message}");
    }
    finally
    {
      _probeGate.Release();
    }
  }

  private async Task ProbeMissingItemsAsync(string ffprobePath, CancellationToken cancellationToken)
  {
    var pending = Items.Where(i => i.Info is null).ToArray();
    if (pending.Length == 0)
    {
      return;
    }

    StatusText = "Анализ файлов…";
    await Parallel.ForEachAsync(
      pending,
      new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
      async (item, token) =>
      {
        try
        {
          var info = await AudioProbe.ProbeAsync(ffprobePath, item.SourcePath, token).ConfigureAwait(true);
          item.Info = info;
          item.UpdateTargetSize(BuildOptions());
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          AppendLog($"Не удалось проанализировать {item.FileName}: {ex.Message}");
        }
      }).ConfigureAwait(true);
  }

  private async Task<bool> PreparePreviewAsync(QueueItemViewModel item)
  {
    try
    {
      var (ffmpeg, _) = await EnsureFfmpegAsync(showUiProgress: true, CancellationToken.None);
      var playable = await PreviewDecoder.EnsurePlayableAsync(ffmpeg, item.SourcePath).ConfigureAwait(true);
      _preview.Load(playable);
      _preview.Volume = PlaybackVolume;
      return true;
    }
    catch (Exception ex)
    {
      StatusText = "Ошибка предпросмотра: " + ex.Message;
      AppendLog(StatusText);
      return false;
    }
  }

  private async Task LoadWaveformAsync(QueueItemViewModel item)
  {
    if (item.Waveform is not null || item.IsWaveformLoading)
    {
      return;
    }

    if (!TryGetReadyFfmpeg(out var ffmpeg, out _))
    {
      return;
    }

    item.IsWaveformLoading = true;
    try
    {
      var data = await WaveformExtractor.ExtractAsync(ffmpeg, item.SourcePath).ConfigureAwait(true);
      item.Waveform = data;
      if (item.Info is null)
      {
        item.UpdateTargetSize(BuildOptions());
      }
    }
    catch (Exception ex)
    {
      AppendLog($"Волновая форма недоступна для {item.FileName}: {ex.Message}");
    }
    finally
    {
      item.IsWaveformLoading = false;
    }
  }

  private async Task<(string Ffmpeg, string Ffprobe)> EnsureFfmpegAsync(
    bool showUiProgress,
    CancellationToken cancellationToken)
  {
    if (_ffmpegPath is not null && _ffprobePath is not null)
    {
      return (_ffmpegPath, _ffprobePath);
    }

    await _ffmpegLock.WaitAsync(cancellationToken).ConfigureAwait(true);
    try
    {
      if (_ffmpegPath is not null && _ffprobePath is not null)
      {
        return (_ffmpegPath, _ffprobePath);
      }

      IProgress<BootstrapProgress>? progress = showUiProgress
        ? new Progress<BootstrapProgress>(p =>
        {
          FfmpegStatusText = p.Percent is double percent
            ? $"{p.Stage} {percent:P0}"
            : p.Stage;
          if (p.Percent is double value)
          {
            OverallProgress = value;
          }
        })
        : null;

      var bootstrapper = new FfmpegBootstrapper();
      var result = await bootstrapper
        .EnsureAsync(FfmpegCustomPath, progress, AppendLog, cancellationToken)
        .ConfigureAwait(true);

      _ffmpegPath = result.Ffmpeg;
      _ffprobePath = result.Ffprobe;
      OverallProgress = 0;
      UpdateFfmpegStatus();
      return result;
    }
    finally
    {
      _ffmpegLock.Release();
    }
  }

  private bool TryGetReadyFfmpeg(out string ffmpeg, out string ffprobe)
  {
    if (_ffmpegPath is not null && _ffprobePath is not null)
    {
      ffmpeg = _ffmpegPath;
      ffprobe = _ffprobePath;
      return true;
    }

    if (FfmpegBootstrapper.TryResolveCustom(FfmpegCustomPath, out ffmpeg, out ffprobe)
      || new FfmpegBootstrapper().TryResolve(out ffmpeg, out ffprobe))
    {
      _ffmpegPath = ffmpeg;
      _ffprobePath = ffprobe;
      UpdateFfmpegStatus();
      return true;
    }

    ffmpeg = string.Empty;
    ffprobe = string.Empty;
    return false;
  }

  private void ApplyProgress(ConversionProgress progress)
  {
    var item = Items.FirstOrDefault(i => i.Id == progress.JobId);
    if (item is null)
    {
      return;
    }

    item.Stage = progress.Stage;
    item.Progress = progress.Percent;

    if (progress.Stage == ConversionStage.Converting)
    {
      var finished = Items.Count(i => i.Stage is ConversionStage.Completed
        or ConversionStage.Skipped
        or ConversionStage.Failed);
      OverallProgress = Items.Count == 0
        ? 0
        : Math.Clamp((finished + progress.Percent) / Items.Count, 0, 1);
    }
  }

  private void Tick()
  {
    if (_preview.HasTrack)
    {
      PlaybackPositionSeconds = _preview.Position.TotalSeconds;
      PlaybackPositionText = $"{TimeText.Format(PlaybackPositionSeconds)} / "
        + TimeText.Format(_preview.TotalTime.TotalSeconds);
    }
    else
    {
      PlaybackPositionText = "00:00.000 / 00:00.000";
    }

    if (_playSelection
      && _stopAtSeconds is double stopAt
      && _preview.Position.TotalSeconds >= stopAt)
    {
      _preview.Pause();
      _playSelection = false;
      _stopAtSeconds = null;
    }
  }

  private void OnPlaybackStopped(object? sender, EventArgs e)
  {
    if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
    {
      dispatcher.BeginInvoke(() => OnPlaybackStopped(sender, e));
      return;
    }

    PlaybackPositionSeconds = _preview.Position.TotalSeconds;
  }

  private ConversionOptions BuildOptions()
  {
    return new ConversionOptions
    {
      Format = Format,
      SampleRate = SampleRate,
      Channels = Channels,
      BitDepth = BitDepth,
      Mp3BitrateKbps = Mp3BitrateKbps,
      NormalizePeak = NormalizePeak,
      OutputDirectory = OutputDirectory,
      AsciiNames = AsciiNames,
      LowercaseNames = LowercaseNames,
      PreserveStructure = PreserveStructure,
      CollisionPolicy = CollisionPolicy,
      Parallelism = Parallelism,
    };
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
    foreach (var preset in Cs16Presets.ForFormat(Format))
    {
      Presets.Add(preset);
    }

    SelectedPreset = Cs16Presets.Match(BuildOptions()) ?? Cs16Presets.ById(Cs16Presets.CustomId);
    _applyingPreset = false;
  }

  private void EnsureCustomPreset()
  {
    var match = Cs16Presets.Match(BuildOptions());
    _applyingPreset = true;
    SelectedPreset = match ?? Cs16Presets.ById(Cs16Presets.CustomId);
    _applyingPreset = false;
  }

  private void RecomputeTargetSizes()
  {
    var options = BuildOptions();
    foreach (var item in Items)
    {
      item.UpdateTargetSize(options);
    }
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
    if (_ffmpegPath is not null && _ffprobePath is not null)
    {
      FfmpegStatusText = "FFmpeg: готов";
      FfmpegDownloadText = _ffmpegPath;
      return;
    }

    FfmpegStatusText = "FFmpeg: будет загружен при первой конвертации";
    FfmpegDownloadText = "Автозагрузка (~80 МБ) при первом запуске конвертации "
      + "или укажите свой ffmpeg.exe.";
  }

  private void AppendLog(string message)
  {
    if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
    {
      dispatcher.BeginInvoke(() => AppendLog(message));
      return;
    }

    LogEntries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    while (LogEntries.Count > 500)
    {
      LogEntries.RemoveAt(0);
    }
  }

  partial void OnSelectedItemChanged(QueueItemViewModel? value)
  {
    OnPropertyChanged(nameof(HasSelection));
    _preview.Stop();
    _playSelection = false;
    _stopAtSeconds = null;
    PlaybackPositionSeconds = 0;
    EditorTitle = value is null ? "Выберите файл в очереди" : value.FileName;

    if (value is not null)
    {
      _ = LoadWaveformAsync(value);
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
    _ffmpegPath = null;
    _ffprobePath = null;
    UpdateFfmpegStatus();
  }

  partial void OnPlaybackVolumeChanged(double value)
  {
    _preview.Volume = value;
  }
}
