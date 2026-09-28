using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class ConversionRunController : IConversionRunController
{
  private readonly IConversionService _conversion;
  private readonly ILogBuffer _log;

  private CancellationTokenSource? _cts;
  private bool _isBusy;

  public ConversionRunController(IConversionService conversion, ILogBuffer log)
  {
    _conversion = conversion;
    _log = log;
  }

  public bool IsBusy => _isBusy;

  public event EventHandler? BusyChanged;

  public async Task<ConversionRunResult> RunAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    ConversionOptions options,
    IProgress<ConversionProgress>? progress = null,
    IProgress<ProbeResult>? probeProgress = null,
    IProgress<BootstrapProgress>? bootstrapProgress = null)
  {
    SetBusy(true);
    _cts?.Dispose();
    _cts = new CancellationTokenSource();
    var cancellationToken = _cts.Token;

    try
    {
      var result = await _conversion
        .ConvertAsync(workItems, options, progress, probeProgress, bootstrapProgress, _log.Add, cancellationToken)
        .ConfigureAwait(true);

      return new ConversionRunResult(Summarize(result.Outcomes), result.Outcomes);
    }
    catch (OperationCanceledException)
    {
      return new ConversionRunResult(
        new ConversionRunSummary(0, 0, 0, Cancelled: true),
        Array.Empty<ConversionOutcome>());
    }
    finally
    {
      SetBusy(false);
    }
  }

  public void Cancel()
  {
    _cts?.Cancel();
  }

  public void Dispose()
  {
    _cts?.Dispose();
    GC.SuppressFinalize(this);
  }

  private static ConversionRunSummary Summarize(IReadOnlyList<ConversionOutcome> outcomes)
  {
    var completed = outcomes.Count(outcome => outcome.Success && !outcome.Skipped);
    var skipped = outcomes.Count(outcome => outcome.Success && outcome.Skipped);
    var failed = outcomes.Count(outcome => !outcome.Success);
    return new ConversionRunSummary(completed, failed, skipped);
  }

  private void SetBusy(bool value)
  {
    if (_isBusy == value)
    {
      return;
    }

    _isBusy = value;
    BusyChanged?.Invoke(this, EventArgs.Empty);
  }
}
