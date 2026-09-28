using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Tests;

public sealed class FfmpegBootstrapperTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void PinnedReleaseConstantsAreConsistent()
  {
    Assert.Contains(FfmpegBootstrapper.Version, FfmpegBootstrapper.DownloadUrl);
    Assert.EndsWith(".zip", FfmpegBootstrapper.DownloadUrl);
    Assert.Matches("^[0-9a-f]{64}$", FfmpegBootstrapper.ArchiveSha256);
  }

  [Fact]
  public void ResolvesCustomDirectoryWithBothBinaries()
  {
    CreateBinary("ffmpeg.exe");
    CreateBinary("ffprobe.exe");

    var result = FfmpegBootstrapper.TryResolveCustom(
      _temp.Path,
      out var ffmpeg,
      out var ffprobe,
      out var error);

    Assert.True(result);
    Assert.Null(error);
    Assert.Equal(Path.Combine(_temp.Path, "ffmpeg.exe"), ffmpeg);
    Assert.Equal(Path.Combine(_temp.Path, "ffprobe.exe"), ffprobe);
  }

  [Fact]
  public void ResolvesBinSubfolder()
  {
    var bin = Path.Combine(_temp.Path, "bin");
    Directory.CreateDirectory(bin);
    File.WriteAllText(Path.Combine(bin, "ffmpeg.exe"), string.Empty);
    File.WriteAllText(Path.Combine(bin, "ffprobe.exe"), string.Empty);

    var result = FfmpegBootstrapper.TryResolveCustom(_temp.Path, out var ffmpeg, out _, out var error);

    Assert.True(result);
    Assert.Null(error);
    Assert.Equal(Path.Combine(bin, "ffmpeg.exe"), ffmpeg);
  }

  [Fact]
  public void MissingFfprobeIsExplicitFailure()
  {
    CreateBinary("ffmpeg.exe");

    var result = FfmpegBootstrapper.TryResolveCustom(
      _temp.Path,
      out var ffmpeg,
      out var ffprobe,
      out var error);

    Assert.False(result);
    Assert.Equal(string.Empty, ffmpeg);
    Assert.Equal(string.Empty, ffprobe);
    Assert.NotNull(error);
    Assert.Contains("ffprobe", error, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void MissingFfmpegIsExplicitFailure()
  {
    CreateBinary("ffprobe.exe");

    var result = FfmpegBootstrapper.TryResolveCustom(_temp.Path, out _, out _, out var error);

    Assert.False(result);
    Assert.NotNull(error);
    Assert.Contains("ffmpeg", error, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public void UnknownPathIsExplicitFailure()
  {
    var missing = Path.Combine(_temp.Path, "nope");

    var result = FfmpegBootstrapper.TryResolveCustom(missing, out _, out _, out var error);

    Assert.False(result);
    Assert.NotNull(error);
  }

  [Fact]
  public void EmptyCustomPathHasNoError()
  {
    var result = FfmpegBootstrapper.TryResolveCustom(null, out _, out _, out var error);

    Assert.False(result);
    Assert.Null(error);
  }

  [Fact]
  public void InstalledDirectoryRequiresBothBinaries()
  {
    var bootstrapper = new FfmpegBootstrapper(_temp.Path);
    CreateBinary("ffmpeg.exe");

    Assert.False(bootstrapper.TryResolve(out _, out _));

    CreateBinary("ffprobe.exe");

    Assert.True(bootstrapper.TryResolve(out var ffmpeg, out var ffprobe));
    Assert.Equal(Path.Combine(_temp.Path, "ffmpeg.exe"), ffmpeg);
    Assert.Equal(Path.Combine(_temp.Path, "ffprobe.exe"), ffprobe);
  }

  private void CreateBinary(string name)
  {
    File.WriteAllText(Path.Combine(_temp.Path, name), string.Empty);
  }
}
