using System.Collections.ObjectModel;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IQueueManager : IDisposable
{
  ObservableCollection<QueueItemViewModel> Items { get; }

  event EventHandler<QueueItemViewModel>? ItemProbed;

  int Add(IEnumerable<string> paths);

  void Remove(QueueItemViewModel item);

  void Clear();

  void RecalculateTargetSizes(ConversionOptions options);
}
