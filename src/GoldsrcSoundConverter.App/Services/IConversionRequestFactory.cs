using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IConversionRequestFactory
{
  ConversionOptions CreateOptions(ConversionSettings settings);

  ConversionOptions CreatePreviewOptions(ConversionSettings settings, string previewDirectory);

  ConversionWorkItem CreateWorkItem(QueueItemViewModel item);

  IReadOnlyList<ConversionWorkItem> CreateWorkItems(IEnumerable<QueueItemViewModel> items);
}
