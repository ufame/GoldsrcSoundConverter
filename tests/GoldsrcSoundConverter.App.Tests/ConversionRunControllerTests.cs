using GoldsrcSoundConverter.App.Services;
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
    return new ConversionRunController(_conversion, _log);
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

  private static ConversionWorkItem WorkItem(string name)
  {
    return new ConversionWorkItem(Guid.NewGuid(), name, null, null, null, null);
  }

  [Fact]
  public async Task ReturnsOutcomesAndSummary()
  {
    using var controller = CreateController();
    var workItems = new[] { WorkItem("a.wav"), WorkItem("b.wav") };

    var result = await controller.RunAsync(workItems, Options());

    Assert.False(result.Summary.Cancelled);
    Assert.Equal(2, result.Summary.Completed);
    Assert.Equal(0, result.Summary.Failed);
    Assert.Equal(0, result.Summary.Skipped);
    Assert.Equal(2, result.Outcomes.Count);
  }

  [Fact]
  public async Task SummarizesFailuresAndSkips()
  {
    _conversion.OutcomeFactory = item => item.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)
      ? new ConversionOutcome(item.Id, false, false, null, "boom", null)
      : new ConversionOutcome(item.Id, true, true, null, null, null);
    using var controller = CreateController();

    var result = await controller.RunAsync(new[] { WorkItem("bad.wav"), WorkItem("skip.wav") }, Options());

    Assert.Equal(0, result.Summary.Completed);
    Assert.Equal(1, result.Summary.Failed);
    Assert.Equal(1, result.Summary.Skipped);
  }

  [Fact]
  public async Task ForwardProgressAndProbeToCaller()
  {
    var progress = new List<ConversionProgress>();
    var probes = new List<ProbeResult>();
    var workItem = WorkItem("a.wav");
    _conversion.ProbeResults.Add(new ProbeResult(workItem.Id, new AudioInfo(
      workItem.SourcePath, "wav", "pcm_s16le", TimeSpan.FromSeconds(5), 22050, 1, 352800, 100)));
    using var controller = CreateController();

    await controller.RunAsync(
      new[] { workItem },
      Options(),
      new ActionProgress<ConversionProgress>(progress.Add),
      new ActionProgress<ProbeResult>(probes.Add));

    Assert.Contains(progress, value => value.Stage == ConversionStage.Converting);
    Assert.Single(probes);
    Assert.Equal(workItem.Id, probes[0].Id);
  }

  [Fact]
  public async Task CancelReturnsCancelledResult()
  {
    _conversion.ConversionGate = new TaskCompletionSource();
    using var controller = CreateController();

    var runTask = controller.RunAsync(new[] { WorkItem("a.wav") }, Options());

    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (!controller.IsBusy && DateTime.UtcNow < deadline)
    {
      await Task.Delay(10);
    }

    controller.Cancel();
    var result = await runTask;

    Assert.True(result.Summary.Cancelled);
    Assert.Empty(result.Outcomes);
  }

  [Fact]
  public async Task BusyFlagTogglesWithEvents()
  {
    using var controller = CreateController();
    var states = new List<bool>();
    controller.BusyChanged += (_, _) => states.Add(controller.IsBusy);

    await controller.RunAsync(new[] { WorkItem("a.wav") }, Options());

    Assert.Equal(ExpectedBusyStates, states);
    Assert.False(controller.IsBusy);
  }
}
