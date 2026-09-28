using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeRunController : IConversionRunController
{
  public bool IsBusy { get; set; }

  public bool Disposed { get; private set; }

  public event EventHandler? BusyChanged;

  public Task<ConversionRunSummary> RunAsync(
    IReadOnlyList<QueueItemViewModel> items,
    ConversionOptions options,
    Action<double>? onOverallProgress = null,
    Action<BootstrapProgress>? onBootstrapProgress = null)
  {
    throw new NotSupportedException("FakeRunController не запускает конвертацию.");
  }

  public void Cancel()
  {
  }

  public void Dispose()
  {
    Disposed = true;
  }

  public void RaiseBusyChanged()
  {
    BusyChanged?.Invoke(this, EventArgs.Empty);
  }
}
