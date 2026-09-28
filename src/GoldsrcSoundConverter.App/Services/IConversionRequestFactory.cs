using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IConversionRequestFactory
{
  ConversionOptions CreateOptions(ConversionSettings settings);

  ConversionOptions CreatePreviewOptions(ConversionSettings settings, string previewDirectory);
}
