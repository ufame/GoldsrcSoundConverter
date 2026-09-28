using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class PresetCatalogTests
{
  [Fact]
  public void ForFormatReturnsMatchingPresetsAndCustom()
  {
    var catalog = new PresetCatalog();

    var presets = catalog.ForFormat(OutputAudioFormat.Mp3);

    Assert.Contains(presets, preset => preset.Id == "mp3-music");
    Assert.Contains(presets, preset => preset.IsCustom);
    Assert.DoesNotContain(presets, preset => preset.Id == "wav-sound");
  }

  [Fact]
  public void ResolveReturnsMatchingPreset()
  {
    var catalog = new PresetCatalog();
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      BitDepth = TargetBitDepth.Sixteen,
      Mp3BitrateKbps = 128,
    };

    Assert.Equal("mp3-music", catalog.Resolve(options).Id);
  }

  [Fact]
  public void ResolveFallsBackToCustomPreset()
  {
    var catalog = new PresetCatalog();
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 44100,
      Channels = TargetChannels.Mono,
      BitDepth = TargetBitDepth.Eight,
    };

    var preset = catalog.Resolve(options);

    Assert.True(preset.IsCustom);
    Assert.Equal(Cs16Presets.CustomId, preset.Id);
  }
}
