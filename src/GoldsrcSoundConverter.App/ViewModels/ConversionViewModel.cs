using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed partial class ConversionViewModel : ObservableObject, IDisposable
{
  private readonly QueueViewModel _queue;
  private readonly IFilePicker _filePicker;
  private readonly IConversionService _conversionService;
  private readonly IConversionRequestFactory _requestFactory;
  private readonly IConversionRunController _runController;
  private readonly ILogBuffer _log;
  private readonly OutputDirectoryProvider _outputDirectory;

  private QueueItemViewModel[] _runItems = Array.Empty<QueueItemViewModel>();
  private Dictionary<Guid, QueueItemViewModel> _runItemsById = new();

  public ConversionViewModel(
    QueueViewModel queue,
    IFilePicker filePicker,
    IConversionService conversionService,
    IConversionRequestFactory requestFactory,
    IConversionRunController runController,
    ILogBuffer log,
    OutputDirectoryProvider outputDirectory)
  {
    _queue = queue;
    _filePicker = filePicker;
    _conversionService = conversionService;
    _requestFactory = requestFactory;
    _runController = runController;
    _log = log;
    _outputDirectory = outputDirectory;

    _queue.ItemsChanged += OnQueueItemsChanged;
    _runController.BusyChanged += OnRunControllerBusyChanged;

    UpdateFfmpegStatus();
  }

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

  [ObservableProperty]
  private OutputAudioFormat _format = OutputAudioFormat.Wav;

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
  private double _overallProgress;

  [ObservableProperty]
  [NotifyCanExecuteChangedFor(nameof(StartCommand))]
  [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
  private bool _isBusy;

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

  public bool CanStart => !IsBusy && _queue.Items.Count > 0;

  public event EventHandler<string>? StatusChanged;

  public event EventHandler? FormatChanged;

  public event EventHandler? OptionsChanged;

  public event EventHandler? RunFinished;

  public ConversionOptions BuildOptions()
  {
    return _requestFactory.CreateOptions(SnapshotSettings());
  }

  public ConversionSettings SnapshotSettings()
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
      _outputDirectory.Value);
  }

  public ConversionOptions CreatePreviewOptions(string previewDirectory)
  {
    return _requestFactory.CreatePreviewOptions(SnapshotSettings(), previewDirectory);
  }

  public void RecalculateTargetSizes()
  {
    _queue.RecalculateTargetSizes(BuildOptions());
  }

  public void Dispose()
  {
    _queue.ItemsChanged -= OnQueueItemsChanged;
    _runController.BusyChanged -= OnRunControllerBusyChanged;
    GC.SuppressFinalize(this);
  }

  [RelayCommand(CanExecute = nameof(CanStart))]
  private async Task StartAsync()
  {
    if (_queue.Items.Count == 0)
    {
      return;
    }

    if (string.IsNullOrWhiteSpace(_outputDirectory.Value))
    {
      StatusChanged?.Invoke(this, "Укажите папку результатов");
      return;
    }

    try
    {
      Directory.CreateDirectory(_outputDirectory.Value);
    }
    catch (Exception ex)
    {
      StatusChanged?.Invoke(this, "Не удалось создать папку результатов: " + ex.Message);
      return;
    }

    OverallProgress = 0;
    StatusChanged?.Invoke(this, "Подготовка FFmpeg…");

    var options = BuildOptions();

    StatusChanged?.Invoke(this, $"Конвертация: {_queue.Items.Count} файл(ов)…");

    try
    {
      var workItems = BeginRun();

      var result = await _runController
        .RunAsync(
          workItems,
          options,
          CreateProgressHandler(value => OverallProgress = value),
          CreateProbeHandler(options),
          new Progress<BootstrapProgress>(ApplyBootstrapProgress))
        .ConfigureAwait(true);

      Complete(result);
      UpdateFfmpegStatus();

      string status;
      if (result.Summary.Cancelled)
      {
        status = "Конвертация отменена";
      }
      else
      {
        OverallProgress = 1;
        status = $"Готово: успешно {result.Summary.Completed}, "
          + $"ошибок {result.Summary.Failed}, пропущено {result.Summary.Skipped}";
      }

      StatusChanged?.Invoke(this, status);
      _log.Add(status);
    }
    catch (Exception ex)
    {
      var status = "Ошибка: " + ex.Message;
      StatusChanged?.Invoke(this, status);
      _log.Add(status);
    }
    finally
    {
      RunFinished?.Invoke(this, EventArgs.Empty);
    }
  }

  [RelayCommand(CanExecute = nameof(IsBusy))]
  private void Cancel()
  {
    StatusChanged?.Invoke(this, "Отмена…");
    _runController.Cancel();
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

  internal IReadOnlyList<ConversionWorkItem> BeginRun()
  {
    _runItems = _queue.Items.ToArray();
    _runItemsById = _runItems.ToDictionary(item => item.Id);

    foreach (var item in _runItems)
    {
      item.Progress = 0;
      item.Error = null;
      item.OutputPath = null;
      item.Stage = ConversionStage.Pending;
    }

    return _runItems.Select(item => item.ToWorkItem()).ToArray();
  }

  internal IProgress<ConversionProgress> CreateProgressHandler(Action<double>? onOverallProgress = null)
  {
    return new ActionProgress<ConversionProgress>(
      progress => ApplyProgress(progress, onOverallProgress));
  }

  internal IProgress<ProbeResult> CreateProbeHandler(ConversionOptions options)
  {
    return new ActionProgress<ProbeResult>(probe => ApplyProbe(probe, options));
  }

  internal void Complete(ConversionRunResult result)
  {
    if (result.Summary.Cancelled)
    {
      ResetInFlight();
      return;
    }

    ApplyOutcomes(result.Outcomes);
  }

  private void ApplyProgress(ConversionProgress progress, Action<double>? onOverallProgress)
  {
    if (!_runItemsById.TryGetValue(progress.JobId, out var item))
    {
      return;
    }

    item.Stage = progress.Stage;
    item.Progress = progress.Percent;

    if (progress.Stage == ConversionStage.Converting && onOverallProgress is not null)
    {
      var finished = _runItems.Count(i => i.Stage is ConversionStage.Completed
        or ConversionStage.Skipped
        or ConversionStage.Failed);
      onOverallProgress(_runItems.Length == 0
        ? 0
        : Math.Clamp((finished + progress.Percent) / _runItems.Length, 0, 1));
    }
  }

  private void ApplyProbe(ProbeResult probe, ConversionOptions options)
  {
    if (!_runItemsById.TryGetValue(probe.Id, out var item) || item.Info is not null)
    {
      return;
    }

    item.Info = probe.Info;
    item.UpdateTargetSize(options);
  }

  private void ApplyOutcomes(IReadOnlyList<ConversionOutcome> outcomes)
  {
    foreach (var outcome in outcomes)
    {
      if (!_runItemsById.TryGetValue(outcome.JobId, out var item))
      {
        continue;
      }

      if (outcome.Success && outcome.Skipped)
      {
        item.Stage = ConversionStage.Skipped;
      }
      else if (outcome.Success)
      {
        item.Stage = ConversionStage.Completed;
        item.Progress = 1;
        item.OutputPath = outcome.OutputPath;
      }
      else
      {
        item.Stage = ConversionStage.Failed;
        item.Error = outcome.Error;
      }
    }
  }

  private void ResetInFlight()
  {
    foreach (var item in _runItems.Where(i => i.Stage is ConversionStage.Pending
      or ConversionStage.Probing
      or ConversionStage.Normalizing
      or ConversionStage.Converting))
    {
      item.Stage = ConversionStage.Pending;
      item.Progress = 0;
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

  private void SetFormat(OutputAudioFormat format)
  {
    if (Format == format)
    {
      return;
    }

    Format = format;
  }

  private void OnQueueItemsChanged(object? sender, EventArgs e)
  {
    StartCommand.NotifyCanExecuteChanged();
  }

  private void OnRunControllerBusyChanged(object? sender, EventArgs e)
  {
    IsBusy = _runController.IsBusy;
  }

  partial void OnIsBusyChanged(bool value)
  {
    _queue.IsLocked = value;
  }

  partial void OnFormatChanged(OutputAudioFormat value)
  {
    OnPropertyChanged(nameof(IsWavFormat));
    OnPropertyChanged(nameof(IsMp3Format));
    FormatChanged?.Invoke(this, EventArgs.Empty);
    RecalculateTargetSizes();
  }

  partial void OnSampleRateChanged(int value)
  {
    OptionsChanged?.Invoke(this, EventArgs.Empty);
    RecalculateTargetSizes();
  }

  partial void OnChannelsChanged(TargetChannels value)
  {
    OptionsChanged?.Invoke(this, EventArgs.Empty);
    RecalculateTargetSizes();
  }

  partial void OnBitDepthChanged(TargetBitDepth value)
  {
    OptionsChanged?.Invoke(this, EventArgs.Empty);
    RecalculateTargetSizes();
  }

  partial void OnMp3BitrateKbpsChanged(int value)
  {
    OptionsChanged?.Invoke(this, EventArgs.Empty);
    RecalculateTargetSizes();
  }

  partial void OnFfmpegCustomPathChanged(string? value)
  {
    _conversionService.SetCustomFfmpegPath(value);
    UpdateFfmpegStatus();
  }
}
