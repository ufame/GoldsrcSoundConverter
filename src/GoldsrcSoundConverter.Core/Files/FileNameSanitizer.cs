using System.Text;
using System.Text.RegularExpressions;

namespace GoldsrcSoundConverter.Core.Files;

public static class FileNameSanitizer
{
  private const int MaxLength = 100;

  private static readonly Dictionary<char, string> Transliteration = new()
  {
    ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e",
    ['ё'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k",
    ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r",
    ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts",
    ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "",
    ['э'] = "e", ['ю'] = "yu", ['я'] = "ya", ['і'] = "i", ['ї'] = "yi", ['є'] = "ye",
    ['ґ'] = "g", ['№'] = "n",
  };

  private static readonly Regex UnderscoreRuns = new(@"_+", RegexOptions.Compiled);
  private static readonly Regex InvalidRuns = new(@"[^A-Za-z0-9\-_.]+", RegexOptions.Compiled);

  public static string Sanitize(string name, bool toAscii, bool lowercase)
  {
    if (string.IsNullOrWhiteSpace(name))
    {
      return "sound";
    }

    var builder = new StringBuilder(name.Length);
    foreach (var ch in name)
    {
      if (toAscii && Transliteration.TryGetValue(char.ToLowerInvariant(ch), out var replacement))
      {
        builder.Append(char.IsUpper(ch) ? Capitalize(replacement) : replacement);
        continue;
      }

      if (ch < 128)
      {
        builder.Append(ch);
      }
    }

    var result = InvalidRuns.Replace(builder.ToString(), "_");
    result = UnderscoreRuns.Replace(result, "_").Trim('_', '.', '-', ' ');

    if (result.Length > MaxLength)
    {
      result = result[..MaxLength].TrimEnd('_', '.', '-', ' ');
    }

    if (result.Length == 0)
    {
      result = "sound";
    }

    return lowercase ? result.ToLowerInvariant() : result;
  }

  private static string Capitalize(string value)
  {
    return value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
  }
}
