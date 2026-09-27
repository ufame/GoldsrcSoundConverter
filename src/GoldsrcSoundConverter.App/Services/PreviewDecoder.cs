using System.IO;
using System.Security.Cryptography;
using System.Text;
using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.App.Services;

public static class PreviewDecoder
{
  public static bool CanPlayDirectly(string path)
  {
    return Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3" or ".aif" or ".aiff";
  }

  public static async Task<string> EnsurePlayableAsync(
    string ffmpegPath,
    string sourcePath,
    CancellationToken cancellationToken = default)
  {
    if (CanPlayDirectly(sourcePath))
    {
      return sourcePath;
    }

    var directory = Path.Combine(Path.GetTempPath(), "GoldsrcSoundConverter", "preview");
    Directory.CreateDirectory(directory);

    var key = Convert.ToHexString(
      SHA1.HashData(Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant())))[..16];
    var target = Path.Combine(directory, key + ".wav");

    if (File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(sourcePath))
    {
      return target;
    }

    var arguments = FfmpegArguments.BuildDecodeToWav(sourcePath, target, 44100, 2);
    var result = await FfmpegRunner
      .RunAsync(ffmpegPath, arguments, cancellationToken: cancellationToken)
      .ConfigureAwait(false);

    if (!result.Success)
    {
      throw new FfmpegException("Не удалось подготовить предпросмотр.", result.StandardError);
    }

    return target;
  }
}
