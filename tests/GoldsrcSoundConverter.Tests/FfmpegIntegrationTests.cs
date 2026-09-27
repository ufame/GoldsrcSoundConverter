using System.Globalization;
using System.IO;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests;

public sealed class FfmpegIntegrationTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly string? _ffmpeg = FindFfmpeg();
  private readonly string? _ffprobe;

  public FfmpegIntegrationTests()
  {
    _ffprobe = _ffmpeg is null
      ? null
      : Path.Combine(Path.GetDirectoryName(_ffmpeg)!, "ffprobe.exe");
  }

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public async Task ConvertsToCs16WavMono22050()
  {
    if (!IsAvailable())
    {
      return;
    }

    var source = await GenerateSineAsync("sine", 1.0);
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 22050,
      Channels = TargetChannels.Mono,
      BitDepth = TargetBitDepth.Sixteen,
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };

    var outcome = await ConvertAsync(source, options);

    Assert.True(outcome.Success, outcome.Error);
    Assert.NotNull(outcome.OutputPath);
    Assert.Equal(Path.Combine(_temp.Path, "sine_1.wav"), outcome.OutputPath);

    var header = WavHeader.Read(outcome.OutputPath!);
    Assert.Equal(1, header.AudioFormat);
    Assert.Equal(1, header.Channels);
    Assert.Equal(22050, header.SampleRate);
    Assert.Equal(16, header.BitsPerSample);

    var info = await AudioProbe.ProbeAsync(_ffprobe!, outcome.OutputPath!);
    Assert.InRange(info.Duration.TotalSeconds, 0.95, 1.05);
  }

  [Fact]
  public async Task ConvertsToCbrMp3WithoutId3()
  {
    if (!IsAvailable())
    {
      return;
    }

    var source = await GenerateSineAsync("music", 1.0);
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      Mp3BitrateKbps = 128,
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };

    var outcome = await ConvertAsync(source, options);

    Assert.True(outcome.Success, outcome.Error);
    Assert.NotNull(outcome.OutputPath);
    Assert.EndsWith(".mp3", outcome.OutputPath);

    var bytes = File.ReadAllBytes(outcome.OutputPath!);
    Assert.False(bytes.Length > 3 && bytes[0] == (byte)'I' && bytes[1] == (byte)'D' && bytes[2] == (byte)'3');
    if (bytes.Length > 128)
    {
      Assert.False(bytes[^128] == (byte)'T' && bytes[^127] == (byte)'A' && bytes[^126] == (byte)'G');
    }

    var info = await AudioProbe.ProbeAsync(_ffprobe!, outcome.OutputPath!);
    Assert.Equal("mp3", info.CodecName);
    Assert.Equal(44100, info.SampleRate);
    Assert.Equal(2, info.Channels);
    Assert.InRange(info.BitRate, 110_000, 140_000);
    Assert.InRange(info.Duration.TotalSeconds, 0.9, 1.1);
  }

  [Fact]
  public async Task TrimsToSelectedRange()
  {
    if (!IsAvailable())
    {
      return;
    }

    var source = await GenerateSineAsync("full", 3.0);
    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 22050,
      Channels = TargetChannels.Mono,
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };

    var job = new ConversionJob(
      0,
      source,
      null,
      TimeSpan.FromSeconds(1),
      TimeSpan.FromSeconds(2));

    var outcome = await new AudioConverter(_ffmpeg!, _ffprobe!).ConvertAsync(job, options, null);

    Assert.True(outcome.Success, outcome.Error);
    var info = await AudioProbe.ProbeAsync(_ffprobe!, outcome.OutputPath!);
    Assert.InRange(info.Duration.TotalSeconds, 0.9, 1.1);
  }

  [Fact]
  public async Task NormalizesQuietSource()
  {
    if (!IsAvailable())
    {
      return;
    }

    var source = await GenerateSineAsync("quiet", 1.0);
    var sourceVolume = await MeasureMaxVolumeDbAsync(source);
    Assert.True(sourceVolume < -10, $"Исходник не тихий: {sourceVolume} dB");

    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Wav,
      SampleRate = 22050,
      Channels = TargetChannels.Mono,
      NormalizePeak = true,
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };

    var outcome = await ConvertAsync(source, options);

    Assert.True(outcome.Success, outcome.Error);
    var outputVolume = await MeasureMaxVolumeDbAsync(outcome.OutputPath!);
    Assert.InRange(outputVolume, -1.5, 0.0);
  }

  [Fact]
  public async Task ConvertsOggToMp3()
  {
    if (!IsAvailable())
    {
      return;
    }

    var sine = await GenerateSineAsync("source", 1.0);
    var ogg = Path.Combine(_temp.Path, "clip.ogg");
    var transcode = await FfmpegRunner.RunAsync(_ffmpeg!, new[]
    {
      "-hide_banner", "-nostdin", "-y",
      "-i", sine,
      "-ar", "44100", "-ac", "2",
      "-c:a", "libvorbis",
      ogg,
    });
    Assert.True(transcode.Success, transcode.StandardError);

    var options = new ConversionOptions
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      Mp3BitrateKbps = 128,
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };

    var outcome = await ConvertAsync(ogg, options);

    Assert.True(outcome.Success, outcome.Error);
    var info = await AudioProbe.ProbeAsync(_ffprobe!, outcome.OutputPath!);
    Assert.Equal("mp3", info.CodecName);
    Assert.InRange(info.Duration.TotalSeconds, 0.9, 1.1);
  }

  [Fact]
  public async Task ExtractsWaveformData()
  {
    if (!IsAvailable())
    {
      return;
    }

    var source = await GenerateSineAsync("wave", 1.0);
    var waveform = await WaveformExtractor.ExtractAsync(_ffmpeg!, source);

    Assert.True(waveform.BucketCount > 100);
    Assert.InRange(waveform.Duration.TotalSeconds, 0.9, 1.1);
    Assert.True(waveform.Maxs.Max() > 0.05f);
    Assert.True(waveform.Mins.Min() < -0.05f);
  }

  private bool IsAvailable()
  {
    return _ffmpeg is not null && _ffprobe is not null && File.Exists(_ffprobe);
  }

  private async Task<ConversionOutcome> ConvertAsync(string source, ConversionOptions options)
  {
    var converter = new AudioConverter(_ffmpeg!, _ffprobe!);
    return await converter.ConvertAsync(new ConversionJob(0, source, null, null, null), options, null);
  }

  private async Task<string> GenerateSineAsync(string name, double seconds, string? volume = null)
  {
    var path = Path.Combine(_temp.Path, name + ".wav");
    var arguments = new List<string>
    {
      "-hide_banner",
      "-nostdin",
      "-y",
      "-f", "lavfi",
      "-i", $"sine=frequency=440:duration={seconds.ToString(CultureInfo.InvariantCulture)}",
      "-ar", "48000",
      "-ac", "2",
    };

    if (volume is not null)
    {
      arguments.Add("-af");
      arguments.Add($"volume={volume}");
    }

    arguments.Add(path);

    var result = await FfmpegRunner.RunAsync(_ffmpeg!, arguments);
    Assert.True(result.Success, result.StandardError);
    Assert.True(File.Exists(path));
    return path;
  }

  private async Task<double> MeasureMaxVolumeDbAsync(string path)
  {
    var arguments = FfmpegArguments.BuildMeasureVolume(path, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    var result = await FfmpegRunner.RunAsync(_ffmpeg!, arguments);
    var match = System.Text.RegularExpressions.Regex.Match(
      result.StandardError,
      @"max_volume:\s*(-?\d+(?:\.\d+)?)\s*dB");

    Assert.True(match.Success, result.StandardError);
    return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
  }

  private static string? FindFfmpeg()
  {
    var fromEnvironment = Environment.GetEnvironmentVariable("GSC_FFMPEG");
    if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
    {
      return fromEnvironment;
    }

    var local = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "GoldsrcSoundConverter",
      "ffmpeg",
      "ffmpeg.exe");
    if (File.Exists(local))
    {
      return local;
    }

    var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
    foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
    {
      try
      {
        var candidate = Path.Combine(directory.Trim(), "ffmpeg.exe");
        if (File.Exists(candidate))
        {
          return candidate;
        }
      }
      catch
      {
      }
    }

    return null;
  }
}

internal static class WavHeader
{
  public static (int AudioFormat, int Channels, int SampleRate, int BitsPerSample) Read(string path)
  {
    using var reader = new BinaryReader(File.OpenRead(path));

    if (new string(reader.ReadChars(4)) != "RIFF")
    {
      throw new InvalidOperationException("Не найден RIFF заголовок.");
    }

    reader.ReadInt32();
    if (new string(reader.ReadChars(4)) != "WAVE")
    {
      throw new InvalidOperationException("Не найден WAVE заголовок.");
    }

    while (reader.BaseStream.Position < reader.BaseStream.Length - 8)
    {
      var chunkId = new string(reader.ReadChars(4));
      var size = reader.ReadInt32();

      if (chunkId == "fmt ")
      {
        var audioFormat = reader.ReadInt16();
        var channels = reader.ReadInt16();
        var sampleRate = reader.ReadInt32();
        reader.ReadInt32();
        reader.ReadInt16();
        var bits = reader.ReadInt16();
        return (audioFormat, channels, sampleRate, bits);
      }

      reader.BaseStream.Seek(size, SeekOrigin.Current);
    }

    throw new InvalidOperationException("Не найден fmt-чанк.");
  }
}
