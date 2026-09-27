namespace GoldsrcSoundConverter.Core.Models;

public sealed class ConversionOptions
{
  public OutputAudioFormat Format { get; set; } = OutputAudioFormat.Wav;
  public int SampleRate { get; set; } = 22050;
  public TargetChannels Channels { get; set; } = TargetChannels.Mono;
  public TargetBitDepth BitDepth { get; set; } = TargetBitDepth.Sixteen;
  public int Mp3BitrateKbps { get; set; } = 128;
  public bool NormalizePeak { get; set; }
  public double NormalizeTargetDb { get; set; } = -0.3;
  public double MaxNormalizeGainDb { get; set; } = 24.0;
  public string OutputDirectory { get; set; } = string.Empty;
  public bool AsciiNames { get; set; } = true;
  public bool LowercaseNames { get; set; } = true;
  public bool PreserveStructure { get; set; }
  public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;
  public int Parallelism { get; set; } = 4;

  public string Extension => Format == OutputAudioFormat.Wav ? ".wav" : ".mp3";

  public ConversionOptions Clone()
  {
    return (ConversionOptions)MemberwiseClone();
  }
}
