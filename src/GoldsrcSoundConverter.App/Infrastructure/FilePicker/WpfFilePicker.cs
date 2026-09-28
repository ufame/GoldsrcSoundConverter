using System.IO;
using GoldsrcSoundConverter.Core.Files;
using Microsoft.Win32;

namespace GoldsrcSoundConverter.App.Infrastructure.FilePicker;

public sealed class WpfFilePicker : IFilePicker
{
  public IReadOnlyList<string> PickFiles()
  {
    var dialog = new OpenFileDialog
    {
      Title = "Выберите звуковые файлы",
      Multiselect = true,
      Filter = AudioFileTypes.FileDialogFilter,
    };

    return dialog.ShowDialog() == true
      ? dialog.FileNames
      : Array.Empty<string>();
  }

  public string? PickFile(string title, string filter)
  {
    var dialog = new OpenFileDialog
    {
      Title = title,
      Multiselect = false,
      Filter = filter,
    };

    return dialog.ShowDialog() == true ? dialog.FileName : null;
  }

  public string? PickFolder(string title = "Выберите папку", string? initialDirectory = null)
  {
    var dialog = new OpenFolderDialog
    {
      Title = title,
    };

    if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
    {
      dialog.InitialDirectory = initialDirectory;
    }

    return dialog.ShowDialog() == true
      ? dialog.FolderName
      : null;
  }
}
