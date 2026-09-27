using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Tests;

public sealed class FfmpegProgressParserTests
{
  [Fact]
  public void ParsesMicrosecondsFraction()
  {
    var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(4));

    Assert.True(parser.TryParse("out_time_us=1000000", out var fraction));
    Assert.Equal(0.25, fraction, 3);

    Assert.True(parser.TryParse("out_time_ms=2000000", out fraction));
    Assert.Equal(0.5, fraction, 3);
  }

  [Fact]
  public void ReportsCompletionOnEnd()
  {
    var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(2));

    Assert.True(parser.TryParse("progress=end", out var fraction));
    Assert.True(parser.IsCompleted);
    Assert.Equal(1, fraction, 3);
  }

  [Fact]
  public void IgnoresUnrelatedLines()
  {
    var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(2));

    Assert.False(parser.TryParse("frame=42", out var fraction));
    Assert.Equal(0, fraction);
  }

  [Fact]
  public void ClampsFractionBelowOne()
  {
    var parser = new FfmpegProgressParser(TimeSpan.FromSeconds(1));

    Assert.True(parser.TryParse("out_time_us=99000000", out var fraction));
    Assert.True(fraction <= 0.999);
  }
}
