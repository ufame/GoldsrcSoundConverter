using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeSettingsStore : ISettingsStore
{
  public AppSettings Settings { get; set; } = new();

  public AppSettings? LastSaved { get; private set; }

  public int SaveCount { get; private set; }

  public AppSettings Load()
  {
    return Settings;
  }

  public void Save(AppSettings settings)
  {
    LastSaved = settings;
    SaveCount++;
    Settings = settings;
  }
}
