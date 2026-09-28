using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.Services;

public sealed record ConversionSettings(
  OutputAudioFormat Format,
  int SampleRate,
  TargetChannels Channels,
  TargetBitDepth BitDepth,
  int Mp3BitrateKbps,
  bool NormalizePeak,
  bool AsciiNames,
  bool LowercaseNames,
  bool PreserveStructure,
  CollisionPolicy CollisionPolicy,
  int Parallelism,
  string OutputDirectory);
