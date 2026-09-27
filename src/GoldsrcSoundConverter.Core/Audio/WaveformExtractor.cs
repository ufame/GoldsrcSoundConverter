using GoldsrcSoundConverter.Core.Ffmpeg;

namespace GoldsrcSoundConverter.Core.Audio;

public static class WaveformExtractor
{
  public const int PreviewSampleRate = 8000;
  public const int SamplesPerBucket = 64;

  public static async Task<WaveformData> ExtractAsync(
    string ffmpegPath,
    string filePath,
    CancellationToken cancellationToken = default)
  {
    var mins = new List<float>(4096);
    var maxs = new List<float>(4096);

    short bucketMin = short.MaxValue;
    short bucketMax = short.MinValue;
    var bucketCount = 0;
    byte? pending = null;
    long totalSamples = 0;

    void ProcessSample(short sample)
    {
      if (sample < bucketMin)
      {
        bucketMin = sample;
      }

      if (sample > bucketMax)
      {
        bucketMax = sample;
      }

      totalSamples++;
      if (++bucketCount < SamplesPerBucket)
      {
        return;
      }

      mins.Add(bucketMin / 32768f);
      maxs.Add(bucketMax / 32768f);
      bucketMin = short.MaxValue;
      bucketMax = short.MinValue;
      bucketCount = 0;
    }

    var arguments = FfmpegArguments.BuildRawWaveform(filePath, PreviewSampleRate);
    var standardError = await FfmpegRunner.RunBinaryAsync(
      ffmpegPath,
      arguments,
      async (stream, token) =>
      {
        var buffer = new byte[1 << 16];
        int read;
        while ((read = await stream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
          for (var i = 0; i < read; i++)
          {
            if (pending is null)
            {
              pending = buffer[i];
              continue;
            }

            ProcessSample((short)(pending.Value | (buffer[i] << 8)));
            pending = null;
          }
        }
      },
      cancellationToken).ConfigureAwait(false);

    if (bucketCount > 0)
    {
      mins.Add(bucketMin / 32768f);
      maxs.Add(bucketMax / 32768f);
    }

    if (mins.Count == 0)
    {
      throw new FfmpegException("Не удалось декодировать аудио для волновой формы.", standardError);
    }

    return new WaveformData(mins.ToArray(), maxs.ToArray(), SamplesPerBucket, PreviewSampleRate);
  }
}
