namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
  public bool Success => ExitCode == 0;
}
