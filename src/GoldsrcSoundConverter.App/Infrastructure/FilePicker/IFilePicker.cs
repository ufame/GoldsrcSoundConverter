namespace GoldsrcSoundConverter.App.Infrastructure.FilePicker;

public interface IFilePicker
{
  IReadOnlyList<string> PickFiles();

  string? PickFile(string title, string filter);

  string? PickFolder(string title = "Выберите папку", string? initialDirectory = null);
}
