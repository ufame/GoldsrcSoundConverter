using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class MainViewModelTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeSettingsStore _settings = new();
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeFolderLauncher _folderLauncher = new();
  private readonly FakeConversionService _conversion = new();
  private readonly FakePlaybackController _playback = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void StartPassesSnapshotAndOptionsToConversionService()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.ogg") });
    var ids = vm.Queue.Items.Select(i => i.Id).ToArray();
    vm.Conversion.Parallelism = 2;
    vm.Conversion.Mp3BitrateKbps = 192;

    vm.Conversion.StartCommand.Execute(null);

    var call = Assert.Single(_conversion.ConvertCalls);
    Assert.Equal(2, call.WorkItems.Count);
    Assert.Equal(ids, call.WorkItems.Select(w => w.Id));
    Assert.Equal(2, call.Options.Parallelism);
    Assert.Equal(192, call.Options.Mp3BitrateKbps);
    Assert.Equal(_temp.Path, call.Options.OutputDirectory);
    Assert.True(_settings.SaveCount > 0);
  }

  [Fact]
  public void StartAppliesOutcomesToQueueItems()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("ok.wav"), CreateFile("bad.wav"), CreateFile("skip.wav") });
    _conversion.OutcomeFactory = item => item.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)
      ? new ConversionOutcome(item.Id, false, false, null, "boom", null)
      : item.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)
        ? new ConversionOutcome(item.Id, true, true, null, null, null)
        : new ConversionOutcome(item.Id, true, false, item.SourcePath + ".out.wav", null, null);

    vm.Conversion.StartCommand.Execute(null);

    Assert.Equal(ConversionStage.Completed, vm.Queue.Items.Single(i => i.SourcePath.EndsWith("ok.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Failed, vm.Queue.Items.Single(i => i.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Skipped, vm.Queue.Items.Single(i => i.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)).Stage);
    Assert.Contains("успешно 1", vm.StatusText);
    Assert.Contains("ошибок 1", vm.StatusText);
    Assert.Contains("пропущено 1", vm.StatusText);
  }

  [Fact]
  public async Task QueueEditingIsLockedWhileBusy()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.Conversion.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.Conversion.IsBusy);

    Assert.False(vm.Queue.AddFilesCommand.CanExecute(null));
    Assert.False(vm.Queue.AddFolderCommand.CanExecute(null));
    Assert.False(vm.Queue.RemoveSelectedCommand.CanExecute(null));
    Assert.False(vm.Queue.ClearCommand.CanExecute(null));
    Assert.False(vm.Queue.CanEditQueue);

    _conversion.ConversionGate.SetResult();
    await startTask;

    Assert.True(vm.Queue.CanEditQueue);
  }

  [Fact]
  public async Task AddPathsDuringConversionIsRejected()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.Conversion.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.Conversion.IsBusy);
    vm.Queue.AddPaths(new[] { CreateFile("b.wav") });

    Assert.Single(vm.Queue.Items);
    Assert.Contains("Дождитесь", vm.StatusText);

    _conversion.ConversionGate.SetResult();
    await startTask;
  }

  [Fact]
  public async Task CancelRequestsCancellation()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.Conversion.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.Conversion.IsBusy);
    vm.Conversion.CancelCommand.Execute(null);
    await startTask;

    Assert.Contains("отменена", vm.StatusText);
  }

  [Fact]
  public async Task PlayPreparesSourceAndStartsPlayback()
  {
    var file = CreateFile("sound.wav");
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { file });
    vm.Queue.SelectedItem = vm.Queue.Items[0];

    await vm.PlayCommand.ExecuteAsync(null);

    Assert.Equal(file, _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public async Task PlaySelectionUsesTrimRange()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("sound.wav") });
    var item = vm.Queue.Items[0];
    item.TrimStartSeconds = 1.5;
    item.TrimEndSeconds = 4.0;
    vm.Queue.SelectedItem = item;

    await vm.PlaySelectionCommand.ExecuteAsync(null);

    var selection = Assert.Single(_playback.Selections);
    Assert.Equal(1.5, selection.Start, 3);
    Assert.Equal(4.0, selection.End, 3);
  }

  [Fact]
  public void PauseAndStopDelegateToPlayback()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("sound.wav") });
    vm.Queue.SelectedItem = vm.Queue.Items[0];
    var stopsAfterSelection = _playback.StopCount;

    vm.PauseCommand.Execute(null);
    vm.StopCommand.Execute(null);

    Assert.Equal(1, _playback.PauseCount);
    Assert.Equal(stopsAfterSelection + 1, _playback.StopCount);
    Assert.Equal(0, vm.PlaybackPositionSeconds);
  }

  [Fact]
  public async Task PreviewResultDelegatesToPlaybackController()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("sound.wav") });
    vm.Queue.SelectedItem = vm.Queue.Items[0];

    await vm.PreviewResultCommand.ExecuteAsync(null);

    Assert.NotNull(_playback.PreparedPath);
    Assert.EndsWith(".result.wav", _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public void PlayFailureReportsStatus()
  {
    _playback.PrepareException = new InvalidOperationException("нет декодера");
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("sound.wav") });
    vm.Queue.SelectedItem = vm.Queue.Items[0];

    vm.PlayCommand.Execute(null);

    Assert.Contains("нет декодера", vm.StatusText);
  }

  [Fact]
  public void OpenOutputFolderUsesLauncher()
  {
    var vm = CreateViewModel();

    vm.OpenOutputFolderCommand.Execute(null);

    Assert.Contains(_temp.Path, _folderLauncher.OpenedPaths);
  }

  [Fact]
  public void OpenMissingOutputFolderReportsStatus()
  {
    var vm = CreateViewModel();
    vm.OutputDirectory = Path.Combine(_temp.Path, "missing");

    vm.OpenOutputFolderCommand.Execute(null);

    Assert.Empty(_folderLauncher.OpenedPaths);
    Assert.Contains("не создана", vm.StatusText);
  }

  [Fact]
  public void BrowseOutputDirectoryUsesPicker()
  {
    var vm = CreateViewModel();
    _filePicker.FolderToPick = _temp.Path;

    vm.BrowseOutputDirectoryCommand.Execute(null);

    Assert.Equal(_temp.Path, vm.OutputDirectory);
  }

  [Fact]
  public void VolumePropagatesToPlayback()
  {
    var vm = CreateViewModel();

    vm.PlaybackVolume = 0.25;

    Assert.Equal(0.25, _playback.Volume, 3);
  }

  [Fact]
  public void SelectionChangeResetsPlaybackState()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.wav") });
    vm.Queue.SelectedItem = vm.Queue.Items[0];
    vm.PlaybackPositionSeconds = 12.5;
    var stopsBefore = _playback.StopCount;

    vm.Queue.SelectedItem = vm.Queue.Items[1];

    Assert.Equal(stopsBefore + 1, _playback.StopCount);
    Assert.Equal(0, vm.PlaybackPositionSeconds, 3);
    Assert.Equal("00:00.000 / 00:00.000", vm.PlaybackPositionText);
    Assert.True(vm.HasSelection);
  }

  [Fact]
  public void DisposeDetachesFromPlaybackEvents()
  {
    var vm = CreateViewModel();

    vm.Dispose();

    _playback.PositionSeconds = 42;
    _playback.RaisePositionChanged();

    Assert.Equal(0, vm.PlaybackPositionSeconds, 3);
  }

  [Fact]
  public void DisposeLeavesChildLifecycleToTheScope()
  {
    var runController = new FakeRunController();
    var vm = CreateViewModel(runController);

    vm.Dispose();

    runController.IsBusy = true;
    runController.RaiseBusyChanged();

    Assert.True(vm.Conversion.IsBusy);
    Assert.False(runController.Disposed);
  }

  private MainViewModel CreateViewModel(IConversionRunController? runController = null)
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var queue = new QueueManager(_conversion, _log);
    var queueVm = new QueueViewModel(queue, _filePicker, new WaveformLoader(_conversion, _log));
    var outputDirectory = new OutputDirectoryProvider();
    var conversion = new ConversionViewModel(
      queueVm,
      _filePicker,
      _conversion,
      new ConversionRequestFactory(),
      runController ?? new ConversionRunController(_conversion, _log),
      new QueueConversionPresenter(queue),
      _log,
      outputDirectory);
    return new MainViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      new PlaybackCoordinator(_playback, _log),
      queueVm,
      conversion,
      new PresetCatalog(),
      _log,
      outputDirectory);
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
}
