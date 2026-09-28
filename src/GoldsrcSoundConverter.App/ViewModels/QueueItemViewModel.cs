using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.ViewModels;

public partial class QueueItemViewModel : ObservableObject
{
  public QueueItemViewModel(string sourcePath, string? sourceRoot)
  {
    SourcePath = sourcePath;
    SourceRoot = sourceRoot;
  }

  public Guid Id { get; init; } = Guid.NewGuid();

  public string SourcePath { get; }

  public string? SourceRoot { get; }

  public string FileName => Path.GetFileName(SourcePath);

  [ObservableProperty]
  private AudioInfo? _info;

  [ObservableProperty]
  private ConversionStage _stage = ConversionStage.Pending;

  [ObservableProperty]
  private double _progress;

  [ObservableProperty]
  private string? _error;

  [ObservableProperty]
  private string? _outputPath;

  [ObservableProperty]
  private double _trimStartSeconds;

  [ObservableProperty]
  private double _trimEndSeconds;

  [ObservableProperty]
  private WaveformData? _waveform;

  [ObservableProperty]
  private bool _isWaveformLoading;

  [ObservableProperty]
  private string _targetSizeText = "—";

  public double DurationSeconds => Info?.Duration.TotalSeconds
    ?? Waveform?.Duration.TotalSeconds
    ?? 0;

  public bool HasTrim => DurationSeconds > 0
    && (TrimStartSeconds > 0.001 || TrimEndSeconds < DurationSeconds - 0.001);

  public string DurationText => DurationSeconds > 0 ? TimeText.Format(DurationSeconds) : "—";

  public string InfoText => Info is null
    ? "—"
    : $"{Info.CodecName} · {Info.SampleRate} Гц · {Info.ChannelsText} · {Info.BitRateText}";

  public string TrimText => HasTrim
    ? $"{TimeText.Format(TrimStartSeconds)}–{TimeText.Format(TrimEndSeconds)}"
    : "—";

  public string ProgressText => Stage == ConversionStage.Converting
    ? Progress.ToString("P0", CultureInfo.InvariantCulture)
    : string.Empty;

  public string StatusText => Stage switch
  {
    ConversionStage.Pending => "В очереди",
    ConversionStage.Probing => "Анализ…",
    ConversionStage.Normalizing => "Нормализация…",
    ConversionStage.Converting => "Конвертация…",
    ConversionStage.Completed => "Готово",
    ConversionStage.Skipped => "Пропущен",
    ConversionStage.Failed => string.IsNullOrWhiteSpace(Error) ? "Ошибка" : "Ошибка: " + Error,
    _ => "—",
  };

  public void UpdateTargetSize(ConversionOptions options)
  {
    var duration = DurationSeconds;
    if (duration <= 0)
    {
      TargetSizeText = "—";
      return;
    }

    var bytes = options.Format == OutputAudioFormat.Mp3
      ? duration * options.Mp3BitrateKbps * 1000.0 / 8.0
      : duration * options.SampleRate * (int)options.Channels * ((int)options.BitDepth / 8.0);

    TargetSizeText = FormatSize(bytes);
  }

  public static string FormatSize(double bytes)
  {
    const double megabyte = 1024 * 1024;
    return bytes >= megabyte
      ? (bytes / megabyte).ToString("0.0", CultureInfo.InvariantCulture) + " МБ"
      : (bytes / 1024).ToString("0", CultureInfo.InvariantCulture) + " КБ";
  }

  partial void OnInfoChanged(AudioInfo? value)
  {
    if (value is not null && TrimEndSeconds <= 0)
    {
      TrimEndSeconds = value.Duration.TotalSeconds;
    }

    RaiseDurationDependents();
  }

  partial void OnWaveformChanged(WaveformData? value)
  {
    if (value is not null && TrimEndSeconds <= 0)
    {
      TrimEndSeconds = value.Duration.TotalSeconds;
    }

    RaiseDurationDependents();
  }

  partial void OnTrimStartSecondsChanged(double value)
  {
    OnPropertyChanged(nameof(TrimText));
    OnPropertyChanged(nameof(HasTrim));
  }

  partial void OnTrimEndSecondsChanged(double value)
  {
    OnPropertyChanged(nameof(TrimText));
    OnPropertyChanged(nameof(HasTrim));
  }

  partial void OnStageChanged(ConversionStage value)
  {
    OnPropertyChanged(nameof(StatusText));
    OnPropertyChanged(nameof(ProgressText));
  }

  partial void OnProgressChanged(double value)
  {
    OnPropertyChanged(nameof(ProgressText));
  }

  partial void OnErrorChanged(string? value)
  {
    OnPropertyChanged(nameof(StatusText));
  }

  private void RaiseDurationDependents()
  {
    OnPropertyChanged(nameof(DurationSeconds));
    OnPropertyChanged(nameof(DurationText));
    OnPropertyChanged(nameof(HasTrim));
    OnPropertyChanged(nameof(TrimText));
  }
}
