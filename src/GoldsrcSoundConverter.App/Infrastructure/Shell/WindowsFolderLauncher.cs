using System.Diagnostics;

namespace GoldsrcSoundConverter.App.Infrastructure.Shell;

public sealed class WindowsFolderLauncher : IFolderLauncher
{
  public void Open(string path)
  {
    Process.Start(new ProcessStartInfo
    {
      FileName = path,
      UseShellExecute = true,
    });
  }
}
