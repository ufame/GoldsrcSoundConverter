namespace GoldsrcSoundConverter.App.Services;

public sealed record PlaybackResult(bool Success, string? Status = null)
{
  public static PlaybackResult Ok { get; } = new(true);
}
