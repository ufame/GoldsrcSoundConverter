namespace GoldsrcSoundConverter.Core.Files;

public sealed record InputFileCandidate(string FilePath, string? SourceRoot);

public static class InputFileDiscoverer
{
  public static IReadOnlyList<InputFileCandidate> Discover(
    IEnumerable<string> paths,
    Action<string, Exception>? onError = null)
  {
    var results = new List<InputFileCandidate>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var path in paths)
    {
      try
      {
        if (Directory.Exists(path))
        {
          foreach (var file in Directory
                     .EnumerateFiles(path, "*", SearchOption.AllDirectories)
                     .Where(AudioFileTypes.IsSupported))
          {
            if (seen.Add(Path.GetFullPath(file)))
            {
              results.Add(new InputFileCandidate(file, path));
            }
          }
        }
        else if (File.Exists(path) && AudioFileTypes.IsSupported(path))
        {
          if (seen.Add(Path.GetFullPath(path)))
          {
            results.Add(new InputFileCandidate(path, null));
          }
        }
      }
      catch (Exception ex)
      {
        onError?.Invoke(path, ex);
      }
    }

    return results;
  }
}
