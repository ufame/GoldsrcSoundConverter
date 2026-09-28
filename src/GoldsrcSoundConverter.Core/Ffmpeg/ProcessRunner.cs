using System.Diagnostics;
using System.Text;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed class ProcessRunner : IProcessRunner
{
  public async Task<ProcessResult> RunAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Action<string>? onStandardOutputLine = null,
    Action<string>? onStandardErrorLine = null,
    CancellationToken cancellationToken = default)
  {
    var startInfo = BuildStartInfo(executablePath, arguments);
    using var process = new Process { StartInfo = startInfo };

    var standardOutput = new StringBuilder();
    var standardError = new StringBuilder();

    process.OutputDataReceived += (_, e) =>
    {
      if (e.Data is null)
      {
        return;
      }

      lock (standardOutput)
      {
        standardOutput.AppendLine(e.Data);
      }

      onStandardOutputLine?.Invoke(e.Data);
    };

    process.ErrorDataReceived += (_, e) =>
    {
      if (e.Data is null)
      {
        return;
      }

      lock (standardError)
      {
        standardError.AppendLine(e.Data);
      }

      onStandardErrorLine?.Invoke(e.Data);
    };

    if (!process.Start())
    {
      throw new InvalidOperationException($"Не удалось запустить процесс: {executablePath}");
    }

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    try
    {
      await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      TryKill(process);
      throw;
    }

    process.WaitForExit();
    return new ProcessResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
  }

  public async Task<string> RunBinaryAsync(
    string executablePath,
    IReadOnlyList<string> arguments,
    Func<Stream, CancellationToken, Task> standardOutputConsumer,
    CancellationToken cancellationToken = default)
  {
    var startInfo = BuildStartInfo(executablePath, arguments);
    using var process = new Process { StartInfo = startInfo };

    var standardError = new StringBuilder();
    process.ErrorDataReceived += (_, e) =>
    {
      if (e.Data is null)
      {
        return;
      }

      lock (standardError)
      {
        standardError.AppendLine(e.Data);
      }
    };

    if (!process.Start())
    {
      throw new InvalidOperationException($"Не удалось запустить процесс: {executablePath}");
    }

    process.BeginErrorReadLine();

    try
    {
      await standardOutputConsumer(process.StandardOutput.BaseStream, cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      TryKill(process);
      throw;
    }
    catch
    {
      TryKill(process);
      throw;
    }

    try
    {
      await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
      TryKill(process);
      throw;
    }

    process.WaitForExit();
    return standardError.ToString();
  }

  private static ProcessStartInfo BuildStartInfo(string executablePath, IReadOnlyList<string> arguments)
  {
    var startInfo = new ProcessStartInfo(executablePath)
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
      StandardOutputEncoding = Encoding.UTF8,
      StandardErrorEncoding = Encoding.UTF8,
    };

    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    return startInfo;
  }

  private static void TryKill(Process process)
  {
    try
    {
      if (!process.HasExited)
      {
        process.Kill(entireProcessTree: true);
      }
    }
    catch
    {
    }
  }
}
