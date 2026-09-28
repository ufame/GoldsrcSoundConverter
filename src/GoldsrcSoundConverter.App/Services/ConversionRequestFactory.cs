using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class ConversionRequestFactory : IConversionRequestFactory
{
  public ConversionOptions CreateOptions(ConversionSettings settings)
  {
    return new ConversionOptions
    {
      Format = settings.Format,
      SampleRate = settings.SampleRate,
      Channels = settings.Channels,
      BitDepth = settings.BitDepth,
      Mp3BitrateKbps = settings.Mp3BitrateKbps,
      NormalizePeak = settings.NormalizePeak,
      AsciiNames = settings.AsciiNames,
      LowercaseNames = settings.LowercaseNames,
      PreserveStructure = settings.PreserveStructure,
      CollisionPolicy = settings.CollisionPolicy,
      Parallelism = settings.Parallelism,
      OutputDirectory = settings.OutputDirectory,
    };
  }

  public ConversionOptions CreatePreviewOptions(ConversionSettings settings, string previewDirectory)
  {
    return CreateOptions(settings) with
    {
      OutputDirectory = previewDirectory,
      AsciiNames = false,
      LowercaseNames = false,
      CollisionPolicy = CollisionPolicy.Overwrite,
      Parallelism = 1,
    };
  }

  public ConversionWorkItem CreateWorkItem(QueueItemViewModel item)
  {
    return new ConversionWorkItem(
      item.Id,
      item.SourcePath,
      item.SourceRoot,
      item.HasTrim ? TimeSpan.FromSeconds(item.TrimStartSeconds) : null,
      item.HasTrim ? TimeSpan.FromSeconds(item.TrimEndSeconds) : null,
      item.Info);
  }

  public IReadOnlyList<ConversionWorkItem> CreateWorkItems(IEnumerable<QueueItemViewModel> items)
  {
    return items.Select(CreateWorkItem).ToArray();
  }
}
