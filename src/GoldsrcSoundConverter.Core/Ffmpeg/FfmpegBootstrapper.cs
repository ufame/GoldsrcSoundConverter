using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace GoldsrcSoundConverter.Core.Ffmpeg;

public sealed record BootstrapProgress(string Stage, double? Percent);

public sealed class FfmpegBootstrapper
{
  public const string Version = "9.0.2";

  public const string DownloadUrl =
    "https://github.com/GyanD/codexffmpeg/releases/download/9.0.2/ffmpeg-9.0.2-essentials_build.zip";

  public const string ArchiveSha256 =
    "60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba";

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
    return TryResolveCustom(customPath, out ffmpegPath, out ffprobePath, out _);
  }

  public static bool TryResolveCustom(
    string? customPath,
    out string ffmpegPath,
    out string ffprobePath,
    out string? error)
  {
    ffmpegPath = string.Empty;
    ffprobePath = string.Empty;
    error = null;

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
      error = $"Указанный путь к FFmpeg не найден: {customPath}";
      return false;
    }

    string? missingFfprobe = null;
    foreach (var candidate in new[] { directory, Path.Combine(directory, "bin") })
    {
      var ffmpeg = Path.Combine(candidate, "ffmpeg.exe");
      if (!File.Exists(ffmpeg))
      {
        continue;
      }

      var ffprobe = Path.Combine(candidate, "ffprobe.exe");
      if (!File.Exists(ffprobe))
      {
        missingFfprobe ??= $"Рядом с ffmpeg.exe не найден ffprobe.exe: {candidate}";
        continue;
      }

      ffmpegPath = ffmpeg;
      ffprobePath = ffprobe;
      return true;
    }

    error = missingFfprobe ?? $"В указанной папке не найден ffmpeg.exe: {directory}";
    return false;
  }

  public async Task<(string Ffmpeg, string Ffprobe)> EnsureAsync(
    string? customPath,
    IProgress<BootstrapProgress>? progress,
    Action<string>? log,
    CancellationToken cancellationToken = default)
  {
    if (TryResolveCustom(customPath, out var customFfmpeg, out var customFfprobe, out var customError))
    {
      return (customFfmpeg, customFfprobe);
    }

    if (customError is not null)
    {
      throw new InvalidOperationException(customError);
    }

    if (TryResolve(out var installedFfmpeg, out var installedFfprobe))
    {
      return (installedFfmpeg, installedFfprobe);
    }

    Directory.CreateDirectory(InstallDirectory);
    var archivePath = Path.Combine(Path.GetTempPath(), $"gsc-ffmpeg-{Guid.NewGuid():N}.zip");

    try
    {
      log?.Invoke($"Загрузка FFmpeg {Version}...");
      await DownloadAsync(DownloadUrl, archivePath, progress, cancellationToken).ConfigureAwait(false);

      progress?.Report(new BootstrapProgress("Проверка контрольной суммы", null));
      await VerifySha256Async(archivePath, cancellationToken).ConfigureAwait(false);

      progress?.Report(new BootstrapProgress("Распаковка FFmpeg", null));
      ExtractBinaries(archivePath, InstallDirectory);

      if (TryResolve(out var ffmpeg, out var ffprobe))
      {
        progress?.Report(new BootstrapProgress("FFmpeg готов", 1));
        log?.Invoke($"FFmpeg {Version} установлен: {ffmpeg}");
        return (ffmpeg, ffprobe);
      }

      throw new InvalidOperationException("В архиве не найдены ffmpeg.exe/ffprobe.exe.");
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      throw new InvalidOperationException(
        $"Не удалось установить FFmpeg {Version}: {ex.Message} "
        + "Укажите папку с ffmpeg.exe вручную в настройках.",
        ex);
    }
    finally
    {
      TryDelete(archivePath);
    }
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

  private static async Task VerifySha256Async(string filePath, CancellationToken cancellationToken)
  {
    var actual = await Task.Run(() =>
    {
      using var stream = File.OpenRead(filePath);
      return Convert.ToHexString(SHA256.HashData(stream));
    }, cancellationToken).ConfigureAwait(false);

    if (!string.Equals(actual, ArchiveSha256, StringComparison.OrdinalIgnoreCase))
    {
      throw new InvalidOperationException(
        $"Контрольная сумма архива FFmpeg не совпала: ожидалась {ArchiveSha256}, получена {actual}.");
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
