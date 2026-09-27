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
  public void RemovesCyrillicWhenTransliterationDisabled()
  {
    Assert.Equal("1", FileNameSanitizer.Sanitize("трек 1", toAscii: false, lowercase: true));
  }

  [Fact]
  public void ReplacesInvalidFileNameCharacters()
  {
    var invalid = Path.GetInvalidFileNameChars().First();
    var result = FileNameSanitizer.Sanitize($"a{invalid}b", toAscii: true, lowercase: false);
    Assert.DoesNotContain(invalid, result);
  }

  [Fact]
  public void LimitsLength()
  {
    var result = FileNameSanitizer.Sanitize(new string('a', 300), toAscii: true, lowercase: false);
    Assert.True(result.Length <= 100);
  }
}
