using GoldsrcSoundConverter.App.ViewModels;

namespace GoldsrcSoundConverter.App.Services;

public interface IWaveformLoader
{
  Task EnsureLoadedAsync(QueueItemViewModel item);
}
