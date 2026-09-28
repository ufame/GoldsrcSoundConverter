using GoldsrcSoundConverter.App.Infrastructure.FilePicker;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeFilePicker : IFilePicker
{
  public IReadOnlyList<string> FilesToPick { get; set; } = Array.Empty<string>();

  public string? FileToPick { get; set; }

  public string? FolderToPick { get; set; }

  public int PickFilesCount { get; private set; }

  public int PickFolderCount { get; private set; }

  public List<string> FolderTitles { get; } = new();

  public IReadOnlyList<string> PickFiles()
  {
    PickFilesCount++;
    return FilesToPick;
  }

  public string? PickFile(string title, string filter)
  {
    return FileToPick;
  }

  public string? PickFolder(string title = "Выберите папку", string? initialDirectory = null)
  {
    PickFolderCount++;
    FolderTitles.Add(title);
    return FolderToPick;
  }
}
