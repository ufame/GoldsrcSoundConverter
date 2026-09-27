using System.Globalization;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public static class FfmpegArguments
{
  public static IReadOnlyList<string> BuildConvert(
    string inputPath,
    string outputPath,
    ConversionOptions options,
    double? normalizeGainDb,
    TimeSpan trimStart,
    TimeSpan outputDuration)
  {
    var args = new List<string>
    {
      "-hide_banner",
      "-nostdin",
      "-y",
      "-loglevel", "error",
      "-progress", "pipe:1",
    };

    if (trimStart > TimeSpan.Zero)
    {
      args.Add("-ss");
      args.Add(Seconds(trimStart.TotalSeconds));
    }

    args.Add("-i");
    args.Add(inputPath);

    args.Add("-vn");
    args.Add("-map");
    args.Add("0:a:0");

    if (outputDuration > TimeSpan.Zero)
    {
      args.Add("-t");
      args.Add(Seconds(outputDuration.TotalSeconds));
    }

    if (normalizeGainDb is > 0)
    {
      args.Add("-af");
      args.Add(FormattableString.Invariant($"volume={normalizeGainDb.Value.ToString("+0.00;-0.00;0", CultureInfo.InvariantCulture)}dB"));
    }

    args.Add("-ar");
    args.Add(options.SampleRate.ToString(CultureInfo.InvariantCulture));
    args.Add("-ac");
    args.Add(((int)options.Channels).ToString(CultureInfo.InvariantCulture));

    if (options.Format == OutputAudioFormat.Wav)
    {
      args.Add("-c:a");
      args.Add(options.BitDepth == TargetBitDepth.Eight ? "pcm_u8" : "pcm_s16le");
    }
    else
    {
      args.Add("-c:a");
      args.Add("libmp3lame");
      args.Add("-b:a");
      args.Add(options.Mp3BitrateKbps.ToString(CultureInfo.InvariantCulture) + "k");
      args.Add("-write_xing");
      args.Add("1");
      args.Add("-id3v2_version");
      args.Add("0");
      args.Add("-write_id3v1");
      args.Add("0");
    }

    args.Add("-map_metadata");
    args.Add("-1");
    args.Add(outputPath);
    return args;
  }

  public static IReadOnlyList<string> BuildMeasureVolume(
    string inputPath,
    TimeSpan trimStart,
    TimeSpan outputDuration)
  {
    var args = new List<string>
    {
      "-hide_banner",
      "-nostdin",
      "-loglevel", "info",
    };

    if (trimStart > TimeSpan.Zero)
    {
      args.Add("-ss");
      args.Add(Seconds(trimStart.TotalSeconds));
    }

    args.Add("-i");
    args.Add(inputPath);
    args.Add("-vn");
    args.Add("-map");
    args.Add("0:a:0");

    if (outputDuration > TimeSpan.Zero)
    {
      args.Add("-t");
      args.Add(Seconds(outputDuration.TotalSeconds));
    }

    args.Add("-af");
    args.Add("volumedetect");
    args.Add("-f");
    args.Add("null");
    args.Add("-");
    return args;
  }

  public static IReadOnlyList<string> BuildRawWaveform(string inputPath, int sampleRate)
  {
    return new List<string>
    {
      "-hide_banner",
      "-nostdin",
      "-loglevel", "error",
      "-i", inputPath,
      "-vn",
      "-map", "0:a:0",
      "-ac", "1",
      "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
      "-f", "s16le",
      "-",
    };
  }

  public static IReadOnlyList<string> BuildDecodeToWav(string inputPath, string outputPath, int sampleRate, int channels)
  {
    return new List<string>
    {
      "-hide_banner",
      "-nostdin",
      "-y",
      "-loglevel", "error",
      "-i", inputPath,
      "-vn",
      "-map", "0:a:0",
      "-ar", sampleRate.ToString(CultureInfo.InvariantCulture),
      "-ac", channels.ToString(CultureInfo.InvariantCulture),
      "-c:a", "pcm_s16le",
      "-map_metadata", "-1",
      outputPath,
    };
  }

  private static string Seconds(double seconds)
  {
    return seconds.ToString("0.######", CultureInfo.InvariantCulture);
  }
}
