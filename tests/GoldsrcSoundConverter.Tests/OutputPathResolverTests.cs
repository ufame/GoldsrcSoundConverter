using GoldsrcSoundConverter.Core.Files;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class OutputPathResolverTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private ConversionOptions Options => new()
  {
    OutputDirectory = _temp.Path,
    AsciiNames = true,
    LowercaseNames = true,
    CollisionPolicy = CollisionPolicy.Rename,
  };

  [Fact]
  public void ResolvesSanitizedPath()
  {
    var source = CreateFile("Привет Мир.ogg");
    var result = OutputPathResolver.Resolve(source, null, Options);

    Assert.Equal(Path.Combine(_temp.Path, "privet_mir.wav"), result);
  }

  [Fact]
  public void RenamesWhenTargetExists()
  {
    var source = CreateFile("sound.wav");
    File.WriteAllText(Path.Combine(_temp.Path, "sound_1.wav"), "x");

    var result = OutputPathResolver.Resolve(source, null, Options);

    Assert.Equal(Path.Combine(_temp.Path, "sound_2.wav"), result);
  }

  [Fact]
  public void NeverOverwritesSourceFile()
  {
    var source = CreateFile("voice.wav");

    var result = OutputPathResolver.Resolve(source, null, Options);

    Assert.NotNull(result);
    Assert.NotEqual(source, result);
  }

  [Fact]
  public void SkipPolicyReturnsNull()
  {
    var source = CreateFile("music.mp3");
    File.WriteAllText(Path.Combine(_temp.Path, "music.wav"), "x");
    var options = Options with { CollisionPolicy = CollisionPolicy.Skip };

    Assert.Null(OutputPathResolver.Resolve(source, null, options));
  }

  [Fact]
  public void OverwritePolicyReturnsTarget()
  {
    var source = CreateFile("music.ogg");
    var existing = Path.Combine(_temp.Path, "music.wav");
    File.WriteAllText(existing, "x");
    var options = Options with { CollisionPolicy = CollisionPolicy.Overwrite };

    Assert.Equal(existing, OutputPathResolver.Resolve(source, null, options));
  }

  [Fact]
  public void PreservesSubdirectories()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var nested = Path.Combine(root, "weapons", "rifle");
    Directory.CreateDirectory(nested);
    var source = Path.Combine(nested, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(source, root, options);

    Assert.Equal(Path.Combine(output, "weapons", "rifle", "shot.wav"), result);
  }

  [Fact]
  public void FileInRootHasNoRelativeDirectory()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    Directory.CreateDirectory(root);
    var source = Path.Combine(root, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(source, root, options);

    Assert.Equal(Path.Combine(output, "shot.wav"), result);
  }

  [Fact]
  public void SiblingDirectoryWithSharedPrefixIsNotInsideRoot()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var sibling = Path.Combine(_temp.Path, "sounds_backup");
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(sibling);
    var source = Path.Combine(sibling, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(source, root, options);

    Assert.Equal(Path.Combine(output, "shot.wav"), result);
  }

  [Fact]
  public void OutsideRootIsFlattenedToOutputDirectory()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var other = Path.Combine(_temp.Path, "other");
    Directory.CreateDirectory(root);
    Directory.CreateDirectory(other);
    var source = Path.Combine(other, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(source, root, options);

    Assert.Equal(Path.Combine(output, "shot.wav"), result);
  }

  [Fact]
  public void RootComparisonIsCaseInsensitive()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var nested = Path.Combine(root, "weapons");
    Directory.CreateDirectory(nested);
    var source = Path.Combine(nested, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(source, Path.Combine(_temp.Path, "SOUNDS"), options);

    Assert.Equal(Path.Combine(output, "weapons", "shot.wav"), result);
  }

  [Fact]
  public void RootWithTrailingSeparatorIsAccepted()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var nested = Path.Combine(root, "weapons");
    Directory.CreateDirectory(nested);
    var source = Path.Combine(nested, "shot.wav");
    File.WriteAllText(source, "x");
    var output = Path.Combine(_temp.Path, "out");
    Directory.CreateDirectory(output);

    var options = Options with { PreserveStructure = true, OutputDirectory = output };

    var result = OutputPathResolver.Resolve(
      source,
      root + Path.DirectorySeparatorChar,
      options);

    Assert.Equal(Path.Combine(output, "weapons", "shot.wav"), result);
  }

  [Fact]
  public void ChangesExtensionAccordingToFormat()
  {
    var source = CreateFile("theme.ogg");
    var options = Options with { Format = OutputAudioFormat.Mp3 };

    var result = OutputPathResolver.Resolve(source, null, options);

    Assert.Equal(Path.Combine(_temp.Path, "theme.mp3"), result);
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }
}
