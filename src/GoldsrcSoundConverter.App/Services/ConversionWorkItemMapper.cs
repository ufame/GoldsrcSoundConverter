using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public static class ConversionWorkItemMapper
{
  public static ConversionWorkItem ToWorkItem(this QueueItemViewModel item)
  {
    return new ConversionWorkItem(
      item.Id,
      item.SourcePath,
      item.SourceRoot,
      item.HasTrim ? TimeSpan.FromSeconds(item.TrimStartSeconds) : null,
      item.HasTrim ? TimeSpan.FromSeconds(item.TrimEndSeconds) : null,
      item.Info);
  }
}
