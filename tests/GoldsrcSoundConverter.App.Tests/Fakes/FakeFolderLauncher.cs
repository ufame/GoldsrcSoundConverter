using GoldsrcSoundConverter.App.Infrastructure.Shell;

namespace GoldsrcSoundConverter.Tests.Fakes;

public sealed class FakeFolderLauncher : IFolderLauncher
{
  public List<string> OpenedPaths { get; } = new();

  public Exception? OpenException { get; set; }

  public void Open(string path)
  {
    if (OpenException is not null)
    {
      throw OpenException;
    }

    OpenedPaths.Add(path);
  }
}
