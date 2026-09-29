using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class ConversionViewModelTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void StartPassesSnapshotAndOptionsToConversionService()
  {
    var vm = CreateConversion(out var ctx, outputDirectory: _temp.Path);
    ctx.Queue.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.ogg") });
    var ids = ctx.Queue.Items.Select(i => i.Id).ToArray();
    vm.Parallelism = 2;
    vm.Mp3BitrateKbps = 192;

    vm.StartCommand.Execute(null);

    var call = Assert.Single(_conversion.ConvertCalls);
    Assert.Equal(2, call.WorkItems.Count);
    Assert.Equal(ids, call.WorkItems.Select(w => w.Id));
    Assert.Equal(2, call.Options.Parallelism);
    Assert.Equal(192, call.Options.Mp3BitrateKbps);
    Assert.Equal(_temp.Path, call.Options.OutputDirectory);
  }

  [Fact]
  public void StartWithoutOutputDirectoryReportsStatus()
  {
    var vm = CreateConversion(out var ctx, outputDirectory: "");
    ctx.Queue.AddPaths(new[] { CreateFile("a.wav") });
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;

    vm.StartCommand.Execute(null);

    Assert.Equal("Укажите папку результатов", status);
    Assert.Empty(_conversion.ConvertCalls);
  }

  [Fact]
  public void StartAppliesOutcomesAndReportsSummary()
  {
    var vm = CreateConversion(out var ctx);
    ctx.Queue.AddPaths(new[] { CreateFile("ok.wav"), CreateFile("bad.wav"), CreateFile("skip.wav") });
    _conversion.OutcomeFactory = item => item.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)
      ? new ConversionOutcome(item.Id, false, false, null, "boom", null)
      : item.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)
        ? new ConversionOutcome(item.Id, true, true, null, null, null)
        : new ConversionOutcome(item.Id, true, false, item.SourcePath + ".out.wav", null, null);
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;

    vm.StartCommand.Execute(null);

    Assert.Equal(ConversionStage.Completed, ctx.Queue.Items.Single(i => i.SourcePath.EndsWith("ok.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Failed, ctx.Queue.Items.Single(i => i.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Skipped, ctx.Queue.Items.Single(i => i.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(1, vm.OverallProgress, 3);
    Assert.Contains("успешно 1", status);
    Assert.Contains("ошибок 1", status);
    Assert.Contains("пропущено 1", status);
  }

  [Fact]
  public async Task BusyStateLocksQueueAndDisablesCommands()
  {
    var vm = CreateConversion(out var ctx);
    ctx.Queue.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.IsBusy);

    Assert.False(vm.StartCommand.CanExecute(null));
    Assert.True(vm.CancelCommand.CanExecute(null));
    Assert.False(ctx.Queue.AddFilesCommand.CanExecute(null));
    Assert.False(ctx.Queue.CanEditQueue);

    _conversion.ConversionGate.SetResult();
    await startTask;

    Assert.False(vm.IsBusy);
    Assert.True(ctx.Queue.CanEditQueue);
  }

  [Fact]
  public async Task CancelRequestsCancellation()
  {
    var vm = CreateConversion(out var ctx);
    ctx.Queue.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;

    var startTask = vm.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.IsBusy);
    vm.CancelCommand.Execute(null);
    await startTask;

    Assert.Equal("Конвертация отменена", status);
  }

  [Fact]
  public void CanStartRequiresQueueItems()
  {
    var vm = CreateConversion(out var ctx);

    Assert.False(vm.CanStart);

    ctx.Queue.AddPaths(new[] { CreateFile("a.wav") });

    Assert.True(vm.CanStart);
  }

  [Fact]
  public void FormatChangeRaisesEventAndTogglesFlags()
  {
    var vm = CreateConversion(out _);
    var raised = 0;
    vm.FormatChanged += (_, _) => raised++;

    vm.Format = OutputAudioFormat.Mp3;

    Assert.True(vm.IsMp3Format);
    Assert.False(vm.IsWavFormat);
    Assert.Equal(1, raised);

    vm.Format = OutputAudioFormat.Mp3;

    Assert.Equal(1, raised);
  }

  [Fact]
  public void OptionChangeRaisesOptionsChanged()
  {
    var vm = CreateConversion(out _);
    var raised = 0;
    vm.OptionsChanged += (_, _) => raised++;

    vm.SampleRate = 44100;
    vm.NormalizePeak = true;

    Assert.Equal(1, raised);
  }

  [Fact]
  public void CustomFfmpegPathPropagatesToService()
  {
    var vm = CreateConversion(out _);

    vm.FfmpegCustomPath = @"C:\ff\ffmpeg.exe";

    Assert.Contains(@"C:\ff\ffmpeg.exe", _conversion.CustomPaths);
  }

  [Fact]
  public void RunFinishedRaisedAfterConversion()
  {
    var vm = CreateConversion(out var ctx);
    ctx.Queue.AddPaths(new[] { CreateFile("a.wav") });
    var finished = 0;
    vm.RunFinished += (_, _) => finished++;

    vm.StartCommand.Execute(null);

    Assert.Equal(1, finished);
  }

  [Fact]
  public void DisposeDetachesFromRunController()
  {
    var runController = new FakeRunController();
    var vm = CreateConversion(out _, runController);

    vm.Dispose();
    vm.Dispose();

    runController.IsBusy = true;
    runController.RaiseBusyChanged();

    Assert.False(vm.IsBusy);
  }

  [Fact]
  public void BeginRunResetsStateAndReturnsSnapshot()
  {
    var vm = CreateConversion(out var ctx);
    var first = AddItem(ctx.Queue, @"C:\in\a.wav");
    first.Stage = ConversionStage.Failed;
    first.Progress = 0.5;
    first.Error = "old";
    first.OutputPath = "old.wav";
    var second = AddItem(ctx.Queue, @"C:\in\b.wav");

    var workItems = vm.BeginRun();

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
    var vm = CreateConversion(out var ctx);
    var item = new QueueItemViewModel(@"C:\in\clip.ogg", @"C:\in")
    {
      TrimStartSeconds = 1,
      TrimEndSeconds = 4,
    };
    item.Info = new AudioInfo(item.SourcePath, "ogg", "vorbis", TimeSpan.FromSeconds(5), 44100, 2, 128000, 10);
    ctx.Queue.Items.Add(item);

    var workItem = Assert.Single(vm.BeginRun());

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
    var vm = CreateConversion(out var ctx);
    var first = AddItem(ctx.Queue, @"C:\in\a.wav");
    var second = AddItem(ctx.Queue, @"C:\in\b.wav");
    vm.BeginRun();
    var overall = new List<double>();
    var handler = vm.CreateProgressHandler(overall.Add);

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
    var vm = CreateConversion(out var ctx);
    vm.BeginRun();
    var late = AddItem(ctx.Queue, @"C:\in\late.wav");
    var handler = vm.CreateProgressHandler();

    handler.Report(new ConversionProgress(late.Id, ConversionStage.Converting, 0.5, null));

    Assert.Equal(ConversionStage.Pending, late.Stage);
    Assert.Equal(0, late.Progress);
  }

  [Fact]
  public void ProbeHandlerAppliesInfoAndTargetSize()
  {
    var vm = CreateConversion(out var ctx);
    var item = AddItem(ctx.Queue, @"C:\in\a.wav");
    vm.BeginRun();
    var handler = vm.CreateProbeHandler(RunOptions());

    handler.Report(Probe(item.SourcePath, item.Id));

    Assert.NotNull(item.Info);
    Assert.NotEqual("—", item.TargetSizeText);
  }

  [Fact]
  public void ProbeIsIgnoredWhenInfoAlreadyKnown()
  {
    var vm = CreateConversion(out var ctx);
    var item = AddItem(ctx.Queue, @"C:\in\a.wav");
    var known = new AudioInfo(item.SourcePath, "wav", "pcm_s16le", TimeSpan.FromSeconds(1), 22050, 1, 352800, 50);
    item.Info = known;
    vm.BeginRun();
    var handler = vm.CreateProbeHandler(RunOptions());

    handler.Report(Probe(item.SourcePath, item.Id));

    Assert.Same(known, item.Info);
  }

  [Fact]
  public void CompleteAppliesOutcomes()
  {
    var vm = CreateConversion(out var ctx);
    var completed = AddItem(ctx.Queue, @"C:\in\ok.wav");
    var failed = AddItem(ctx.Queue, @"C:\in\bad.wav");
    var skipped = AddItem(ctx.Queue, @"C:\in\skip.wav");
    vm.BeginRun();
    var result = new ConversionRunResult(
      new ConversionRunSummary(1, 1, 1),
      new[]
      {
        new ConversionOutcome(completed.Id, true, false, @"C:\out\ok.wav", null, null),
        new ConversionOutcome(failed.Id, false, false, null, "boom", null),
        new ConversionOutcome(skipped.Id, true, true, null, null, null),
      });

    vm.Complete(result);

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
    var vm = CreateConversion(out var ctx);
    var first = AddItem(ctx.Queue, @"C:\in\a.wav");
    var second = AddItem(ctx.Queue, @"C:\in\b.wav");
    vm.BeginRun();
    var handler = vm.CreateProgressHandler();
    handler.Report(new ConversionProgress(first.Id, ConversionStage.Converting, 0.7, null));
    second.Stage = ConversionStage.Failed;

    vm.Complete(new ConversionRunResult(
      new ConversionRunSummary(0, 0, 0, Cancelled: true),
      Array.Empty<ConversionOutcome>()));

    Assert.Equal(ConversionStage.Pending, first.Stage);
    Assert.Equal(0, first.Progress);
    Assert.Equal(ConversionStage.Failed, second.Stage);
  }

  private static ConversionOptions RunOptions()
  {
    return new ConversionOptions
    {
      OutputDirectory = @"C:\out",
      AsciiNames = true,
      LowercaseNames = true,
    };
  }

  private static ProbeResult Probe(string path, Guid id)
  {
    return new ProbeResult(id, new AudioInfo(path, "wav", "pcm_s16le", TimeSpan.FromSeconds(10), 22050, 1, 352800, 100));
  }

  private static QueueItemViewModel AddItem(QueueViewModel queue, string path)
  {
    var item = new QueueItemViewModel(path, null);
    queue.Items.Add(item);
    return item;
  }

  private ConversionViewModel CreateConversion(
    out Context context,
    IConversionRunController? runController = null,
    string outputDirectory = @"C:\out")
  {
    var queue = new QueueManager(_conversion, _log);
    var queueVm = new QueueViewModel(queue, _filePicker, new WaveformLoader(_conversion, _log));
    context = new Context(queueVm);
    return new ConversionViewModel(
      queueVm,
      _filePicker,
      _conversion,
      new ConversionRequestFactory(),
      runController ?? new ConversionRunController(_conversion, _log),
            _log,
      new OutputDirectoryProvider { Value = outputDirectory });
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
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

  public sealed record Context(QueueViewModel Queue);
}
