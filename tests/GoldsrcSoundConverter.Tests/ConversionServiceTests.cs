using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionServiceTests : IDisposable
{
  private const string ProbeJson =
    """{"streams":[{"codec_name":"pcm_s16le","sample_rate":"22050","channels":1,"bit_rate":"352800","duration":"1.0"}],"format":{"format_name":"wav","duration":"1.0","bit_rate":"352800"}}""";

  private readonly TempDirectory _temp = new();
  private readonly TempDirectory _install = new();

  public void Dispose()
  {
    _temp.Dispose();
    _install.Dispose();
  }

  [Fact]
  public async Task ConvertAsyncRunsPipelineAndReturnsOutcomes()
  {
    var runner = new FakeProcessRunner(call => call.IsProbe
      ? new FakeProcessRunner.Response(0, ProbeJson, "")
      : new FakeProcessRunner.Response(0));
    var service = CreateService(runner);
    var source = CreateSource("clip.wav");
    var workItems = new[]
    {
      new ConversionWorkItem(Guid.NewGuid(), source, null, null, null, null),
    };

    var result = await service.ConvertAsync(
      workItems,
      Options(),
      progress: null,
      probeProgress: null,
      bootstrapProgress: null,
      log: null);

    var outcome = Assert.Single(result.Outcomes);
    Assert.True(outcome.Success, outcome.Error);
    Assert.Equal(Path.Combine(_temp.Path, "clip.wav"), outcome.OutputPath);
    Assert.Equal(Path.Combine(_install.Path, "ffmpeg.exe"), result.FfmpegPath);
    Assert.Equal(Path.Combine(_install.Path, "ffprobe.exe"), result.FfprobePath);
    Assert.Contains(runner.Calls, call => !call.IsProbe);
  }

  [Fact]
  public async Task TryProbeReturnsNullWhenFfmpegMissing()
  {
    var runner = new FakeProcessRunner();
    var service = CreateService(runner, installBinaries: false);

    var result = await service.TryProbeAsync(Guid.NewGuid(), CreateSource("clip.wav"));

    Assert.Null(result);
    Assert.Empty(runner.Calls);
  }

  [Fact]
  public async Task TryProbeReturnsInfoWhenFfmpegPresent()
  {
    var runner = new FakeProcessRunner(_ => new FakeProcessRunner.Response(0, ProbeJson, ""));
    var service = CreateService(runner);
    var id = Guid.NewGuid();

    var result = await service.TryProbeAsync(id, CreateSource("clip.wav"));

    Assert.NotNull(result);
    Assert.Equal(id, result!.Id);
    Assert.Equal(22050, result.Info.SampleRate);
  }

  [Fact]
  public async Task ConvertSingleAsyncUsesProvidedOptions()
  {
    var runner = new FakeProcessRunner(call => call.IsProbe
      ? new FakeProcessRunner.Response(0, ProbeJson, "")
      : new FakeProcessRunner.Response(0));
    var service = CreateService(runner);
    var item = new ConversionWorkItem(Guid.NewGuid(), CreateSource("clip.ogg"), null, null, null, null);
    var options = Options() with { Format = OutputAudioFormat.Mp3, Mp3BitrateKbps = 192 };

    var outcome = await service.ConvertSingleAsync(item, options, null, null);

    Assert.True(outcome.Success, outcome.Error);
    Assert.EndsWith(".mp3", outcome.OutputPath);
    var convert = runner.Calls.Last(call => !call.IsProbe);
    Assert.Contains("libmp3lame", convert.Arguments);
  }

  [Fact]
  public async Task PreparePlayableAsyncDecodesNonPlayableSource()
  {
    var runner = new FakeProcessRunner(call =>
    {
      var output = call.Arguments[^1];
      File.WriteAllText(output, "wav-bytes");
      return new FakeProcessRunner.Response(0);
    });
    var service = CreateService(runner);
    var source = CreateSource("clip.ogg");

    var playable = await service.PreparePlayableAsync(source, null, null);

    Assert.True(File.Exists(playable));
    Assert.EndsWith(".wav", playable);
  }

  [Fact]
  public void CustomPathIsUsedAndClearedWhenChanged()
  {
    using var custom = new TempDirectory();
    File.WriteAllText(Path.Combine(custom.Path, "ffmpeg.exe"), string.Empty);
    File.WriteAllText(Path.Combine(custom.Path, "ffprobe.exe"), string.Empty);
    var runner = new FakeProcessRunner();
    var service = CreateService(runner, installBinaries: false);

    service.SetCustomFfmpegPath(custom.Path);
    Assert.True(service.TryResolveFfmpeg(out var ffmpeg, out _));
    Assert.Equal(Path.Combine(custom.Path, "ffmpeg.exe"), ffmpeg);

    service.SetCustomFfmpegPath(null);
    Assert.False(service.TryResolveFfmpeg(out _, out _));
  }

  private ConversionService CreateService(FakeProcessRunner runner, bool installBinaries = true)
  {
    if (installBinaries)
    {
      File.WriteAllText(Path.Combine(_install.Path, "ffmpeg.exe"), string.Empty);
      File.WriteAllText(Path.Combine(_install.Path, "ffprobe.exe"), string.Empty);
    }

    return new ConversionService(runner, new FfmpegBootstrapper(_install.Path));
  }

  private ConversionOptions Options()
  {
    return new ConversionOptions
    {
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };
  }

  private string CreateSource(string name)
  {
    var directory = Path.Combine(_temp.Path, "src");
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, name);
    File.WriteAllText(path, "data");
    return path;
  }
}
