using System.IO;
using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeProcessRunner : IProcessRunner
{
  private readonly Func<Call, Response>? _responder;
  private readonly Queue<Response> _queued = new();

  public FakeProcessRunner(Func<Call, Response>? responder = null)
  {
    _responder = responder;
  }

  public List<Call> Calls { get; } = new();

  public void Enqueue(Response response)
  {
    _queued.Enqueue(response);
  }

  public Task<ProcessResult> RunAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Action<string>? onStandardOutputLine = null,
    Action<string>? onStandardErrorLine = null,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var call = new Call(executablePath, arguments);
    Calls.Add(call);
    var response = NextResponse(call);

    if (response.StandardOutput.Length > 0)
    {
      onStandardOutputLine?.Invoke(response.StandardOutput);
    }

    if (response.StandardError.Length > 0)
    {
      onStandardErrorLine?.Invoke(response.StandardError);
    }

    return Task.FromResult(new ProcessResult(response.ExitCode, response.StandardOutput, response.StandardError));
  }

  public async Task<string> RunBinaryAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Func<Stream, CancellationToken, Task> standardOutputConsumer,
    CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var call = new Call(executablePath, arguments);
    Calls.Add(call);
    var response = NextResponse(call);

    await using var stream = new MemoryStream();
    await standardOutputConsumer(stream, cancellationToken).ConfigureAwait(false);
    return response.StandardError;
  }

  private Response NextResponse(Call call)
  {
    if (_responder is not null)
    {
      return _responder(call);
    }

    return _queued.Count > 0 ? _queued.Dequeue() : new Response(0);
  }

  public sealed record Call(string ExecutablePath, IReadOnlyList<string> Arguments)
  {
    public bool IsProbe => ExecutablePath.Contains("ffprobe", StringComparison.OrdinalIgnoreCase);
  }

  public sealed record Response(int ExitCode, string StandardOutput = "", string StandardError = "");
}
