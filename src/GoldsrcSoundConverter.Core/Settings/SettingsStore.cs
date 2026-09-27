using System.Text.Json;
using System.Text.Json.Serialization;

namespace GoldsrcSoundConverter.Core.Settings;

public sealed class SettingsStore
{
  private static readonly JsonSerializerOptions Options = new()
  {
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() },
  };

  public SettingsStore(string? filePath = null)
  {
    FilePath = filePath ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GoldsrcSoundConverter",
      "settings.json");
  }

  public string FilePath { get; }

  public AppSettings Load()
  {
    try
    {
      if (!File.Exists(FilePath))
      {
        return new AppSettings();
      }

      var json = File.ReadAllText(FilePath);
      return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
    }
    catch
    {
      return new AppSettings();
    }
  }

  public void Save(AppSettings settings)
  {
    var directory = Path.GetDirectoryName(FilePath);
    if (!string.IsNullOrEmpty(directory))
    {
      Directory.CreateDirectory(directory);
    }

    File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
  }
}
