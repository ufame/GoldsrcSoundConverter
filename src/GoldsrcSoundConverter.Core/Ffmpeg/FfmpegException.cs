namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed class FfmpegException : Exception
{
  public FfmpegException(string message, string? standardError = null)
    : base(message)
  {
    StandardError = standardError;
  }

  public string? StandardError { get; }

  public string ShortMessage
  {
    get
    {
      if (string.IsNullOrWhiteSpace(StandardError))
      {
        return Message;
      }

      var lines = StandardError
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
      return lines.Length > 0 ? lines[^1] : Message;
    }
  }
}
