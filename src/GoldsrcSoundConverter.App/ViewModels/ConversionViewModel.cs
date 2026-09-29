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
  private readonly IQueueConversionPresenter _presenter;
  private readonly ILogBuffer _log;
  private readonly OutputDirectoryProvider _outputDirectory;

  public ConversionViewModel(
    QueueViewModel queue,
    IFilePicker filePicker,
    IConversionService conversionService,
    IConversionRequestFactory requestFactory,
    IConversionRunController runController,
    IQueueConversionPresenter presenter,
    ILogBuffer log,
    OutputDirectoryProvider outputDirectory)
  {
    _queue = queue;
    _filePicker = filePicker;
    _conversionService = conversionService;
    _requestFactory = requestFactory;
    _runController = runController;
    _presenter = presenter;
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
    _runController.Dispose();
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
