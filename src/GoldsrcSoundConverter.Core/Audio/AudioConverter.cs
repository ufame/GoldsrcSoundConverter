using System.Diagnostics;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.Core.Audio;

public sealed class AudioConverter
{
  private readonly string _ffmpegPath;
  private readonly string _ffprobePath;

  public AudioConverter(string ffmpegPath, string ffprobePath)
  {
    _ffmpegPath = ffmpegPath;
    _ffprobePath = ffprobePath;
  }

  public async Task<ConversionOutcome> ConvertAsync(
    ConversionJob job,
    ConversionOptions options,
    AudioInfo? knownInfo,
    IProgress<ConversionProgress>? progress = null,
    Action<string>? log = null,
    CancellationToken cancellationToken = default)
  {
    try
    {
      progress?.Report(new ConversionProgress(job.Id, ConversionStage.Probing, 0, null));
      var info = knownInfo ?? await AudioProbe
        .ProbeAsync(_ffprobePath, job.SourcePath, cancellationToken)
        .ConfigureAwait(false);

      var outputPath = job.OutputPath;
      if (outputPath is null)
      {
        log?.Invoke($"[{Path.GetFileName(job.SourcePath)}] пропущен: файл уже существует");
        progress?.Report(new ConversionProgress(job.Id, ConversionStage.Skipped, 0, TimeSpan.Zero));
        return new ConversionOutcome(job.Id, true, true, null, null, null);
      }

      var trimStart = job.TrimStart ?? TimeSpan.Zero;
      var trimEnd = job.TrimEnd ?? info.Duration;
      if (trimEnd <= trimStart)
      {
        throw new InvalidOperationException("Конец обрезки должен быть позже начала.");
      }

      var outputDuration = trimEnd - trimStart;
      var gainDb = await NormalizeAsync(job, options, trimStart, outputDuration, progress, log, cancellationToken)
        .ConfigureAwait(false);

      var directory = Path.GetDirectoryName(outputPath);
      if (!string.IsNullOrEmpty(directory))
      {
        Directory.CreateDirectory(directory);
      }

      var arguments = FfmpegArguments.BuildConvert(
        job.SourcePath,
        outputPath,
        options,
        gainDb,
        trimStart,
        outputDuration);

      var parser = new FfmpegProgressParser(outputDuration);
      var stopwatch = Stopwatch.StartNew();
      progress?.Report(new ConversionProgress(job.Id, ConversionStage.Converting, 0, outputDuration));

      var result = await FfmpegRunner.RunAsync(
        _ffmpegPath,
        arguments,
        line =>
        {
          if (!parser.TryParse(line, out var fraction))
          {
            return;
          }

          var eta = fraction > 0.01
            ? TimeSpan.FromSeconds(stopwatch.Elapsed.TotalSeconds * (1 - fraction) / fraction)
            : (TimeSpan?)null;
          progress?.Report(new ConversionProgress(job.Id, ConversionStage.Converting, fraction, eta));
        },
        cancellationToken: cancellationToken).ConfigureAwait(false);

      if (!result.Success)
      {
        throw new FfmpegException("FFmpeg завершился с ошибкой.", result.StandardError);
      }

      var outputInfo = await AudioProbe
        .ProbeAsync(_ffprobePath, outputPath, cancellationToken)
        .ConfigureAwait(false);

      progress?.Report(new ConversionProgress(job.Id, ConversionStage.Completed, 1, TimeSpan.Zero));
      return new ConversionOutcome(job.Id, true, false, outputPath, null, outputInfo);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      var message = ex is FfmpegException ffmpegException ? ffmpegException.ShortMessage : ex.Message;
      log?.Invoke($"[{Path.GetFileName(job.SourcePath)}] ошибка: {message}");
      progress?.Report(new ConversionProgress(job.Id, ConversionStage.Failed, 0, null));
      return new ConversionOutcome(job.Id, false, false, null, message, null);
    }
  }

  private async Task<double?> NormalizeAsync(
    ConversionJob job,
    ConversionOptions options,
    TimeSpan trimStart,
    TimeSpan outputDuration,
    IProgress<ConversionProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken)
  {
    if (!options.NormalizePeak)
    {
      return null;
    }

    progress?.Report(new ConversionProgress(job.Id, ConversionStage.Normalizing, 0, null));
    var gainDb = await PeakNormalizer.MeasureGainAsync(
      _ffmpegPath,
      job.SourcePath,
      trimStart,
      outputDuration,
      options.NormalizeTargetDb,
      options.MaxNormalizeGainDb,
      cancellationToken).ConfigureAwait(false);

    log?.Invoke(gainDb.HasValue
      ? $"[{Path.GetFileName(job.SourcePath)}] нормализация: {gainDb.Value:+0.00;-0.00} dB"
      : $"[{Path.GetFileName(job.SourcePath)}] нормализация не требуется");

    return gainDb;
  }
}
