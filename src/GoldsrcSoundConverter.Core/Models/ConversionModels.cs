namespace GoldsrcSoundConverter.Core.Models;

public enum ConversionStage
{
  Pending,
  Probing,
  Normalizing,
  Converting,
  Completed,
  Skipped,
  Failed,
}

public sealed record ConversionJob(
  Guid Id,
  string SourcePath,
  string? SourceRoot,
  TimeSpan? TrimStart,
  TimeSpan? TrimEnd)
{
  public string? OutputPath { get; init; }
}

public sealed record ConversionProgress(
  Guid JobId,
  ConversionStage Stage,
  double Percent,
  TimeSpan? Eta);

public sealed record ConversionOutcome(
  Guid JobId,
  bool Success,
  bool Skipped,
  string? OutputPath,
  string? Error,
  AudioInfo? OutputInfo);

public sealed record ConversionWorkItem(
  Guid Id,
  string SourcePath,
  string? SourceRoot,
  TimeSpan? TrimStart,
  TimeSpan? TrimEnd,
  AudioInfo? KnownInfo);

public sealed record ProbeResult(Guid Id, AudioInfo Info);

public sealed record ConversionBatchResult(
  IReadOnlyList<ConversionOutcome> Outcomes,
  string FfmpegPath,
  string FfprobePath);
