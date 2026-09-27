namespace GoldsrcSoundConverter.Core.Models;

public sealed record Cs16Preset(
  string Id,
  string Name,
  OutputAudioFormat Format,
  int SampleRate,
  TargetChannels Channels,
  TargetBitDepth BitDepth,
  int Mp3BitrateKbps)
{
  public bool IsCustom => Id == Cs16Presets.CustomId;

  public string Description => Format == OutputAudioFormat.Mp3
    ? $"{SampleRate} Hz, CBR {Mp3BitrateKbps} kbps, {ChannelsText}"
    : $"{SampleRate} Hz, {(int)BitDepth}-bit, {ChannelsText}";

  private string ChannelsText => Channels == TargetChannels.Mono ? "mono" : "stereo";
}

public static class Cs16Presets
{
  public const string CustomId = "custom";

  public static IReadOnlyList<Cs16Preset> All { get; } = new Cs16Preset[]
  {
    new("wav-sound", "WAV — звуки (22050 Hz, 16-bit, mono)", OutputAudioFormat.Wav, 22050, TargetChannels.Mono, TargetBitDepth.Sixteen, 0),
    new("wav-voice", "WAV — голос/интерфейс (11025 Hz, 16-bit, mono)", OutputAudioFormat.Wav, 11025, TargetChannels.Mono, TargetBitDepth.Sixteen, 0),
    new("wav-lite", "WAV — экономичный (22050 Hz, 8-bit, mono)", OutputAudioFormat.Wav, 22050, TargetChannels.Mono, TargetBitDepth.Eight, 0),
    new("wav-stereo", "WAV — музыка/2D (44100 Hz, 16-bit, stereo)", OutputAudioFormat.Wav, 44100, TargetChannels.Stereo, TargetBitDepth.Sixteen, 0),
    new("mp3-music", "MP3 — музыка (44100 Hz, CBR 128k, stereo)", OutputAudioFormat.Mp3, 44100, TargetChannels.Stereo, TargetBitDepth.Sixteen, 128),
    new("mp3-music-hq", "MP3 — музыка HQ (44100 Hz, CBR 192k, stereo)", OutputAudioFormat.Mp3, 44100, TargetChannels.Stereo, TargetBitDepth.Sixteen, 192),
    new("mp3-voice", "MP3 — голос/радио (22050 Hz, CBR 64k, mono)", OutputAudioFormat.Mp3, 22050, TargetChannels.Mono, TargetBitDepth.Sixteen, 64),
    new(CustomId, "Свои параметры", OutputAudioFormat.Wav, 22050, TargetChannels.Mono, TargetBitDepth.Sixteen, 0),
  };

  public static Cs16Preset DefaultFor(OutputAudioFormat format)
  {
    return format == OutputAudioFormat.Wav ? All[0] : All[4];
  }

  public static Cs16Preset? ById(string? id)
  {
    return id is null ? null : All.FirstOrDefault(p => p.Id == id);
  }

  public static IReadOnlyList<Cs16Preset> ForFormat(OutputAudioFormat format)
  {
    return All.Where(p => p.IsCustom || p.Format == format).ToArray();
  }

  public static Cs16Preset? Match(ConversionOptions options)
  {
    return All.FirstOrDefault(p => !p.IsCustom
      && p.Format == options.Format
      && p.SampleRate == options.SampleRate
      && p.Channels == options.Channels
      && p.BitDepth == options.BitDepth
      && (options.Format != OutputAudioFormat.Mp3 || p.Mp3BitrateKbps == options.Mp3BitrateKbps));
  }
}
