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

  private readonly Action<string>? _log;

  public SettingsStore(string? filePath = null, Action<string>? log = null)
  {
    FilePath = filePath ?? Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GoldsrcSoundConverter",
      "settings.json");
    _log = log;
  }

  public string FilePath { get; }

  public string BackupFilePath => FilePath + ".bak";

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
    catch (Exception ex)
    {
      _log?.Invoke(
        $"Не удалось прочитать настройки: {ex.Message}. "
        + $"Повреждённый файл сохранён как {BackupFilePath}.");
      BackupCorruptFile();
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

    WriteAtomically(JsonSerializer.Serialize(settings, Options));
  }

  private void BackupCorruptFile()
  {
    try
    {
      if (File.Exists(FilePath))
      {
        File.Move(FilePath, BackupFilePath, overwrite: true);
      }
    }
    catch (Exception ex)
    {
      _log?.Invoke($"Не удалось создать резервную копию настроек: {ex.Message}");
    }
  }

  private void WriteAtomically(string json)
  {
    var tempPath = FilePath + ".tmp";
    try
    {
      using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
      using (var writer = new StreamWriter(stream))
      {
        writer.Write(json);
        writer.Flush();
        stream.Flush(flushToDisk: true);
      }

      if (File.Exists(FilePath))
      {
        try
        {
          File.Replace(tempPath, FilePath, destinationBackupFileName: null);
        }
        catch (IOException)
        {
          File.Move(tempPath, FilePath, overwrite: true);
        }
      }
      else
      {
        File.Move(tempPath, FilePath);
      }
    }
    catch
    {
      TryDelete(tempPath);
      throw;
    }
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }
    catch
    {
    }
  }
}
