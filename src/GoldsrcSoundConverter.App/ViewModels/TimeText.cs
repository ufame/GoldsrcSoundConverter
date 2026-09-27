namespace GoldsrcSoundConverter.App.ViewModels;

public static class TimeText
{
  public static string Format(double seconds)
  {
    var value = TimeSpan.FromSeconds(Math.Max(0, seconds));
    return value.TotalHours >= 1
      ? value.ToString(@"h\:mm\:ss\.fff")
      : value.ToString(@"mm\:ss\.fff");
  }
}

public sealed record DisplayOption<T>(string Display, T Value);
