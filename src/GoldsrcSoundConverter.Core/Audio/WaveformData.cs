namespace GoldsrcSoundConverter.Core.Audio;

public sealed class WaveformData
{
  public WaveformData(float[] mins, float[] maxs, int samplesPerBucket, int sourceSampleRate)
  {
    if (mins.Length != maxs.Length)
    {
      throw new ArgumentException("Массивы min/max должны быть одинаковой длины.", nameof(maxs));
    }

    Mins = mins;
    Maxs = maxs;
    SamplesPerBucket = samplesPerBucket;
    SourceSampleRate = sourceSampleRate;
    Duration = TimeSpan.FromSeconds(sourceSampleRate > 0
      ? mins.Length * (double)samplesPerBucket / sourceSampleRate
      : 0);
  }

  public float[] Mins { get; }

  public float[] Maxs { get; }

  public int SamplesPerBucket { get; }

  public int SourceSampleRate { get; }

  public TimeSpan Duration { get; }

  public int BucketCount => Mins.Length;

  public (float Min, float Max) Aggregate(int startBucket, int endBucketExclusive)
  {
    var start = Math.Clamp(startBucket, 0, Math.Max(0, BucketCount - 1));
    var end = Math.Clamp(endBucketExclusive, start + 1, BucketCount);
    var min = float.MaxValue;
    var max = float.MinValue;

    for (var i = start; i < end; i++)
    {
      if (Mins[i] < min)
      {
        min = Mins[i];
      }

      if (Maxs[i] > max)
      {
        max = Maxs[i];
      }
    }

    return min > max ? (0, 0) : (min, max);
  }
}
