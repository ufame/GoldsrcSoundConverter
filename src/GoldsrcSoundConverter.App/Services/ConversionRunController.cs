using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class ConversionRunController : IConversionRunController
{
  private readonly IConversionService _conversion;
  private readonly IConversionRequestFactory _requestFactory;
  private readonly ILogBuffer _log;

  private CancellationTokenSource? _cts;
  private bool _isBusy;

  public ConversionRunController(
    IConversionService conversion,
    IConversionRequestFactory requestFactory,
    ILogBuffer log)
  {
    _conversion = conversion;
    _requestFactory = requestFactory;
    _log = log;
  }

  public bool IsBusy => _isBusy;

  public event EventHandler? BusyChanged;

  public async Task<ConversionRunSummary> RunAsync(
    IReadOnlyList<QueueItemViewModel> items,
    ConversionOptions options,
    Action<double>? onOverallProgress = null,
    Action<BootstrapProgress>? onBootstrapProgress = null)
  {
    SetBusy(true);
    _cts?.Dispose();
    _cts = new CancellationTokenSource();
    var cancellationToken = _cts.Token;

    foreach (var item in items)
    {
      item.Progress = 0;
      item.Error = null;
      item.OutputPath = null;
      item.Stage = ConversionStage.Pending;
    }

    try
    {
      var workItems = _requestFactory.CreateWorkItems(items);
      var progress = new ActionProgress<ConversionProgress>(value => ApplyProgress(items, value, onOverallProgress));
      var probeProgress = new ActionProgress<ProbeResult>(value => ApplyProbe(items, value, options));
      var bootstrapProgress = onBootstrapProgress is null
        ? null
        : new ActionProgress<BootstrapProgress>(onBootstrapProgress);

      var result = await _conversion
        .ConvertAsync(workItems, options, progress, probeProgress, bootstrapProgress, _log.Add, cancellationToken)
        .ConfigureAwait(true);

      return ApplyOutcomes(items, result.Outcomes);
    }
    catch (OperationCanceledException)
    {
      foreach (var item in items.Where(i => i.Stage is ConversionStage.Pending
        or ConversionStage.Probing
        or ConversionStage.Normalizing
        or ConversionStage.Converting))
      {
        item.Stage = ConversionStage.Pending;
        item.Progress = 0;
      }

      return new ConversionRunSummary(0, 0, 0, Cancelled: true);
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

  private void SetBusy(bool value)
  {
    if (_isBusy == value)
    {
      return;
    }

    _isBusy = value;
    BusyChanged?.Invoke(this, EventArgs.Empty);
  }

  private sealed class ActionProgress<T> : IProgress<T>
  {
    private readonly Action<T> _action;

    public ActionProgress(Action<T> action)
    {
      _action = action;
    }

    public void Report(T value)
    {
      _action(value);
    }
  }

  private static void ApplyProgress(
    IReadOnlyList<QueueItemViewModel> items,
    ConversionProgress progress,
    Action<double>? onOverallProgress)
  {
    var item = items.FirstOrDefault(i => i.Id == progress.JobId);
    if (item is null)
    {
      return;
    }

    item.Stage = progress.Stage;
    item.Progress = progress.Percent;

    if (progress.Stage == ConversionStage.Converting && onOverallProgress is not null)
    {
      var finished = items.Count(i => i.Stage is ConversionStage.Completed
        or ConversionStage.Skipped
        or ConversionStage.Failed);
      onOverallProgress(items.Count == 0
        ? 0
        : Math.Clamp((finished + progress.Percent) / items.Count, 0, 1));
    }
  }

  private static void ApplyProbe(
    IReadOnlyList<QueueItemViewModel> items,
    ProbeResult probe,
    ConversionOptions options)
  {
    var item = items.FirstOrDefault(i => i.Id == probe.Id);
    if (item is null || item.Info is not null)
    {
      return;
    }

    item.Info = probe.Info;
    item.UpdateTargetSize(options);
  }

  private static ConversionRunSummary ApplyOutcomes(
    IReadOnlyList<QueueItemViewModel> items,
    IReadOnlyList<ConversionOutcome> outcomes)
  {
    var completed = 0;
    var failed = 0;
    var skipped = 0;

    foreach (var outcome in outcomes)
    {
      var item = items.FirstOrDefault(i => i.Id == outcome.JobId);
      if (item is null)
      {
        continue;
      }

      if (outcome.Success && outcome.Skipped)
      {
        item.Stage = ConversionStage.Skipped;
        skipped++;
      }
      else if (outcome.Success)
      {
        item.Stage = ConversionStage.Completed;
        item.Progress = 1;
        item.OutputPath = outcome.OutputPath;
        completed++;
      }
      else
      {
        item.Stage = ConversionStage.Failed;
        item.Error = outcome.Error;
        failed++;
      }
    }

    return new ConversionRunSummary(completed, failed, skipped);
  }
}
