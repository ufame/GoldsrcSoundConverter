using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class WaveformLoaderTests
{
  private static readonly float[] Mins = { -0.5f };
  private static readonly float[] Maxs = { 0.5f };

  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  private WaveformLoader CreateLoader()
  {
    return new WaveformLoader(_conversion, _log);
  }

  private static QueueItemViewModel Item()
  {
    return new QueueItemViewModel(@"C:\in\clip.ogg", null);
  }

  private static ConversionOptions Options()
  {
    return new ConversionOptions { OutputDirectory = @"C:\out" };
  }

  [Fact]
  public async Task LoadsWaveformOnce()
  {
    _conversion.Waveform = new WaveformData(Mins, Maxs, 64, 8000);
    var loader = CreateLoader();
    var item = Item();

    await loader.EnsureLoadedAsync(item, Options());
    await loader.EnsureLoadedAsync(item, Options());

    Assert.NotNull(item.Waveform);
    Assert.Equal(1, _conversion.WaveformRequests);
    Assert.False(item.IsWaveformLoading);
  }

  [Fact]
  public async Task MissingWaveformDoesNotFail()
  {
    _conversion.Waveform = null;
    var loader = CreateLoader();
    var item = Item();

    await loader.EnsureLoadedAsync(item, Options());

    Assert.Null(item.Waveform);
    Assert.False(item.IsWaveformLoading);
    Assert.Empty(_log.Entries);
  }

  [Fact]
  public async Task ExtractionFailureIsLogged()
  {
    _conversion.WaveformException = new InvalidOperationException("decode failed");
    var loader = CreateLoader();
    var item = Item();

    await loader.EnsureLoadedAsync(item, Options());

    Assert.False(item.IsWaveformLoading);
    Assert.Contains(_log.Entries, entry => entry.Contains("decode failed", StringComparison.Ordinal));
  }
}
