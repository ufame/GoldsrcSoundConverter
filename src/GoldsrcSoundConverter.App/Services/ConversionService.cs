using System.IO;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Files;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed class ConversionService : IConversionService
{
  private const int ProbeParallelism = 3;

  private readonly IProcessRunner _processRunner;
  private readonly FfmpegBootstrapper _bootstrapper;
  private readonly SemaphoreSlim _ffmpegLock = new(1, 1);
  private readonly SemaphoreSlim _probeGate = new(ProbeParallelism, ProbeParallelism);

  private string? _customFfmpegPath;
  private string? _ffmpegPath;
  private string? _ffprobePath;

  public ConversionService(IProcessRunner processRunner, FfmpegBootstrapper bootstrapper)
  {
    _processRunner = processRunner;
    _bootstrapper = bootstrapper;
  }

  public string? FfmpegPath => _ffmpegPath;

  public string? FfprobePath => _ffprobePath;

  public void SetCustomFfmpegPath(string? customPath)
  {
    _customFfmpegPath = customPath;
    _ffmpegPath = null;
    _ffprobePath = null;
  }

  public bool TryResolveFfmpeg(out string ffmpeg, out string ffprobe)
  {
    if (_ffmpegPath is not null && _ffprobePath is not null)
    {
      ffmpeg = _ffmpegPath;
      ffprobe = _ffprobePath;
      return true;
    }

    if (FfmpegBootstrapper.TryResolveCustom(_customFfmpegPath, out ffmpeg, out ffprobe)
      || _bootstrapper.TryResolve(out ffmpeg, out ffprobe))
    {
      _ffmpegPath = ffmpeg;
      _ffprobePath = ffprobe;
      return true;
    }

    ffmpeg = string.Empty;
    ffprobe = string.Empty;
    return false;
  }

  public async Task<(string Ffmpeg, string Ffprobe)> EnsureFfmpegAsync(
    IProgress<BootstrapProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (_ffmpegPath is not null && _ffprobePath is not null)
    {
      return (_ffmpegPath, _ffprobePath);
    }

    await _ffmpegLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      if (_ffmpegPath is not null && _ffprobePath is not null)
      {
        return (_ffmpegPath, _ffprobePath);
      }

      var result = await _bootstrapper
        .EnsureAsync(_customFfmpegPath, progress, log, cancellationToken)
        .ConfigureAwait(false);

      _ffmpegPath = result.Ffmpeg;
      _ffprobePath = result.Ffprobe;
      return result;
    }
    finally
    {
      _ffmpegLock.Release();
    }
  }

  public async Task<ProbeResult?> TryProbeAsync(
    Guid id,
    string path,
    CancellationToken cancellationToken = default)
  {
    if (!TryResolveFfmpeg(out _, out var ffprobe))
    {
      return null;
    }

    await _probeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
      var info = await AudioProbe
        .ProbeAsync(_processRunner, ffprobe, path, cancellationToken)
        .ConfigureAwait(false);
      return new ProbeResult(id, info);
    }
    finally
    {
      _probeGate.Release();
    }
  }

  public async Task<WaveformData?> TryExtractWaveformAsync(
    string path,
    CancellationToken cancellationToken = default)
  {
    if (!TryResolveFfmpeg(out var ffmpeg, out _))
    {
      return null;
    }

    return await WaveformExtractor
      .ExtractAsync(_processRunner, ffmpeg, path, cancellationToken)
      .ConfigureAwait(false);
  }

  public async Task<ConversionBatchResult> ConvertAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    ConversionOptions options,
    IProgress<ConversionProgress>? progress,
    IProgress<ProbeResult>? probeProgress,
    IProgress<BootstrapProgress>? bootstrapProgress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    options.Validate();

    var (ffmpeg, ffprobe) = await EnsureFfmpegAsync(bootstrapProgress, log, cancellationToken)
      .ConfigureAwait(false);

    var knownInfos = await ProbeMissingAsync(workItems, ffprobe, probeProgress, log, cancellationToken)
      .ConfigureAwait(false);

    var jobs = workItems
      .Select(item => new ConversionJob(
        item.Id,
        item.SourcePath,
        item.SourceRoot,
        item.TrimStart,
        item.TrimEnd))
      .ToArray();

    var planned = new ConversionPlanner().Plan(jobs, options);
    var converter = new AudioConverter(_processRunner, ffmpeg, ffprobe);
    var outcomes = await new BatchConverter(converter)
      .RunAsync(planned, options, knownInfos, progress, log, cancellationToken)
      .ConfigureAwait(false);

    return new ConversionBatchResult(outcomes, ffmpeg, ffprobe);
  }

  private async Task<IReadOnlyDictionary<Guid, AudioInfo>> ProbeMissingAsync(
    IReadOnlyList<ConversionWorkItem> workItems,
    string ffprobePath,
    IProgress<ProbeResult>? probeProgress,
    Action<string>? log,
    CancellationToken cancellationToken)
  {
    var knownInfos = new Dictionary<Guid, AudioInfo>();
    foreach (var item in workItems)
    {
      if (item.KnownInfo is not null)
      {
        knownInfos[item.Id] = item.KnownInfo;
      }
    }

    var pending = workItems.Where(item => item.KnownInfo is null).ToArray();
    if (pending.Length == 0)
    {
      return knownInfos;
    }

    var results = new System.Collections.Concurrent.ConcurrentDictionary<Guid, AudioInfo>();
    await Parallel.ForEachAsync(
      pending,
      new ParallelOptions { MaxDegreeOfParallelism = ProbeParallelism, CancellationToken = cancellationToken },
      async (item, token) =>
      {
        try
        {
          var info = await AudioProbe
            .ProbeAsync(_processRunner, ffprobePath, item.SourcePath, token)
            .ConfigureAwait(false);
          results[item.Id] = info;
          probeProgress?.Report(new ProbeResult(item.Id, info));
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          log?.Invoke($"Не удалось проанализировать {Path.GetFileName(item.SourcePath)}: {ex.Message}");
        }
      }).ConfigureAwait(false);

    foreach (var pair in results)
    {
      knownInfos[pair.Key] = pair.Value;
    }

    return knownInfos;
  }
}
