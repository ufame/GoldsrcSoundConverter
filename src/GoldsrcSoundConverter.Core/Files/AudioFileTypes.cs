namespace GoldsrcSoundConverter.Core.Files;

public static class AudioFileTypes
{
  private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
  {
    ".wav", ".wave", ".mp3", ".ogg", ".oga", ".opus", ".flac", ".m4a", ".aac",
    ".wma", ".aif", ".aiff", ".mka", ".mp2", ".mpa", ".ac3", ".wv", ".ape",
    ".mp4", ".mkv", ".mov", ".avi", ".webm",
  };

  public const string FileDialogFilter =
    "Аудио и видео|*.wav;*.wave;*.mp3;*.ogg;*.oga;*.opus;*.flac;*.m4a;*.aac;*.wma;*.aif;*.aiff;*.mka;*.mp2;*.mpa;*.ac3;*.wv;*.ape;*.mp4;*.mkv;*.mov;*.avi;*.webm|Все файлы|*.*";

  public static bool IsSupported(string path)
  {
    return SupportedExtensions.Contains(Path.GetExtension(path));
  }
}
