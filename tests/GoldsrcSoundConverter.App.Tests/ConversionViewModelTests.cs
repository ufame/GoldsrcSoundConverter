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
  public void DisposeDetachesFromRunControllerAndDisposesIt()
  {
    var runController = new FakeRunController();
    var vm = CreateConversion(out _, runController);

    vm.Dispose();
    vm.Dispose();

    runController.IsBusy = true;
    runController.RaiseBusyChanged();

    Assert.False(vm.IsBusy);
    Assert.True(runController.Disposed);
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
      new QueueConversionPresenter(queue),
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
