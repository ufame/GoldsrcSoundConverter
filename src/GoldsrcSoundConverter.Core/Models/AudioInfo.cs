namespace GoldsrcSoundConverter.Core.Models;

public sealed record AudioInfo(
  string FilePath,
  string FormatName,
  string CodecName,
  TimeSpan Duration,
  int SampleRate,
  int Channels,
  long BitRate,
  long FileSize)
{
  public string ChannelsText => Channels switch
  {
    1 => "mono",
    2 => "stereo",
    _ => $"{Channels} ch",
  };

  public string BitRateText => BitRate > 0 ? $"{BitRate / 1000} kbps" : "—";
}
