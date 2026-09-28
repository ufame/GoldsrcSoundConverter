using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Audio;

public sealed class BatchConverter
{
  private readonly AudioConverter _converter;

  public BatchConverter(string ffmpegPath, string ffprobePath)
  {
    _converter = new AudioConverter(ffmpegPath, ffprobePath);
  }

  public async Task<IReadOnlyList<ConversionOutcome>> RunAsync(
    IReadOnlyList<ConversionJob> jobs,
    ConversionOptions options,
    IReadOnlyDictionary<Guid, AudioInfo>? knownInfos = null,
    IProgress<ConversionProgress>? progress = null,
    Action<string>? log = null,
    CancellationToken cancellationToken = default)
  {
    var results = new ConversionOutcome?[jobs.Count];
    var parallelOptions = new ParallelOptions
    {
      MaxDegreeOfParallelism = Math.Clamp(options.Parallelism, 1, 16),
      CancellationToken = cancellationToken,
    };

    await Parallel.ForEachAsync(
      Enumerable.Range(0, jobs.Count),
      parallelOptions,
      async (index, token) =>
      {
        var job = jobs[index];
        AudioInfo? knownInfo = null;
        knownInfos?.TryGetValue(job.Id, out knownInfo);
        results[index] = await _converter
          .ConvertAsync(job, options, knownInfo, progress, log, token)
          .ConfigureAwait(false);
      }).ConfigureAwait(false);

    return results
      .Where(r => r is not null)
      .Select(r => r!)
      .ToArray();
  }
}
