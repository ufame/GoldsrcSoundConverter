using System.Collections.ObjectModel;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IQueueManager
{
  ObservableCollection<QueueItemViewModel> Items { get; }

  int Add(IEnumerable<string> paths, ConversionOptions options);

  void Remove(QueueItemViewModel item);

  void Clear();

  void RecalculateTargetSizes(ConversionOptions options);
}
