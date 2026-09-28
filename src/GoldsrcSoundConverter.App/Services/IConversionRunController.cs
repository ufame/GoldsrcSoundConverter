using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed record ConversionRunSummary(int Completed, int Failed, int Skipped, bool Cancelled = false);

public sealed record ConversionRunResult(
  ConversionRunSummary Summary,
  IReadOnlyList<ConversionOutcome> Outcomes);

public interface IConversionRunController : IDisposable
{
  bool IsBusy { get; }

  event EventHandler? BusyChanged;

  Task<ConversionRunResult> RunAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    ConversionOptions options,
    IProgress<ConversionProgress>? progress = null,
    IProgress<ProbeResult>? probeProgress = null,
    IProgress<BootstrapProgress>? bootstrapProgress = null);

  void Cancel();
}
