using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class PresetCatalog : IPresetCatalog
{
  private static readonly Cs16Preset Custom = Cs16Presets.ById(Cs16Presets.CustomId)!;

  public IReadOnlyList<Cs16Preset> ForFormat(OutputAudioFormat format)
  {
    return Cs16Presets.ForFormat(format);
  }

  public Cs16Preset Resolve(ConversionOptions options)
  {
    return Cs16Presets.Match(options) ?? Custom;
  }
}
