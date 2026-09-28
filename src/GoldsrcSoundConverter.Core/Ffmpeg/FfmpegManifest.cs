using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed record FfmpegManifest(
  [property: JsonPropertyName("version")] string Version,
  [property: JsonPropertyName("url")] string Url,
  [property: JsonPropertyName("sha256")] string Sha256);

public static class FfmpegManifestLoader
{
  public const string ResourceName = "GoldsrcSoundConverter.FfmpegManifest.json";

  private static readonly JsonSerializerOptions Options = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  public static FfmpegManifest Load()
  {
    using var stream = typeof(FfmpegManifestLoader).Assembly.GetManifestResourceStream(ResourceName)
      ?? throw new InvalidOperationException($"Ресурс {ResourceName} не найден в сборке.");

    var manifest = JsonSerializer.Deserialize<FfmpegManifest>(stream, Options)
      ?? throw new InvalidOperationException("Манифест FFmpeg пуст.");

    if (string.IsNullOrWhiteSpace(manifest.Version)
      || string.IsNullOrWhiteSpace(manifest.Url)
      || manifest.Sha256.Length != 64)
    {
      throw new InvalidOperationException("Манифест FFmpeg содержит некорректные данные.");
    }

    return manifest;
  }
}
