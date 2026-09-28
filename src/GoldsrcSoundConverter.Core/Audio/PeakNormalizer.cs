using System.Globalization;
using System.Text.RegularExpressions;
using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Core.Audio;

public static class PeakNormalizer
{
  public const double DefaultTargetDb = -0.3;
  public const double DefaultMaxGainDb = 24.0;
  public const double SilenceThresholdDb = -70.0;

  private static readonly Regex MaxVolumeRegex = new(
    @"max_volume:\s*(-?\d+(?:\.\d+)?)\s*dB",
    RegexOptions.Compiled | RegexOptions.CultureInvariant);

  public static async Task<double?> MeasureGainAsync(
    IProcessRunner processRunner,
    string ffmpegPath,
    string inputPath,
    TimeSpan trimStart,
    TimeSpan outputDuration,
    double targetDb,
    double maxGainDb,
    CancellationToken cancellationToken = default)
  {
    var arguments = FfmpegArguments.BuildMeasureVolume(inputPath, trimStart, outputDuration);
    var result = await processRunner
      .RunAsync(ffmpegPath, arguments, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    if (!result.Success)
    {
      throw new FfmpegException("Не удалось измерить громкость файла.", result.StandardError);
    }

    var match = MaxVolumeRegex.Match(result.StandardError);
    if (!match.Success)
    {
      return null;
    }

    var maxVolumeDb = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    return ComputeGainDb(maxVolumeDb, targetDb, maxGainDb);
  }

  public static double? ComputeGainDb(double maxVolumeDb, double targetDb, double maxGainDb)
  {
    if (maxVolumeDb <= SilenceThresholdDb)
    {
      return null;
    }

    var gainDb = targetDb - maxVolumeDb;
    if (gainDb <= 0.01)
    {
      return null;
    }

    return Math.Min(gainDb, maxGainDb);
  }
}
