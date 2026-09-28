using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed record ConversionRunSummary(int Completed, int Failed, int Skipped, bool Cancelled = false);

public interface IConversionRunController : IDisposable
{
  bool IsBusy { get; }

  event EventHandler? BusyChanged;

  Task<ConversionRunSummary> RunAsync(
    IReadOnlyList<QueueItemViewModel> items,
    ConversionOptions options,
    Action<double>? onOverallProgress = null,
    Action<BootstrapProgress>? onBootstrapProgress = null);

  void Cancel();
}
