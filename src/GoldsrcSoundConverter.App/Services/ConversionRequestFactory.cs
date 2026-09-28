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

}
