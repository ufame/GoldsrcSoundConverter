using GoldsrcSoundConverter.Core.Files;

namespace GoldsrcSoundConverter.Tests;

public sealed class FileNameSanitizerTests
{
  [Theory]
  [InlineData("Привет мир", "privet_mir")]
  [InlineData("трек №1", "trek_n1")]
  [InlineData("Ёжик", "ezhik")]
  [InlineData("Съешь ещё_этих булок", "sesh_esche_etih_bulok")]
  [InlineData("a<>b", "a_b")]
  [InlineData("...", "sound")]
  [InlineData("", "sound")]
  [InlineData("  spaced  out  ", "spaced_out")]
  public void SanitizesToAsciiLowercase(string input, string expected)
  {
    Assert.Equal(expected, FileNameSanitizer.Sanitize(input, toAscii: true, lowercase: true));
  }

  [Fact]
  public void KeepsCaseAndAsciiWhenRequested()
  {
    Assert.Equal("Track_01", FileNameSanitizer.Sanitize("Track 01", toAscii: true, lowercase: false));
  }

  [Fact]
  public void KeepsCyrillicWhenTransliterationDisabled()
  {
    Assert.Equal("трек_1", FileNameSanitizer.Sanitize("трек 1", toAscii: false, lowercase: false));
  }

  [Theory]
  [InlineData("Привет", "Привет")]
  [InlineData("Українська", "Українська")]
  [InlineData("中文", "中文")]
  [InlineData("日本語", "日本語")]
  public void KeepsUnicodeWhenTransliterationDisabled(string input, string expected)
  {
    Assert.Equal(expected, FileNameSanitizer.Sanitize(input, toAscii: false, lowercase: false));
  }

  [Theory]
  [InlineData("CON", "_CON")]
  [InlineData("con", "_con")]
  [InlineData("CON.wav", "_CON.wav")]
  [InlineData("PRN", "_PRN")]
  [InlineData("AUX", "_AUX")]
  [InlineData("NUL", "_NUL")]
  [InlineData("COM1", "_COM1")]
  [InlineData("com9", "_com9")]
  [InlineData("LPT1", "_LPT1")]
  [InlineData("lpt9", "_lpt9")]
  public void AvoidsReservedWindowsNames(string input, string expected)
  {
    var result = FileNameSanitizer.Sanitize(input, toAscii: true, lowercase: false);

    Assert.Equal(expected, result);
    Assert.DoesNotContain(result, Path.GetInvalidFileNameChars());
  }

  [Theory]
  [InlineData("my.CON")]
  [InlineData("sound")]
  [InlineData("connection")]
  public void KeepsRegularNamesThatContainReservedLookingWords(string input)
  {
    Assert.Equal(input, FileNameSanitizer.Sanitize(input, toAscii: true, lowercase: false));
  }

  [Theory]
  [InlineData(".")]
  [InlineData("..")]
  [InlineData("...")]
  [InlineData("....")]
  public void DotsOnlyBecomePlaceholder(string input)
  {
    Assert.Equal("sound", FileNameSanitizer.Sanitize(input, toAscii: true, lowercase: false));
  }

  [Fact]
  public void ReplacesInvalidFileNameCharacters()
  {
    var invalid = Path.GetInvalidFileNameChars().First();
    var result = FileNameSanitizer.Sanitize($"a{invalid}b", toAscii: true, lowercase: false);
    Assert.DoesNotContain(invalid, result);
  }

  [Theory]
  [InlineData(":", "a_b")]
  [InlineData("*", "a_b")]
  [InlineData("?", "a_b")]
  [InlineData("\"", "a_b")]
  [InlineData("<", "a_b")]
  [InlineData(">", "a_b")]
  [InlineData("|", "a_b")]
  [InlineData("/", "a_b")]
  [InlineData("\\", "a_b")]
  public void ReplacesKnownInvalidCharacters(string invalidChar, string expected)
  {
    Assert.Equal(expected, FileNameSanitizer.Sanitize($"a{invalidChar}b", toAscii: true, lowercase: false));
  }

  [Fact]
  public void NeverProducesInvalidWindowsNameForUnicodeInput()
  {
    var invalid = Path.GetInvalidFileNameChars();
    var inputs = new[] { "中文", "日本語", "😀", "Привет", "Українська", "a:b*c?d\"e<f>g|h/i\\j", new string('x', 300) };

    foreach (var input in inputs)
    {
      foreach (var toAscii in new[] { true, false })
      {
        var result = FileNameSanitizer.Sanitize(input, toAscii, lowercase: false);
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.DoesNotContain(result, invalid);
        Assert.False(result.EndsWith('.'), $"«{result}» заканчивается точкой");
        Assert.False(result.EndsWith(' '), $"«{result}» заканчивается пробелом");
      }
    }
  }

  [Fact]
  public void LimitsLength()
  {
    var result = FileNameSanitizer.Sanitize(new string('a', 300), toAscii: true, lowercase: false);
    Assert.True(result.Length <= 100);
  }
}
