using System.Text.Json;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.Tests;

public sealed class SettingsStoreTests : IDisposable
{
  private readonly TempDirectory _temp = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private string SettingsPath => Path.Combine(_temp.Path, "settings.json");

  [Fact]
  public void MissingFileReturnsDefaults()
  {
    var store = new SettingsStore(SettingsPath);

    var settings = store.Load();

    Assert.Equal(22050, settings.SampleRate);
    Assert.Equal(OutputAudioFormat.Wav, settings.Format);
  }

  [Fact]
  public void SaveAndLoadRoundtrip()
  {
    var store = new SettingsStore(SettingsPath);
    var settings = new AppSettings
    {
      Format = OutputAudioFormat.Mp3,
      SampleRate = 44100,
      Channels = TargetChannels.Stereo,
      Mp3BitrateKbps = 192,
      CollisionPolicy = CollisionPolicy.Overwrite,
      Parallelism = 8,
      OutputDirectory = @"C:\out",
    };

    store.Save(settings);
    var loaded = new SettingsStore(SettingsPath).Load();

    Assert.Equal(OutputAudioFormat.Mp3, loaded.Format);
    Assert.Equal(44100, loaded.SampleRate);
    Assert.Equal(TargetChannels.Stereo, loaded.Channels);
    Assert.Equal(192, loaded.Mp3BitrateKbps);
    Assert.Equal(CollisionPolicy.Overwrite, loaded.CollisionPolicy);
    Assert.Equal(8, loaded.Parallelism);
    Assert.Equal(@"C:\out", loaded.OutputDirectory);
  }

  [Fact]
  public void MalformedJsonIsBackedUpAndDefaultsReturned()
  {
    File.WriteAllText(SettingsPath, "{ this is not json");
    var logged = new List<string>();
    var store = new SettingsStore(SettingsPath, logged.Add);

    var settings = store.Load();

    Assert.Equal(22050, settings.SampleRate);
    Assert.False(File.Exists(SettingsPath));
    Assert.True(File.Exists(store.BackupFilePath));
    Assert.Contains("not json", File.ReadAllText(store.BackupFilePath));
    Assert.Contains(logged, message => message.Contains("Не удалось прочитать настройки"));
  }

  [Fact]
  public void UnknownPropertiesAreIgnored()
  {
    File.WriteAllText(
      SettingsPath,
      """{ "SampleRate": 11025, "SomeFutureOption": true, "Nested": { "x": 1 } }""");

    var settings = new SettingsStore(SettingsPath).Load();

    Assert.Equal(11025, settings.SampleRate);
  }

  [Fact]
  public void AtomicSaveLeavesNoTempFile()
  {
    var store = new SettingsStore(SettingsPath);

    store.Save(new AppSettings());

    Assert.True(File.Exists(SettingsPath));
    Assert.False(File.Exists(SettingsPath + ".tmp"));
  }

  [Fact]
  public void RepeatedSaveOverwritesExistingFile()
  {
    var store = new SettingsStore(SettingsPath);
    store.Save(new AppSettings { SampleRate = 11025 });
    store.Save(new AppSettings { SampleRate = 44100 });

    var loaded = store.Load();

    Assert.Equal(44100, loaded.SampleRate);
    Assert.False(File.Exists(SettingsPath + ".tmp"));
  }

  [Fact]
  public void EmptyJsonObjectReturnsDefaults()
  {
    File.WriteAllText(SettingsPath, "{}");

    var settings = new SettingsStore(SettingsPath).Load();

    Assert.Equal(22050, settings.SampleRate);
  }

  [Fact]
  public void SavedFileIsValidJson()
  {
    var store = new SettingsStore(SettingsPath);
    store.Save(new AppSettings());

    using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
    Assert.True(document.RootElement.TryGetProperty("SampleRate", out _));
  }
}
