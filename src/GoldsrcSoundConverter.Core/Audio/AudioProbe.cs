using System.Globalization;
using System.Text.Json;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Audio;

public static class AudioProbe
{
  public static async Task<AudioInfo> ProbeAsync(
    string ffprobePath,
    string filePath,
    CancellationToken cancellationToken = default)
  {
    var arguments = new[]
    {
      "-hide_banner",
      "-v", "error",
      "-select_streams", "a:0",
      "-show_entries", "stream=codec_name,sample_rate,channels,bit_rate,duration",
      "-show_entries", "format=format_name,duration,bit_rate",
      "-of", "json",
      filePath,
    };

    var result = await FfmpegRunner
      .RunAsync(ffprobePath, arguments, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    if (!result.Success)
    {
      throw new FfmpegException($"ffprobe: {filePath}", result.StandardError);
    }

    using var document = JsonDocument.Parse(result.StandardOutput);
    var root = document.RootElement;

    if (!root.TryGetProperty("streams", out var streams) || streams.GetArrayLength() == 0)
    {
      throw new InvalidOperationException("В файле не найдена аудиодорожка.");
    }

    var stream = streams[0];
    root.TryGetProperty("format", out var format);

    var duration = ReadDuration(stream);
    if (duration <= TimeSpan.Zero && format.ValueKind == JsonValueKind.Object)
    {
      duration = ReadDuration(format);
    }

    var bitRate = ReadLong(stream, "bit_rate");
    if (bitRate <= 0 && format.ValueKind == JsonValueKind.Object)
    {
      bitRate = ReadLong(format, "bit_rate");
    }

    long fileSize = 0;
    try
    {
      fileSize = new FileInfo(filePath).Length;
    }
    catch
    {
    }

    return new AudioInfo(
      filePath,
      ReadString(format, "format_name") ?? "unknown",
      ReadString(stream, "codec_name") ?? "unknown",
      duration,
      (int)ReadLong(stream, "sample_rate"),
      (int)ReadLong(stream, "channels"),
      bitRate,
      fileSize);
  }

  private static string? ReadString(JsonElement element, string propertyName)
  {
    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
    {
      return null;
    }

    return property.ValueKind switch
    {
      JsonValueKind.String => property.GetString(),
      JsonValueKind.Number => property.GetRawText(),
      _ => null,
    };
  }

  private static long ReadLong(JsonElement element, string propertyName)
  {
    var text = ReadString(element, propertyName);
    return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
  }

  private static TimeSpan ReadDuration(JsonElement element)
  {
    var text = ReadString(element, "duration");
    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
      ? TimeSpan.FromSeconds(seconds)
      : TimeSpan.Zero;
  }
}
