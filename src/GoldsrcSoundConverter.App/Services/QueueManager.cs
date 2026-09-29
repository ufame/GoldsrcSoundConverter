using System.Collections.ObjectModel;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Files;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class QueueManager : IQueueManager
{
  private readonly IConversionService _conversion;
  private readonly ILogBuffer _log;

  private CancellationTokenSource _probeCts = new();
  private bool _disposed;

  public QueueManager(IConversionService conversion, ILogBuffer log)
  {
    _conversion = conversion;
    _log = log;
  }

  public ObservableCollection<QueueItemViewModel> Items { get; } = new();

  public event EventHandler<QueueItemViewModel>? ItemProbed;

  public int Add(IEnumerable<string> paths)
  {
    var added = 0;
    var candidates = InputFileDiscoverer.Discover(
      paths,
      (path, ex) => _log.Add($"Не удалось добавить «{path}»: {ex.Message}"));

    foreach (var candidate in candidates)
    {
      if (Items.Any(i => string.Equals(i.SourcePath, candidate.FilePath, StringComparison.OrdinalIgnoreCase)))
      {
        continue;
      }

      var item = new QueueItemViewModel(candidate.FilePath, candidate.SourceRoot);
      Items.Add(item);
      _ = ProbeItemAsync(item, _probeCts.Token);
      added++;
    }

    return added;
  }

  public void Remove(QueueItemViewModel item)
  {
    Items.Remove(item);
  }

  public void Clear()
  {
    if (!_disposed)
    {
      CancelPendingProbes();
    }

    Items.Clear();
  }

  public void RecalculateTargetSizes(ConversionOptions options)
  {
    foreach (var item in Items)
    {
      item.UpdateTargetSize(options);
    }
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    CancelPendingProbes();
    _disposed = true;
    _probeCts.Dispose();
    GC.SuppressFinalize(this);
  }

  private void CancelPendingProbes()
  {
    var previous = _probeCts;
    _probeCts = new CancellationTokenSource();
    previous.Cancel();
    previous.Dispose();
  }

  private async Task ProbeItemAsync(
    QueueItemViewModel item,
    CancellationToken cancellationToken)
  {
    if (item.Info is not null)
    {
      return;
    }

    try
    {
      var result = await _conversion
        .TryProbeAsync(item.Id, item.SourcePath, cancellationToken)
        .ConfigureAwait(true);

      if (result is null || cancellationToken.IsCancellationRequested || !Items.Contains(item))
      {
        return;
      }

      item.Info = result.Info;
      ItemProbed?.Invoke(this, item);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      if (!cancellationToken.IsCancellationRequested)
      {
        _log.Add($"Не удалось проанализировать {item.FileName}: {ex.Message}");
      }
    }
  }
}
