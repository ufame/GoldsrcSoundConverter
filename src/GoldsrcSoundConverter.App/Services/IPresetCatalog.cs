using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IPresetCatalog
{
  IReadOnlyList<Cs16Preset> ForFormat(OutputAudioFormat format);

  Cs16Preset Resolve(ConversionOptions options);
}
