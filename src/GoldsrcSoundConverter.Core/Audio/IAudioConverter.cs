using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Audio;

public interface IAudioConverter
{
  Task<ConversionOutcome> ConvertAsync(
    ConversionJob job,
    ConversionOptions options,
    AudioInfo? knownInfo,
    IProgress<ConversionProgress>? progress = null,
    Action<string>? log = null,
    CancellationToken cancellationToken = default);
}
