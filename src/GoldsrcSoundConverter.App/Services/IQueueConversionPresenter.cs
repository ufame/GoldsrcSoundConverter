using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public interface IQueueConversionPresenter
{
  IReadOnlyList<ConversionWorkItem> BeginRun();

  IProgress<ConversionProgress> CreateProgressHandler(Action<double>? onOverallProgress = null);

  IProgress<ProbeResult> CreateProbeHandler(ConversionOptions options);

  void Complete(ConversionRunResult result);
}
