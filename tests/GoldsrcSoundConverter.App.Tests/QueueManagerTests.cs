using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class QueueManagerTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private QueueManager CreateManager()
  {
    return new QueueManager(_conversion, _log);
  }

  private ConversionOptions Options()
  {
    return new ConversionOptions
    {
      OutputDirectory = _temp.Path,
      AsciiNames = true,
      LowercaseNames = true,
    };
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }

  [Fact]
  public void AddAddsNewFilesAndSkipsDuplicates()
  {
    var manager = CreateManager();
    var first = CreateFile("a.wav");
    var second = CreateFile("b.ogg");

    var added = manager.Add(new[] { first, second }, Options());
    var addedAgain = manager.Add(new[] { first, second }, Options());

    Assert.Equal(2, added);
    Assert.Equal(0, addedAgain);
    Assert.Equal(2, manager.Items.Count);
  }

  [Fact]
  public void AddIgnoresUnsupportedFiles()
  {
    var manager = CreateManager();

    var added = manager.Add(new[] { CreateFile("notes.txt") }, Options());

    Assert.Equal(0, added);
    Assert.Empty(manager.Items);
  }

  [Fact]
  public void RemoveKeepsRemainingItemInstances()
  {
    var manager = CreateManager();
    var first = CreateFile("a.wav");
    var second = CreateFile("b.wav");
    manager.Add(new[] { first, second }, Options());
    var secondItem = manager.Items[1];
    var secondId = secondItem.Id;

    manager.Remove(manager.Items[0]);

    var remaining = Assert.Single(manager.Items);
    Assert.Same(secondItem, remaining);
    Assert.Equal(second, remaining.SourcePath);
    Assert.Equal(secondId, remaining.Id);
  }

  [Fact]
  public void ClearEmptiesQueue()
  {
    var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav"), CreateFile("b.wav") }, Options());

    manager.Clear();

    Assert.Empty(manager.Items);
  }

  [Fact]
  public void RecalculateTargetSizesUsesProvidedOptions()
  {
    var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") }, Options());
    var item = manager.Items[0];
    item.Info = CreateInfo(item.SourcePath);

    manager.RecalculateTargetSizes(Options() with { Format = OutputAudioFormat.Wav });

    Assert.NotEqual("—", item.TargetSizeText);
  }

  [Fact]
  public async Task ProbeResultIsAppliedToItem()
  {
    var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") }, Options());
    var item = manager.Items[0];

    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (item.Info is null && DateTime.UtcNow < deadline)
    {
      await Task.Delay(10);
    }

    Assert.NotNull(item.Info);
    Assert.NotEqual("—", item.TargetSizeText);
  }

  private static AudioInfo CreateInfo(string path)
  {
    return new AudioInfo(path, "wav", "pcm_s16le", TimeSpan.FromSeconds(10), 22050, 1, 352800, 100);
  }
}
