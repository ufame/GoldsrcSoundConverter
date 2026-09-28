using System.Security.Cryptography;
using System.Text;

namespace GoldsrcSoundConverter.Core.Audio;

public static class PreviewCache
{
  public static string ComputeKey(string sourcePath)
  {
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sourcePath.ToLowerInvariant()));
    return Convert.ToHexString(hash)[..16];
  }

  public static string ComputeFingerprint(string sourcePath)
  {
    var fullPath = Path.GetFullPath(sourcePath);
    try
    {
      var info = new FileInfo(fullPath);
      return info.Exists
        ? $"{fullPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}"
        : $"{fullPath}|missing";
    }
    catch (Exception)
    {
      return $"{fullPath}|missing";
    }
  }

  public static bool IsValid(string sourcePath, string cachedPath, string fingerprintPath)
  {
    try
    {
      if (!File.Exists(cachedPath) || new FileInfo(cachedPath).Length == 0)
      {
        return false;
      }

      if (!File.Exists(fingerprintPath))
      {
        return false;
      }

      return string.Equals(
        File.ReadAllText(fingerprintPath),
        ComputeFingerprint(sourcePath),
        StringComparison.Ordinal);
    }
    catch (Exception)
    {
      return false;
    }
  }
}
