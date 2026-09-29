using System.Collections.ObjectModel;
using GoldsrcSoundConverter.App.Services;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed class LogViewModel
{
  private readonly ILogBuffer _log;

  public LogViewModel(ILogBuffer log)
  {
    _log = log;
  }

  public ObservableCollection<string> Entries => _log.Entries;

  public void Append(string message)
  {
    _log.Add(message);
  }
}
