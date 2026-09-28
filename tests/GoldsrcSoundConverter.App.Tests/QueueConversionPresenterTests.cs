using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class QueueConversionPresenterTests : IDisposable
{
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();
  private readonly QueueManager _queue;
  private readonly QueueConversionPresenter _presenter;

  public QueueConversionPresenterTests()
  {
    _queue = new QueueManager(_conversion, _log);
    _presenter = new QueueConversionPresenter(_queue);
  }

  public void Dispose()
  {
    _queue.Dispose();
  }

  private static ConversionOptions Options()
  {
    return new ConversionOptions
    {
      OutputDirectory = @"C:\out",
      AsciiNames = true,
      LowercaseNames = true,
    };
  }

  private QueueItemViewModel AddItem(string path)
  {
    var item = new QueueItemViewModel(path, null);
    _queue.Items.Add(item);
    return item;
  }

  private static ProbeResult Probe(string path, Guid id)
  {
    return new ProbeResult(id, new AudioInfo(path, "wav", "pcm_s16le", TimeSpan.FromSeconds(10), 22050, 1, 352800, 100));
  }

  [Fact]
  public void BeginRunResetsStateAndReturnsSnapshot()
  {
    var first = AddItem(@"C:\in\a.wav");
    first.Stage = ConversionStage.Failed;
    first.Progress = 0.5;
    first.Error = "old";
    first.OutputPath = "old.wav";
    var second = AddItem(@"C:\in\b.wav");

    var workItems = _presenter.BeginRun();

    Assert.Equal(2, workItems.Count);
    Assert.Equal(new[] { first.Id, second.Id }, workItems.Select(workItem => workItem.Id));
    Assert.All(new[] { first, second }, item =>
    {
      Assert.Equal(ConversionStage.Pending, item.Stage);
      Assert.Equal(0, item.Progress);
      Assert.Null(item.Error);
      Assert.Null(item.OutputPath);
    });
  }

  [Fact]
  public void BeginRunMapsTrimsAndKnownInfo()
  {
    var item = new QueueItemViewModel(@"C:\in\clip.ogg", @"C:\in")
    {
      TrimStartSeconds = 1,
      TrimEndSeconds = 4,
    };
    item.Info = new AudioInfo(item.SourcePath, "ogg", "vorbis", TimeSpan.FromSeconds(5), 44100, 2, 128000, 10);
    _queue.Items.Add(item);

    var workItem = Assert.Single(_presenter.BeginRun());

    Assert.Equal(item.Id, workItem.Id);
    Assert.Equal(item.SourcePath, workItem.SourcePath);
    Assert.Equal(item.SourceRoot, workItem.SourceRoot);
    Assert.Equal(TimeSpan.FromSeconds(1), workItem.TrimStart);
    Assert.Equal(TimeSpan.FromSeconds(4), workItem.TrimEnd);
    Assert.Same(item.Info, workItem.KnownInfo);
  }

  [Fact]
  public void ProgressHandlerUpdatesItemAndOverall()
  {
    var first = AddItem(@"C:\in\a.wav");
    var second = AddItem(@"C:\in\b.wav");
    _presenter.BeginRun();
    var overall = new List<double>();
    var handler = _presenter.CreateProgressHandler(overall.Add);

    handler.Report(new ConversionProgress(first.Id, ConversionStage.Converting, 0.5, null));

    Assert.Equal(ConversionStage.Converting, first.Stage);
    Assert.Equal(0.5, first.Progress, 3);
    Assert.Contains(overall, value => Math.Abs(value - 0.25) < 0.001);

    handler.Report(new ConversionProgress(first.Id, ConversionStage.Completed, 1, null));
    handler.Report(new ConversionProgress(second.Id, ConversionStage.Converting, 0.5, null));

    Assert.Contains(overall, value => Math.Abs(value - 0.75) < 0.001);
  }

  [Fact]
  public void ProgressForItemOutsideSnapshotIsIgnored()
  {
    _presenter.BeginRun();
    var late = AddItem(@"C:\in\late.wav");
    var handler = _presenter.CreateProgressHandler();

    handler.Report(new ConversionProgress(late.Id, ConversionStage.Converting, 0.5, null));

    Assert.Equal(ConversionStage.Pending, late.Stage);
    Assert.Equal(0, late.Progress);
  }

  [Fact]
  public void ProbeHandlerAppliesInfoAndTargetSize()
  {
    var item = AddItem(@"C:\in\a.wav");
    _presenter.BeginRun();
    var handler = _presenter.CreateProbeHandler(Options());

    handler.Report(Probe(item.SourcePath, item.Id));

    Assert.NotNull(item.Info);
    Assert.NotEqual("—", item.TargetSizeText);
  }

  [Fact]
  public void ProbeIsIgnoredWhenInfoAlreadyKnown()
  {
    var item = AddItem(@"C:\in\a.wav");
    var known = new AudioInfo(item.SourcePath, "wav", "pcm_s16le", TimeSpan.FromSeconds(1), 22050, 1, 352800, 50);
    item.Info = known;
    _presenter.BeginRun();
    var handler = _presenter.CreateProbeHandler(Options());

    handler.Report(Probe(item.SourcePath, item.Id));

    Assert.Same(known, item.Info);
  }

  [Fact]
  public void CompleteAppliesOutcomes()
  {
    var completed = AddItem(@"C:\in\ok.wav");
    var failed = AddItem(@"C:\in\bad.wav");
    var skipped = AddItem(@"C:\in\skip.wav");
    _presenter.BeginRun();
    var result = new ConversionRunResult(
      new ConversionRunSummary(1, 1, 1),
      new[]
      {
        new ConversionOutcome(completed.Id, true, false, @"C:\out\ok.wav", null, null),
        new ConversionOutcome(failed.Id, false, false, null, "boom", null),
        new ConversionOutcome(skipped.Id, true, true, null, null, null),
      });

    _presenter.Complete(result);

    Assert.Equal(ConversionStage.Completed, completed.Stage);
    Assert.Equal(1, completed.Progress);
    Assert.Equal(@"C:\out\ok.wav", completed.OutputPath);
    Assert.Equal(ConversionStage.Failed, failed.Stage);
    Assert.Equal("boom", failed.Error);
    Assert.Equal(ConversionStage.Skipped, skipped.Stage);
  }

  [Fact]
  public void CompleteWithCancellationResetsOnlyInFlightItems()
  {
    var first = AddItem(@"C:\in\a.wav");
    var second = AddItem(@"C:\in\b.wav");
    _presenter.BeginRun();
    var handler = _presenter.CreateProgressHandler();
    handler.Report(new ConversionProgress(first.Id, ConversionStage.Converting, 0.7, null));
    second.Stage = ConversionStage.Failed;

    _presenter.Complete(new ConversionRunResult(
      new ConversionRunSummary(0, 0, 0, Cancelled: true),
      Array.Empty<ConversionOutcome>()));

    Assert.Equal(ConversionStage.Pending, first.Stage);
    Assert.Equal(0, first.Progress);
    Assert.Equal(ConversionStage.Failed, second.Stage);
  }
}
