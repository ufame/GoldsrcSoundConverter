namespace GoldsrcSoundConverter.Core.Ffmpeg;

public interface IProcessRunner
{
  Task<ProcessResult> RunAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Action<string>? onStandardOutputLine = null,
    Action<string>? onStandardErrorLine = null,
    CancellationToken cancellationToken = default);

  Task<string> RunBinaryAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Func<Stream, CancellationToken, Task> standardOutputConsumer,
    CancellationToken cancellationToken = default);
}
