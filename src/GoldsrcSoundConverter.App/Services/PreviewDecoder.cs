using System.IO;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.App.Services;

public static class PreviewDecoder
{
  public static bool CanPlayDirectly(string path)
  {
    return Path.GetExtension(path).ToLowerInvariant() is ".wav" or ".mp3" or ".aif" or ".aiff";
  }

  public static async Task<string> EnsurePlayableAsync(
    IProcessRunner processRunner,
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

    var key = PreviewCache.ComputeKey(sourcePath);
    var target = Path.Combine(directory, key + ".wav");
    var fingerprintPath = target + ".meta";

    if (PreviewCache.IsValid(sourcePath, target, fingerprintPath))
    {
      return target;
    }

    var tempPath = Path.Combine(directory, $"{key}.{Guid.NewGuid():N}.tmp");
    try
    {
      var arguments = FfmpegArguments.BuildDecodeToWav(sourcePath, tempPath, 44100, 2);
      var result = await processRunner
        .RunAsync(ffmpegPath, arguments, cancellationToken: cancellationToken)
        .ConfigureAwait(false);

      if (!result.Success || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
      {
        throw new FfmpegException("Не удалось подготовить предпросмотр.", result.StandardError);
      }

      File.Move(tempPath, target, overwrite: true);
      WriteFingerprint(fingerprintPath, PreviewCache.ComputeFingerprint(sourcePath));
      return target;
    }
    finally
    {
      TryDelete(tempPath);
    }
  }

  private static void WriteFingerprint(string fingerprintPath, string fingerprint)
  {
    var tempPath = fingerprintPath + ".tmp";
    try
    {
      File.WriteAllText(tempPath, fingerprint);
      File.Move(tempPath, fingerprintPath, overwrite: true);
    }
    finally
    {
      TryDelete(tempPath);
    }
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }
    catch
    {
    }
  }
}
