using System.Collections.ObjectModel;
using System.Globalization;

namespace GoldsrcSoundConverter.App.Services;

public sealed class LogBuffer : ILogBuffer
{
  public const int MaxEntries = 500;

  public ObservableCollection<string> Entries { get; } = new();

  public void Add(string message)
  {
    var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    Entries.Add($"[{timestamp}] {message}");

    while (Entries.Count > MaxEntries)
    {
      Entries.RemoveAt(0);
    }
  }
}
