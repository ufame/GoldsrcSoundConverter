using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Settings;

public sealed class AppSettings
{
  public string? FfmpegCustomPath { get; set; }
  public string OutputDirectory { get; set; } = string.Empty;
  public OutputAudioFormat Format { get; set; } = OutputAudioFormat.Wav;
  public string PresetId { get; set; } = "wav-sound";
  public int SampleRate { get; set; } = 22050;
  public TargetChannels Channels { get; set; } = TargetChannels.Mono;
  public TargetBitDepth BitDepth { get; set; } = TargetBitDepth.Sixteen;
  public int Mp3BitrateKbps { get; set; } = 128;
  public bool NormalizePeak { get; set; }
  public bool AsciiNames { get; set; } = true;
  public bool LowercaseNames { get; set; } = true;
  public bool PreserveStructure { get; set; }
  public CollisionPolicy CollisionPolicy { get; set; } = CollisionPolicy.Rename;
  public int Parallelism { get; set; } = 4;
  public bool DarkTheme { get; set; } = true;
  public double WindowWidth { get; set; } = 1360;
  public double WindowHeight { get; set; } = 860;
}
