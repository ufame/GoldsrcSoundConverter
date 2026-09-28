using GoldsrcSoundConverter.Core.Files;

namespace GoldsrcSoundConverter.Tests;

public sealed class InputFileDiscovererTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void DiscoversSupportedFilesRecursivelyWithSourceRoot()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    var nested = Path.Combine(root, "weapons");
    Directory.CreateDirectory(nested);
    var top = Path.Combine(root, "hit.wav");
    var deep = Path.Combine(nested, "shot.ogg");
    File.WriteAllText(top, "x");
    File.WriteAllText(deep, "x");
    File.WriteAllText(Path.Combine(root, "notes.txt"), "x");

    var candidates = InputFileDiscoverer.Discover(new[] { root });

    Assert.Equal(2, candidates.Count);
    Assert.All(candidates, candidate => Assert.Equal(root, candidate.SourceRoot));
    Assert.Contains(candidates, candidate => candidate.FilePath == top);
    Assert.Contains(candidates, candidate => candidate.FilePath == deep);
  }

  [Fact]
  public void DirectFileHasNoSourceRoot()
  {
    var file = Path.Combine(_temp.Path, "voice.wav");
    File.WriteAllText(file, "x");

    var candidates = InputFileDiscoverer.Discover(new[] { file });

    var candidate = Assert.Single(candidates);
    Assert.Equal(file, candidate.FilePath);
    Assert.Null(candidate.SourceRoot);
  }

  [Fact]
  public void DeduplicatesRepeatedPathsAndFiles()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    Directory.CreateDirectory(root);
    var file = Path.Combine(root, "hit.wav");
    File.WriteAllText(file, "x");

    var candidates = InputFileDiscoverer.Discover(new[] { root, root, file });

    Assert.Single(candidates);
    Assert.Equal(file, candidates[0].FilePath);
  }

  [Fact]
  public void IgnoresMissingAndUnsupportedPaths()
  {
    var text = Path.Combine(_temp.Path, "notes.txt");
    File.WriteAllText(text, "x");

    var candidates = InputFileDiscoverer.Discover(new[]
    {
      Path.Combine(_temp.Path, "missing"),
      text,
    });

    Assert.Empty(candidates);
  }

  [Fact]
  public void AcceptsMixedFoldersAndFiles()
  {
    var root = Path.Combine(_temp.Path, "sounds");
    Directory.CreateDirectory(root);
    var inFolder = Path.Combine(root, "hit.wav");
    var direct = Path.Combine(_temp.Path, "voice.wav");
    File.WriteAllText(inFolder, "x");
    File.WriteAllText(direct, "x");

    var candidates = InputFileDiscoverer.Discover(new[] { root, direct });

    Assert.Equal(2, candidates.Count);
    Assert.Contains(candidates, candidate => candidate.FilePath == inFolder && candidate.SourceRoot == root);
    Assert.Contains(candidates, candidate => candidate.FilePath == direct && candidate.SourceRoot is null);
  }
}
