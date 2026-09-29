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
  private readonly Dictionary<Guid, CancellationTokenSource> _probeTokens = new();

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
      var probeToken = new CancellationTokenSource();
      _probeTokens[item.Id] = probeToken;
      Items.Add(item);
      _ = ProbeItemAsync(item, probeToken);
      added++;
    }

    return added;
  }

  public void Remove(QueueItemViewModel item)
  {
    if (_probeTokens.Remove(item.Id, out var probeToken))
    {
      probeToken.Cancel();
      probeToken.Dispose();
    }

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
    GC.SuppressFinalize(this);
  }

  private void CancelPendingProbes()
  {
    foreach (var probeToken in _probeTokens.Values)
    {
      probeToken.Cancel();
      probeToken.Dispose();
    }

    _probeTokens.Clear();
  }

  private async Task ProbeItemAsync(
    QueueItemViewModel item,
    CancellationTokenSource probeToken)
  {
    if (item.Info is not null)
    {
      _probeTokens.Remove(item.Id);
      probeToken.Dispose();
      return;
    }

    try
    {
      var result = await _conversion
        .TryProbeAsync(item.Id, item.SourcePath, probeToken.Token)
        .ConfigureAwait(true);

      if (result is null || !Items.Contains(item))
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
      if (!probeToken.IsCancellationRequested)
      {
        _log.Add($"Не удалось проанализировать {item.FileName}: {ex.Message}");
      }
    }
    finally
    {
      _probeTokens.Remove(item.Id);
      probeToken.Dispose();
    }
  }
}
