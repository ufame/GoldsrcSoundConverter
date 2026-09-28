using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IWaveformLoader
{
  Task EnsureLoadedAsync(QueueItemViewModel item, ConversionOptions options);
}
