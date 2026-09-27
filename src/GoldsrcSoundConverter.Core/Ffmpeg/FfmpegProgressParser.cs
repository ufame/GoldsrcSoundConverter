using System.Globalization;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed class FfmpegProgressParser
{
  private readonly double _totalSeconds;
  private double _lastFraction;

  public FfmpegProgressParser(TimeSpan totalDuration)
  {
    _totalSeconds = totalDuration.TotalSeconds;
  }

  public bool IsCompleted { get; private set; }

  public bool TryParse(string line, out double fraction)
  {
    fraction = _lastFraction;

    if (line.StartsWith("out_time_us=", StringComparison.Ordinal)
      || line.StartsWith("out_time_ms=", StringComparison.Ordinal))
    {
      var value = line[(line.IndexOf('=') + 1)..];
      if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
      {
        var seconds = microseconds / 1_000_000.0;
        fraction = _totalSeconds > 0 ? Math.Clamp(seconds / _totalSeconds, 0, 0.999) : 0;
        _lastFraction = fraction;
        return true;
      }

      return false;
    }

    if (line.StartsWith("progress=", StringComparison.Ordinal))
    {
      if (line.EndsWith("end", StringComparison.Ordinal))
      {
        IsCompleted = true;
        fraction = 1;
        _lastFraction = 1;
      }
      else
      {
        fraction = _lastFraction;
      }

      return true;
    }

    return false;
  }
}
