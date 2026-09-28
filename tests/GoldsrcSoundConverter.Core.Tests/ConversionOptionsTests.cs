using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionOptionsTests
{
  [Fact]
  public void DefaultsAreValid()
  {
    var options = new ConversionOptions();

    Assert.Same(options, options.Validate());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void RejectsNonPositiveSampleRate(int sampleRate)
  {
    var options = new ConversionOptions { SampleRate = sampleRate };

    Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
  }

  [Theory]
  [InlineData(1)]
  [InlineData(4)]
  [InlineData(16)]
  public void AcceptsParallelismWithinRange(int parallelism)
  {
    var options = new ConversionOptions { Parallelism = parallelism };

    Assert.Same(options, options.Validate());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  [InlineData(17)]
  public void RejectsParallelismOutsideRange(int parallelism)
  {
    var options = new ConversionOptions { Parallelism = parallelism };

    Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
  }

  [Theory]
  [InlineData(32)]
  [InlineData(128)]
  [InlineData(320)]
  public void AcceptsValidMp3Bitrate(int bitrate)
  {
    var options = new ConversionOptions { Format = OutputAudioFormat.Mp3, Mp3BitrateKbps = bitrate };

    Assert.Same(options, options.Validate());
  }

  [Theory]
  [InlineData(0)]
  [InlineData(16)]
  [InlineData(321)]
  public void RejectsInvalidMp3Bitrate(int bitrate)
  {
    var options = new ConversionOptions { Format = OutputAudioFormat.Mp3, Mp3BitrateKbps = bitrate };

    Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
  }

  [Fact]
  public void IgnoresMp3BitrateForWavFormat()
  {
    var options = new ConversionOptions { Format = OutputAudioFormat.Wav, Mp3BitrateKbps = 0 };

    Assert.Same(options, options.Validate());
  }

  [Theory]
  [InlineData(0.0)]
  [InlineData(-0.3)]
  [InlineData(-60.0)]
  public void AcceptsValidNormalizeTarget(double target)
  {
    var options = new ConversionOptions { NormalizeTargetDb = target };

    Assert.Same(options, options.Validate());
  }

  [Theory]
  [InlineData(1.0)]
  [InlineData(-60.1)]
  public void RejectsInvalidNormalizeTarget(double target)
  {
    var options = new ConversionOptions { NormalizeTargetDb = target };

    Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
  }

  [Theory]
  [InlineData(-1.0)]
  [InlineData(97.0)]
  public void RejectsInvalidMaxNormalizeGain(double gain)
  {
    var options = new ConversionOptions { MaxNormalizeGainDb = gain };

    Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
  }

  [Fact]
  public void WithReturnsNewInstanceAndKeepsOriginal()
  {
    var original = new ConversionOptions();
    var changed = original with { SampleRate = 44100 };

    Assert.NotSame(original, changed);
    Assert.Equal(22050, original.SampleRate);
    Assert.Equal(44100, changed.SampleRate);
  }

  [Fact]
  public void ExtensionDependsOnFormat()
  {
    Assert.Equal(".wav", new ConversionOptions().Extension);
    Assert.Equal(".mp3", (new ConversionOptions() with { Format = OutputAudioFormat.Mp3 }).Extension);
  }
}
