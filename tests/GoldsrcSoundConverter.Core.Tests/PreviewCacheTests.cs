using GoldsrcSoundConverter.Core.Audio;

namespace GoldsrcSoundConverter.Tests;

public sealed class PreviewCacheTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private string SourcePath => Path.Combine(_temp.Path, "source.ogg");

  private string CachePath => Path.Combine(_temp.Path, "cache.wav");

  private string FingerprintPath => CachePath + ".meta";

  [Fact]
  public void KeyIsStableAndCaseInsensitive()
  {
    var first = PreviewCache.ComputeKey(@"C:\Sounds\Voice.ogg");
    var second = PreviewCache.ComputeKey(@"c:\sounds\voice.OGG");

    Assert.Equal(first, second);
  }

  [Fact]
  public void KeyDiffersForDifferentPaths()
  {
    Assert.NotEqual(
      PreviewCache.ComputeKey(@"C:\a.wav"),
      PreviewCache.ComputeKey(@"C:\b.wav"));
  }

  [Fact]
  public void FingerprintChangesWhenSourceChanges()
  {
    File.WriteAllText(SourcePath, "one");
    var first = PreviewCache.ComputeFingerprint(SourcePath);

    File.WriteAllText(SourcePath, "much longer content");
    var second = PreviewCache.ComputeFingerprint(SourcePath);

    File.SetLastWriteTimeUtc(SourcePath, DateTime.UtcNow.AddHours(1));
    var third = PreviewCache.ComputeFingerprint(SourcePath);

    Assert.NotEqual(first, second);
    Assert.NotEqual(second, third);
  }

  [Fact]
  public void FingerprintForMissingSourceIsMarked()
  {
    Assert.EndsWith("|missing", PreviewCache.ComputeFingerprint(SourcePath));
  }

  [Fact]
  public void CacheIsInvalidWhenMissing()
  {
    File.WriteAllText(SourcePath, "data");

    Assert.False(PreviewCache.IsValid(SourcePath, CachePath, FingerprintPath));
  }

  [Fact]
  public void CacheIsInvalidWithoutFingerprintFile()
  {
    File.WriteAllText(SourcePath, "data");
    File.WriteAllText(CachePath, "wav-bytes");

    Assert.False(PreviewCache.IsValid(SourcePath, CachePath, FingerprintPath));
  }

  [Fact]
  public void CacheIsInvalidWhenEmpty()
  {
    File.WriteAllText(SourcePath, "data");
    File.WriteAllText(CachePath, string.Empty);
    File.WriteAllText(FingerprintPath, PreviewCache.ComputeFingerprint(SourcePath));

    Assert.False(PreviewCache.IsValid(SourcePath, CachePath, FingerprintPath));
  }

  [Fact]
  public void CacheIsValidForMatchingFingerprint()
  {
    File.WriteAllText(SourcePath, "data");
    File.WriteAllText(CachePath, "wav-bytes");
    File.WriteAllText(FingerprintPath, PreviewCache.ComputeFingerprint(SourcePath));

    Assert.True(PreviewCache.IsValid(SourcePath, CachePath, FingerprintPath));
  }

  [Fact]
  public void CacheIsInvalidAfterSourceChanged()
  {
    File.WriteAllText(SourcePath, "data");
    File.WriteAllText(CachePath, "wav-bytes");
    File.WriteAllText(FingerprintPath, PreviewCache.ComputeFingerprint(SourcePath));

    File.WriteAllText(SourcePath, "other data");

    Assert.False(PreviewCache.IsValid(SourcePath, CachePath, FingerprintPath));
  }
}
