using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
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

    var added = manager.Add(new[] { first, second });
    var addedAgain = manager.Add(new[] { first, second });

    Assert.Equal(2, added);
    Assert.Equal(0, addedAgain);
    Assert.Equal(2, manager.Items.Count);
  }

  [Fact]
  public void AddIgnoresUnsupportedFiles()
  {
    var manager = CreateManager();

    var added = manager.Add(new[] { CreateFile("notes.txt") });

    Assert.Equal(0, added);
    Assert.Empty(manager.Items);
  }

  [Fact]
  public void RemoveKeepsRemainingItemInstances()
  {
    var manager = CreateManager();
    var first = CreateFile("a.wav");
    var second = CreateFile("b.wav");
    manager.Add(new[] { first, second });
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
    manager.Add(new[] { CreateFile("a.wav"), CreateFile("b.wav") });

    manager.Clear();

    Assert.Empty(manager.Items);
  }

  [Fact]
  public void RecalculateTargetSizesUsesProvidedOptions()
  {
    var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") });
    var item = manager.Items[0];
    item.Info = CreateInfo(item.SourcePath);

    manager.RecalculateTargetSizes(Options() with { Format = OutputAudioFormat.Wav });

    Assert.NotEqual("—", item.TargetSizeText);
  }

  [Fact]
  public async Task ProbeResultIsAppliedToItemAndRaisesEvent()
  {
    var manager = CreateManager();
    QueueItemViewModel? probed = null;
    manager.ItemProbed += (_, item) => probed = item;
    manager.Add(new[] { CreateFile("a.wav") });
    var item = manager.Items[0];

    await WaitUntil(() => probed is not null);

    Assert.Same(item, probed);
    Assert.NotNull(item.Info);
    manager.RecalculateTargetSizes(Options() with { Format = OutputAudioFormat.Wav });
    Assert.NotEqual("—", item.TargetSizeText);
  }

  [Fact]
  public async Task RemovingItemCancelsItsPendingProbe()
  {
    _conversion.ProbeGate = new TaskCompletionSource();
    using var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") });
    var item = manager.Items[0];

    manager.Remove(item);

    await WaitUntil(() => _conversion.ProbeCancellations >= 1);
    _conversion.ProbeGate.SetResult();
    await Task.Delay(50);

    Assert.Null(item.Info);
  }

  [Fact]
  public async Task RemovingOneItemKeepsOtherProbesRunning()
  {
    _conversion.ProbeGate = new TaskCompletionSource();
    using var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav"), CreateFile("b.wav") });
    var removed = manager.Items[0];
    var kept = manager.Items[1];

    manager.Remove(removed);
    await WaitUntil(() => _conversion.ProbeCancellations >= 1);

    _conversion.ProbeGate.SetResult();
    await WaitUntil(() => kept.Info is not null);

    Assert.Null(removed.Info);
    Assert.True(_conversion.ProbeCancellations >= 1);
  }

  [Fact]
  public async Task ClearCancelsPendingProbes()
  {
    _conversion.ProbeGate = new TaskCompletionSource();
    using var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") });

    manager.Clear();

    await WaitUntil(() => _conversion.ProbeCancellations >= 1);
    Assert.Empty(manager.Items);
  }

  [Fact]
  public async Task DisposeCancelsPendingProbesAndIsIdempotent()
  {
    _conversion.ProbeGate = new TaskCompletionSource();
    using var manager = CreateManager();
    manager.Add(new[] { CreateFile("a.wav") });

    manager.Dispose();
    manager.Dispose();

    await WaitUntil(() => _conversion.ProbeCancellations >= 1);
  }

  private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
  {
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (!condition())
    {
      if (DateTime.UtcNow > deadline)
      {
        throw new TimeoutException("Условие не выполнено за отведённое время.");
      }

      await Task.Delay(10);
    }
  }

  private static AudioInfo CreateInfo(string path)
  {
    return new AudioInfo(path, "wav", "pcm_s16le", TimeSpan.FromSeconds(10), 22050, 1, 352800, 100);
  }
}
