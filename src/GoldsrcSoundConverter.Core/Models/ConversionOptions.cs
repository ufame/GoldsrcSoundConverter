namespace GoldsrcSoundConverter.Core.Models;

public sealed record ConversionOptions
{
  public const int MinParallelism = 1;
  public const int MaxParallelism = 16;
  public const int MinMp3BitrateKbps = 32;
  public const int MaxMp3BitrateKbps = 320;

  public OutputAudioFormat Format { get; init; } = OutputAudioFormat.Wav;
  public int SampleRate { get; init; } = 22050;
  public TargetChannels Channels { get; init; } = TargetChannels.Mono;
  public TargetBitDepth BitDepth { get; init; } = TargetBitDepth.Sixteen;
  public int Mp3BitrateKbps { get; init; } = 128;
  public bool NormalizePeak { get; init; }
  public double NormalizeTargetDb { get; init; } = -0.3;
  public double MaxNormalizeGainDb { get; init; } = 24.0;
  public string OutputDirectory { get; init; } = string.Empty;
  public bool AsciiNames { get; init; } = true;
  public bool LowercaseNames { get; init; } = true;
  public bool PreserveStructure { get; init; }
  public CollisionPolicy CollisionPolicy { get; init; } = CollisionPolicy.Rename;
  public int Parallelism { get; init; } = 4;

  public string Extension => Format == OutputAudioFormat.Wav ? ".wav" : ".mp3";

  public ConversionOptions Validate()
  {
    if (SampleRate <= 0)
    {
      throw new ArgumentOutOfRangeException(
        nameof(SampleRate),
        SampleRate,
        "Частота дискретизации должна быть положительной.");
    }

    if (Parallelism < MinParallelism || Parallelism > MaxParallelism)
    {
      throw new ArgumentOutOfRangeException(
        nameof(Parallelism),
        Parallelism,
        $"Количество параллельных задач должно быть от {MinParallelism} до {MaxParallelism}.");
    }

    if (Format == OutputAudioFormat.Mp3
      && (Mp3BitrateKbps < MinMp3BitrateKbps || Mp3BitrateKbps > MaxMp3BitrateKbps))
    {
      throw new ArgumentOutOfRangeException(
        nameof(Mp3BitrateKbps),
        Mp3BitrateKbps,
        $"Битрейт MP3 должен быть от {MinMp3BitrateKbps} до {MaxMp3BitrateKbps} kbps.");
    }

    if (NormalizeTargetDb > 0 || NormalizeTargetDb < -60)
    {
      throw new ArgumentOutOfRangeException(
        nameof(NormalizeTargetDb),
        NormalizeTargetDb,
        "Целевой уровень нормализации должен быть от -60 до 0 dB.");
    }

    if (MaxNormalizeGainDb < 0 || MaxNormalizeGainDb > 96)
    {
      throw new ArgumentOutOfRangeException(
        nameof(MaxNormalizeGainDb),
        MaxNormalizeGainDb,
        "Максимальное усиление нормализации должно быть от 0 до 96 dB.");
    }

    return this;
  }
}
