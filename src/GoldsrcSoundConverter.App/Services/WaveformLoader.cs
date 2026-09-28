using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class WaveformLoader : IWaveformLoader
{
  private readonly IConversionService _conversion;
  private readonly ILogBuffer _log;

  public WaveformLoader(IConversionService conversion, ILogBuffer log)
  {
    _conversion = conversion;
    _log = log;
  }

  public async Task EnsureLoadedAsync(QueueItemViewModel item, ConversionOptions options)
  {
    if (item.Waveform is not null || item.IsWaveformLoading)
    {
      return;
    }

    item.IsWaveformLoading = true;
    try
    {
      var data = await _conversion
        .TryExtractWaveformAsync(item.SourcePath)
        .ConfigureAwait(true);
      if (data is null)
      {
        return;
      }

      item.Waveform = data;
      if (item.Info is null)
      {
        item.UpdateTargetSize(options);
      }
    }
    catch (Exception ex)
    {
      _log.Add($"Волновая форма недоступна для {item.FileName}: {ex.Message}");
    }
    finally
    {
      item.IsWaveformLoading = false;
    }
  }
}
