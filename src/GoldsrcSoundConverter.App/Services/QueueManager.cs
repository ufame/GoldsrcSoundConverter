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

  public QueueManager(IConversionService conversion, ILogBuffer log)
  {
    _conversion = conversion;
    _log = log;
  }

  public ObservableCollection<QueueItemViewModel> Items { get; } = new();

  public int Add(IEnumerable<string> paths, ConversionOptions options)
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
      item.UpdateTargetSize(options);
      _ = ProbeItemAsync(item, options);
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
    Items.Clear();
  }

  public void RecalculateTargetSizes(ConversionOptions options)
  {
    foreach (var item in Items)
    {
      item.UpdateTargetSize(options);
    }
  }

  private async Task ProbeItemAsync(QueueItemViewModel item, ConversionOptions options)
  {
    if (item.Info is not null)
    {
      return;
    }

    try
    {
      var result = await _conversion.TryProbeAsync(item.Id, item.SourcePath).ConfigureAwait(true);
      if (result is null)
      {
        return;
      }

      item.Info = result.Info;
      item.UpdateTargetSize(options);
    }
    catch (Exception ex)
    {
      _log.Add($"Не удалось проанализировать {item.FileName}: {ex.Message}");
    }
  }
}
