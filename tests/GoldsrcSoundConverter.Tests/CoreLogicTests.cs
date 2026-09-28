using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class Cs16PresetsTests
{
  [Fact]
  public void WavSoundPresetMatchesCs16Expectations()
  {
    var preset = Cs16Presets.ById("wav-sound");

    Assert.NotNull(preset);
    Assert.Equal(OutputAudioFormat.Wav, preset!.Format);
    Assert.Equal(22050, preset.SampleRate);
    Assert.Equal(TargetChannels.Mono, preset.Channels);
    Assert.Equal(TargetBitDepth.Sixteen, preset.BitDepth);
  }

  [Fact]
  public void Mp3MusicPresetIsCbr44100Stereo()
  {
    var preset = Cs16Presets.ById("mp3-music");

    Assert.NotNull(preset);
    Assert.Equal(OutputAudioFormat.Mp3, preset!.Format);
    Assert.Equal(44100, preset.SampleRate);
    Assert.Equal(TargetChannels.Stereo, preset.Channels);
    Assert.Equal(128, preset.Mp3BitrateKbps);
  }

  [Fact]
  public void ForFormatFiltersPresets()
  {
    var mp3 = Cs16Presets.ForFormat(OutputAudioFormat.Mp3);

    Assert.Contains(mp3, p => p.Id == "mp3-music");
    Assert.Contains(mp3, p => p.IsCustom);
    Assert.DoesNotContain(mp3, p => p.Id == "wav-sound");
  }

  [Fact]
  public void MatchFindsEquivalentPreset()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      BitDepth = TargetBitDepth.Sixteen,
      Mp3BitrateKbps = 128,
    };

    Assert.Equal("mp3-music", Cs16Presets.Match(options)?.Id);
  }

  [Fact]
  public void MatchReturnsNullForCustomCombination()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 44100,
      Channels = TargetChannels.Mono,
      BitDepth = TargetBitDepth.Eight,
    };

    Assert.Null(Cs16Presets.Match(options));
  }

  [Fact]
  public void ByIdReturnsNullForUnknownId()
  {
    Assert.Null(Cs16Presets.ById("does-not-exist"));
    Assert.Null(Cs16Presets.ById(null));
  }

  [Fact]
  public void CustomPresetIsAvailableForEveryFormat()
  {
    foreach (var format in new[] { OutputAudioFormat.Wav, OutputAudioFormat.Mp3 })
    {
      var preset = Cs16Presets.ForFormat(format).Single(p => p.IsCustom);
      Assert.Equal(Cs16Presets.CustomId, preset.Id);
    }
  }

  [Fact]
  public void Mp3MatchRequiresMatchingBitrate()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      BitDepth = TargetBitDepth.Sixteen,
      Mp3BitrateKbps = 192,
    };

    Assert.Equal("mp3-music-hq", Cs16Presets.Match(options)?.Id);
  }

  [Fact]
  public void DefaultForFormatPicksExpectedPreset()
  {
    Assert.Equal("wav-sound", Cs16Presets.DefaultFor(OutputAudioFormat.Wav).Id);
    Assert.Equal("mp3-music", Cs16Presets.DefaultFor(OutputAudioFormat.Mp3).Id);
  }
}

public sealed class WaveformDataTests
{
  [Fact]
  public void AggregateReturnsBucketRange()
  {
    var data = new WaveformData(
      new[] { -1f, -0.5f, -0.2f, -0.1f },
      new[] { 0.1f, 0.4f, 0.8f, 1f },
      samplesPerBucket: 64,
      sourceSampleRate: 8000);

    var (min, max) = data.Aggregate(1, 3);

    Assert.Equal(-0.5f, min);
    Assert.Equal(0.8f, max);
  }

  [Fact]
  public void AggregateClampsOutOfRange()
  {
    var data = new WaveformData(new[] { -0.5f }, new[] { 0.5f }, 64, 8000);

    var (min, max) = data.Aggregate(-10, 100);

    Assert.Equal(-0.5f, min);
    Assert.Equal(0.5f, max);
  }

  [Fact]
  public void DurationMatchesBuckets()
  {
    var data = new WaveformData(new float[80], new float[80], 64, 8000);

    Assert.Equal(0.64, data.Duration.TotalSeconds, 3);
  }
}

public sealed class PeakNormalizerTests
{
  [Fact]
  public void ComputesGainToTarget()
  {
    Assert.Equal(19.7, PeakNormalizer.ComputeGainDb(-20, -0.3, 24)!.Value, 3);
  }

  [Fact]
  public void ReturnsNullWhenAlreadyLoudEnough()
  {
    Assert.Null(PeakNormalizer.ComputeGainDb(-0.1, -0.3, 24));
  }

  [Fact]
  public void ReturnsNullForSilence()
  {
    Assert.Null(PeakNormalizer.ComputeGainDb(-91, -0.3, 24));
  }

  [Fact]
  public void ClampsToMaxGain()
  {
    Assert.Equal(24, PeakNormalizer.ComputeGainDb(-60, -0.3, 24)!.Value, 3);
  }
}

public sealed class AudioFileTypesTests
{
  [Theory]
  [InlineData("a.wav", true)]
  [InlineData("a.MP3", true)]
  [InlineData("a.ogg", true)]
  [InlineData("a.flac", true)]
  [InlineData("a.m4a", true)]
  [InlineData("a.mp4", true)]
  [InlineData("a.txt", false)]
  [InlineData("a.sma", false)]
  public void DetectsSupportedExtensions(string path, bool expected)
  {
    Assert.Equal(expected, Core.Files.AudioFileTypes.IsSupported(path));
  }
}
