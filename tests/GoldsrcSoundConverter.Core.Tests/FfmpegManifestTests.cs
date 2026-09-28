using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Tests;

public sealed class FfmpegManifestTests
{
  [Fact]
  public void LoadsPinnedManifest()
  {
    var manifest = FfmpegManifestLoader.Load();

    Assert.False(string.IsNullOrWhiteSpace(manifest.Version));
    Assert.Contains(manifest.Version, manifest.Url);
    Assert.EndsWith(".zip", manifest.Url);
    Assert.Matches("^[0-9a-f]{64}$", manifest.Sha256);
  }

  [Fact]
  public void BootstrapperExposesManifestValues()
  {
    var manifest = FfmpegManifestLoader.Load();

    Assert.Equal(manifest.Version, FfmpegBootstrapper.Version);
    Assert.Equal(manifest.Url, FfmpegBootstrapper.DownloadUrl);
    Assert.Equal(manifest.Sha256, FfmpegBootstrapper.ArchiveSha256);
  }
}
