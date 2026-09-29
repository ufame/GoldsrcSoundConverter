using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeSettingsStore : ISettingsStore
{
  public AppSettings Settings { get; set; } = new();

  public AppSettings? LastSaved { get; private set; }

  public int SaveCount { get; private set; }

  public Exception? SaveException { get; set; }

  public AppSettings Load()
  {
    return Settings;
  }

  public void Save(AppSettings settings)
  {
    if (SaveException is not null)
    {
      throw SaveException;
    }

    LastSaved = settings;
    SaveCount++;
    Settings = settings;
  }
}
