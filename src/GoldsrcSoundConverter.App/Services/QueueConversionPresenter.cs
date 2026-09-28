using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class QueueConversionPresenter : IQueueConversionPresenter
{
  private readonly IQueueManager _queue;

  private QueueItemViewModel[] _items = Array.Empty<QueueItemViewModel>();
  private Dictionary<Guid, QueueItemViewModel> _itemsById = new();

  public QueueConversionPresenter(IQueueManager queue)
  {
    _queue = queue;
  }

  public IReadOnlyList<ConversionWorkItem> BeginRun()
  {
    _items = _queue.Items.ToArray();
    _itemsById = _items.ToDictionary(item => item.Id);

    foreach (var item in _items)
    {
      item.Progress = 0;
      item.Error = null;
      item.OutputPath = null;
      item.Stage = ConversionStage.Pending;
    }

    return _items.Select(item => item.ToWorkItem()).ToArray();
  }

  public IProgress<ConversionProgress> CreateProgressHandler(Action<double>? onOverallProgress = null)
  {
    return new ActionProgress<ConversionProgress>(
      progress => ApplyProgress(progress, onOverallProgress));
  }

  public IProgress<ProbeResult> CreateProbeHandler(ConversionOptions options)
  {
    return new ActionProgress<ProbeResult>(probe => ApplyProbe(probe, options));
  }

  public void Complete(ConversionRunResult result)
  {
    if (result.Summary.Cancelled)
    {
      ResetInFlight();
      return;
    }

    ApplyOutcomes(result.Outcomes);
  }

  private void ApplyProgress(ConversionProgress progress, Action<double>? onOverallProgress)
  {
    if (!_itemsById.TryGetValue(progress.JobId, out var item))
    {
      return;
    }

    item.Stage = progress.Stage;
    item.Progress = progress.Percent;

    if (progress.Stage == ConversionStage.Converting && onOverallProgress is not null)
    {
      var finished = _items.Count(i => i.Stage is ConversionStage.Completed
        or ConversionStage.Skipped
        or ConversionStage.Failed);
      onOverallProgress(_items.Length == 0
        ? 0
        : Math.Clamp((finished + progress.Percent) / _items.Length, 0, 1));
    }
  }

  private void ApplyProbe(ProbeResult probe, ConversionOptions options)
  {
    if (!_itemsById.TryGetValue(probe.Id, out var item) || item.Info is not null)
    {
      return;
    }

    item.Info = probe.Info;
    item.UpdateTargetSize(options);
  }

  private void ApplyOutcomes(IReadOnlyList<ConversionOutcome> outcomes)
  {
    foreach (var outcome in outcomes)
    {
      if (!_itemsById.TryGetValue(outcome.JobId, out var item))
      {
        continue;
      }

      if (outcome.Success && outcome.Skipped)
      {
        item.Stage = ConversionStage.Skipped;
      }
      else if (outcome.Success)
      {
        item.Stage = ConversionStage.Completed;
        item.Progress = 1;
        item.OutputPath = outcome.OutputPath;
      }
      else
      {
        item.Stage = ConversionStage.Failed;
        item.Error = outcome.Error;
      }
    }
  }

  private void ResetInFlight()
  {
    foreach (var item in _items.Where(i => i.Stage is ConversionStage.Pending
      or ConversionStage.Probing
      or ConversionStage.Normalizing
      or ConversionStage.Converting))
    {
      item.Stage = ConversionStage.Pending;
      item.Progress = 0;
    }
  }
}
