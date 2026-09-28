using System.Collections.ObjectModel;

namespace GoldsrcSoundConverter.App.Services;

public interface ILogBuffer
{
  ObservableCollection<string> Entries { get; }

  void Add(string message);
}
