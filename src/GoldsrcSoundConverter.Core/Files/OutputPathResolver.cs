using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Files;

public static class OutputPathResolver
{
  public static string? Resolve(string sourcePath, string? sourceRoot, ConversionOptions options)
  {
    var baseName = FileNameSanitizer.Sanitize(
      Path.GetFileNameWithoutExtension(sourcePath),
      options.AsciiNames,
      options.LowercaseNames);

    var relativeDirectory = ResolveRelativeDirectory(sourcePath, sourceRoot, options.PreserveStructure);
    var directory = Path.Combine(options.OutputDirectory, relativeDirectory);
    var candidate = Path.Combine(directory, baseName + options.Extension);

    if (options.CollisionPolicy == CollisionPolicy.Skip && File.Exists(candidate))
    {
      return null;
    }

    if (options.CollisionPolicy == CollisionPolicy.Overwrite)
    {
      return PathsEqual(candidate, sourcePath)
        ? FindFreePath(directory, baseName, options.Extension, sourcePath)
        : candidate;
    }

    return File.Exists(candidate) || PathsEqual(candidate, sourcePath)
      ? FindFreePath(directory, baseName, options.Extension, sourcePath)
      : candidate;
  }

  private static string ResolveRelativeDirectory(string sourcePath, string? sourceRoot, bool preserveStructure)
  {
    if (!preserveStructure || string.IsNullOrEmpty(sourceRoot))
    {
      return string.Empty;
    }

    var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
    var root = Path.GetFullPath(sourceRoot);
    if (sourceDirectory is null || !sourceDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase))
    {
      return string.Empty;
    }

    var relative = sourceDirectory[root.Length..]
      .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return relative;
  }

  private static string FindFreePath(string directory, string baseName, string extension, string sourcePath)
  {
    for (var i = 1; i < 10000; i++)
    {
      var candidate = Path.Combine(directory, $"{baseName}_{i}{extension}");
      if (!File.Exists(candidate) && !PathsEqual(candidate, sourcePath))
      {
        return candidate;
      }
    }

    throw new InvalidOperationException("Не удалось подобрать свободное имя файла.");
  }

  private static bool PathsEqual(string first, string second)
  {
    return string.Equals(
      Path.GetFullPath(first),
      Path.GetFullPath(second),
      StringComparison.OrdinalIgnoreCase);
  }
}
