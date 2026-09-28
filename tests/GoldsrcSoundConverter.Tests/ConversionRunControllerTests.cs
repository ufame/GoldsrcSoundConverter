using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionRunControllerTests : IDisposable
{
  private static readonly bool[] ExpectedBusyStates = { true, false };

  private readonly TempDirectory _temp = new();
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  private ConversionRunController CreateController()
  {
    return new ConversionRunController(_conversion, new ConversionRequestFactory(), _log);
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

  private static QueueItemViewModel Item(string name, int index)
  {
    return new QueueItemViewModel(index, name, null);
  }

  [Fact]
  public async Task SuccessOutcomesAreAppliedAndCounted()
  {
    using var controller = CreateController();
    var items = new[] { Item("a.wav", 0), Item("b.wav", 1) };

    var summary = await controller.RunAsync(items, Options());

    Assert.Equal(2, summary.Completed);
    Assert.Equal(0, summary.Failed);
    Assert.Equal(0, summary.Skipped);
    Assert.False(summary.Cancelled);
    Assert.All(items, item =>
    {
      Assert.Equal(ConversionStage.Completed, item.Stage);
      Assert.Equal(1, item.Progress);
      Assert.NotNull(item.OutputPath);
    });
  }

  [Fact]
  public async Task FailuresAndSkipsAreCounted()
  {
    _conversion.OutcomeFactory = item => item.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)
      ? new ConversionOutcome(item.Id, false, false, null, "boom", null)
      : new ConversionOutcome(item.Id, true, true, null, null, null);
    using var controller = CreateController();
    var items = new[] { Item("bad.wav", 0), Item("skip.wav", 1) };

    var summary = await controller.RunAsync(items, Options());

    Assert.Equal(0, summary.Completed);
    Assert.Equal(1, summary.Failed);
    Assert.Equal(1, summary.Skipped);
    Assert.Equal(ConversionStage.Failed, items[0].Stage);
    Assert.Equal("boom", items[0].Error);
    Assert.Equal(ConversionStage.Skipped, items[1].Stage);
  }

  [Fact]
  public async Task ProbeResultsUpdateItems()
  {
    using var controller = CreateController();
    var items = new[] { Item("a.wav", 0) };
    _conversion.ProbeResults.Add(new ProbeResult(items[0].Id, new AudioInfo(
      items[0].SourcePath,
      "wav",
      "pcm_s16le",
      TimeSpan.FromSeconds(10),
      22050,
      1,
      352800,
      100)));

    await controller.RunAsync(items, Options());

    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (items[0].Info is null && DateTime.UtcNow < deadline)
    {
      await Task.Delay(10);
    }

    Assert.NotNull(items[0].Info);
    Assert.NotEqual("—", items[0].TargetSizeText);
  }

  [Fact]
  public async Task OverallProgressIsReported()
  {
    using var controller = CreateController();
    var items = new[] { Item("a.wav", 0) };
    var values = new List<double>();

    await controller.RunAsync(items, Options(), value => values.Add(value));

    Assert.Contains(values, value => value is > 0 and < 1);
  }

  [Fact]
  public async Task CancelResetsPendingItemsAndReturnsCancelled()
  {
    _conversion.ConversionGate = new TaskCompletionSource();
    using var controller = CreateController();
    var items = new[] { Item("a.wav", 0) };

    var runTask = controller.RunAsync(items, Options());

    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (!controller.IsBusy && DateTime.UtcNow < deadline)
    {
      await Task.Delay(10);
    }

    controller.Cancel();
    var summary = await runTask;

    Assert.True(summary.Cancelled);
    Assert.Equal(ConversionStage.Pending, items[0].Stage);
    Assert.Equal(0, items[0].Progress);
  }

  [Fact]
  public async Task BusyFlagTogglesWithEvents()
  {
    using var controller = CreateController();
    var states = new List<bool>();
    controller.BusyChanged += (_, _) => states.Add(controller.IsBusy);

    await controller.RunAsync(new[] { Item("a.wav", 0) }, Options());

    Assert.Equal(ExpectedBusyStates, states);
    Assert.False(controller.IsBusy);
  }
}
