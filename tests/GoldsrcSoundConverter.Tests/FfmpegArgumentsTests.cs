using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class FfmpegArgumentsTests
{
  [Fact]
  public void BuildConvertWavContainsCs16Settings()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 22050,
      Channels = TargetChannels.Mono,
      BitDepth = TargetBitDepth.Sixteen,
    };

    var args = FfmpegArguments.BuildConvert("in.ogg", "out.wav", options, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));

    Assert.Contains("pcm_s16le", args);
    Assert.Contains("22050", args);
    Assert.Contains("1", args);
    Assert.Contains("-map_metadata", args);
    Assert.Contains("in.ogg", args);
    Assert.Contains("out.wav", args);
    Assert.DoesNotContain("libmp3lame", args);
  }

  [Fact]
  public void BuildConvertWavEightBitUsesUnsignedPcm()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      BitDepth = TargetBitDepth.Eight,
    };

    var args = FfmpegArguments.BuildConvert("in.wav", "out.wav", options, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

    Assert.Contains("pcm_u8", args);
  }

  [Fact]
  public void BuildConvertMp3UsesLameCbrWithoutId3()
  {
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      Mp3BitrateKbps = 128,
    };

    var args = FfmpegArguments.BuildConvert("in.flac", "out.mp3", options, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));

    Assert.Contains("libmp3lame", args);
    Assert.Contains("128k", args);
    Assert.Contains("-id3v2_version", args);
    Assert.Contains("0", args);
    Assert.Contains("-write_id3v1", args);
  }

  [Fact]
  public void BuildConvertAppliesTrimAndNormalization()
  {
    var options = new ConversionOptions();
    var args = FfmpegArguments.BuildConvert(
      "in.wav",
      "out.wav",
      options,
      normalizeGainDb: 3.25,
      trimStart: TimeSpan.FromSeconds(1.5),
      outputDuration: TimeSpan.FromSeconds(2.5));

    var ssIndex = args.ToList().IndexOf("-ss");
    Assert.True(ssIndex >= 0);
    Assert.Equal("1.5", args[ssIndex + 1]);
    Assert.Contains("volume=+3.25dB", args);
    var tIndex = args.ToList().IndexOf("-t");
    Assert.True(tIndex >= 0);
    Assert.Equal("2.5", args[tIndex + 1]);
  }

  [Fact]
  public void BuildMeasureVolumeUsesVolumeDetect()
  {
    var args = FfmpegArguments.BuildMeasureVolume("in.mp3", TimeSpan.Zero, TimeSpan.FromSeconds(1));
    Assert.Contains("volumedetect", args);
    Assert.Contains("null", args);
  }
}
