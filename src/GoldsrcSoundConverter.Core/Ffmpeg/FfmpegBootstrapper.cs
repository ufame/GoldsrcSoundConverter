using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed record BootstrapProgress(string Stage, double? Percent);

public sealed class FfmpegBootstrapper
{
  private static readonly (string Name, string Url, string? Sha256Url)[] Sources =
  {
    (
      "gyan.dev (essentials)",
      "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
      "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256"),
    (
      "BtbN (LGPL)",
      "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip",
      null),
  };

  private static readonly HttpClient Http = CreateHttpClient();

  public FfmpegBootstrapper(string? installDirectory = null)
  {
    InstallDirectory = installDirectory ?? DefaultInstallDirectory;
  }

  public static string DefaultInstallDirectory => Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "GoldsrcSoundConverter",
    "ffmpeg");

  public string InstallDirectory { get; }

  public string FfmpegPath => Path.Combine(InstallDirectory, "ffmpeg.exe");

  public string FfprobePath => Path.Combine(InstallDirectory, "ffprobe.exe");

  public bool TryResolve(out string ffmpegPath, out string ffprobePath)
  {
    ffmpegPath = FfmpegPath;
    ffprobePath = FfprobePath;
    return File.Exists(ffmpegPath) && File.Exists(ffprobePath);
  }

  public static bool TryResolveCustom(string? customPath, out string ffmpegPath, out string ffprobePath)
  {
    ffmpegPath = string.Empty;
    ffprobePath = string.Empty;

    if (string.IsNullOrWhiteSpace(customPath))
    {
      return false;
    }

    string? directory = null;
    if (File.Exists(customPath))
    {
      directory = Path.GetDirectoryName(customPath);
    }
    else if (Directory.Exists(customPath))
    {
      directory = customPath;
    }

    if (directory is null)
    {
      return false;
    }

    foreach (var candidate in new[] { directory, Path.Combine(directory, "bin") })
    {
      var ffmpeg = Path.Combine(candidate, "ffmpeg.exe");
      if (!File.Exists(ffmpeg))
      {
        continue;
      }

      var ffprobe = Path.Combine(candidate, "ffprobe.exe");
      ffmpegPath = ffmpeg;
      ffprobePath = File.Exists(ffprobe) ? ffprobe : ffmpeg;
      return true;
    }

    return false;
  }

  public async Task<(string Ffmpeg, string Ffprobe)> EnsureAsync(
    string? customPath,
    IProgress<BootstrapProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (TryResolveCustom(customPath, out var customFfmpeg, out var customFfprobe))
    {
      return (customFfmpeg, customFfprobe);
    }

    if (TryResolve(out var installedFfmpeg, out var installedFfprobe))
    {
      return (installedFfmpeg, installedFfprobe);
    }

    Directory.CreateDirectory(InstallDirectory);
    var archivePath = Path.Combine(Path.GetTempPath(), $"gsc-ffmpeg-{Guid.NewGuid():N}.zip");

    Exception? lastError = null;
    foreach (var source in Sources)
    {
      cancellationToken.ThrowIfCancellationRequested();
      try
      {
        log?.Invoke($"Загрузка FFmpeg ({source.Name})...");
        await DownloadAsync(source.Url, archivePath, progress, cancellationToken).ConfigureAwait(false);

        if (source.Sha256Url is not null)
        {
          progress?.Report(new BootstrapProgress("Проверка контрольной суммы", null));
          await VerifySha256Async(archivePath, source.Sha256Url, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new BootstrapProgress("Распаковка FFmpeg", null));
        ExtractBinaries(archivePath, InstallDirectory);

        if (TryResolve(out var ffmpeg, out var ffprobe))
        {
          progress?.Report(new BootstrapProgress("FFmpeg готов", 1));
          log?.Invoke($"FFmpeg установлен: {ffmpeg}");
          return (ffmpeg, ffprobe);
        }

        throw new InvalidOperationException("В архиве не найдены ffmpeg.exe/ffprobe.exe.");
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        lastError = ex;
        log?.Invoke($"Не удалось установить FFmpeg из {source.Name}: {ex.Message}");
      }
      finally
      {
        TryDelete(archivePath);
      }
    }

    throw new InvalidOperationException(
      "Не удалось автоматически загрузить FFmpeg. Укажите папку с ffmpeg.exe вручную в настройках.",
      lastError);
  }

  private static async Task DownloadAsync(
    string url,
    string destinationPath,
    IProgress<BootstrapProgress>? progress,
    CancellationToken cancellationToken)
  {
    using var response = await Http
      .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
      .ConfigureAwait(false);
    response.EnsureSuccessStatusCode();

    var total = response.Content.Headers.ContentLength;
    await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    await using var destination = new FileStream(
      destinationPath,
      FileMode.Create,
      FileAccess.Write,
      FileShare.None,
      bufferSize: 1 << 20,
      useAsync: true);

    var buffer = new byte[1 << 20];
    long received = 0;
    int read;
    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
    {
      await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
      received += read;
      if (total is > 0)
      {
        progress?.Report(new BootstrapProgress(
          "Загрузка FFmpeg",
          Math.Clamp(received / (double)total.Value, 0, 1)));
      }
    }
  }

  private static async Task VerifySha256Async(string filePath, string sha256Url, CancellationToken cancellationToken)
  {
    var text = await Http.GetStringAsync(sha256Url, cancellationToken).ConfigureAwait(false);
    var expected = text
      .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
      .FirstOrDefault();

    if (expected is null || expected.Length != 64)
    {
      throw new InvalidOperationException("Не удалось прочитать контрольную сумму FFmpeg.");
    }

    var actual = await Task.Run(() =>
    {
      using var stream = File.OpenRead(filePath);
      return Convert.ToHexString(SHA256.HashData(stream));
    }, cancellationToken).ConfigureAwait(false);

    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException("Контрольная сумма архива FFmpeg не совпала.");
    }
  }

  private static void ExtractBinaries(string archivePath, string destinationDirectory)
  {
    using var archive = ZipFile.OpenRead(archivePath);
    var extracted = 0;

    foreach (var entry in archive.Entries)
    {
      var fileName = Path.GetFileName(entry.FullName);
      if (fileName.Length == 0)
      {
        continue;
      }

      var isBinary = fileName.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase)
        || fileName.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)
        || fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

      if (!isBinary)
      {
        continue;
      }

      var target = Path.Combine(destinationDirectory, fileName);
      entry.ExtractToFile(target, overwrite: true);
      extracted++;
    }

    if (extracted == 0)
    {
      throw new InvalidOperationException("Архив FFmpeg пуст или имеет неожиданный формат.");
    }
  }

  private static HttpClient CreateHttpClient()
  {
    var client = new HttpClient
    {
      Timeout = Timeout.InfiniteTimeSpan,
    };
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
      "Mozilla/5.0 (Windows NT 10.0; Win64; x64) GoldsrcSoundConverter/1.0");
    return client;
  }

  private static void TryDelete(string path)
  {
    try
    {
      if (File.Exists(path))
      {
        File.Delete(path);
      }
    }
    catch
    {
    }
  }
}
