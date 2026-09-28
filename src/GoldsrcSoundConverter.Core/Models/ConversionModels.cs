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
  int Index,
  string SourcePath,
  string? SourceRoot,
  TimeSpan? TrimStart,
  TimeSpan? TrimEnd)
{
  public string? OutputPath { get; init; }
}

public sealed record ConversionProgress(
  int Index,
  ConversionStage Stage,
  double Percent,
  TimeSpan? Eta);

public sealed record ConversionOutcome(
  int Index,
  bool Success,
  bool Skipped,
  string? OutputPath,
  string? Error,
  AudioInfo? OutputInfo);
