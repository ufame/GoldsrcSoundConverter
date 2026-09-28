using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class MainViewModelTests : IDisposable
{
  private static readonly float[] WaveformMins = { -0.5f };
  private static readonly float[] WaveformMaxs = { 0.5f };

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
  public void AddFilesAddsSupportedFiles()
  {
    var file = CreateFile("sound.wav");
    _filePicker.FilesToPick = new[] { file };
    var vm = CreateViewModel();

    vm.AddFilesCommand.Execute(null);

    var item = Assert.Single(vm.Items);
    Assert.Equal(file, item.SourcePath);
    Assert.True(vm.CanStart);
  }

  [Fact]
  public void AddPathsIgnoresDuplicatesAndUnsupportedFiles()
  {
    var wave = CreateFile("sound.wav");
    var text = CreateFile("notes.txt");
    var vm = CreateViewModel();

    vm.AddPaths(new[] { wave, wave, text });

    Assert.Single(vm.Items);
    Assert.Equal(wave, vm.Items[0].SourcePath);
  }

  [Fact]
  public void RemoveSelectedReindexesAndKeepsStableIds()
  {
    var first = CreateFile("a.wav");
    var second = CreateFile("b.wav");
    var vm = CreateViewModel();
    vm.AddPaths(new[] { first, second });
    var secondId = vm.Items[1].Id;

    vm.SelectedItem = vm.Items[0];
    vm.RemoveSelectedCommand.Execute(null);

    var remaining = Assert.Single(vm.Items);
    Assert.Equal(second, remaining.SourcePath);
    Assert.Equal(0, remaining.Index);
    Assert.Equal(secondId, remaining.Id);
  }

  [Fact]
  public void StartPassesSnapshotAndOptionsToConversionService()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.ogg") });
    var ids = vm.Items.Select(i => i.Id).ToArray();
    vm.Parallelism = 2;
    vm.Mp3BitrateKbps = 192;

    vm.StartCommand.Execute(null);

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
    vm.AddPaths(new[] { CreateFile("ok.wav"), CreateFile("bad.wav"), CreateFile("skip.wav") });
    _conversion.OutcomeFactory = item => item.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)
      ? new ConversionOutcome(item.Id, false, false, null, "boom", null)
      : item.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)
        ? new ConversionOutcome(item.Id, true, true, null, null, null)
        : new ConversionOutcome(item.Id, true, false, item.SourcePath + ".out.wav", null, null);

    vm.StartCommand.Execute(null);

    Assert.Equal(ConversionStage.Completed, vm.Items.Single(i => i.SourcePath.EndsWith("ok.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Failed, vm.Items.Single(i => i.SourcePath.EndsWith("bad.wav", StringComparison.Ordinal)).Stage);
    Assert.Equal(ConversionStage.Skipped, vm.Items.Single(i => i.SourcePath.EndsWith("skip.wav", StringComparison.Ordinal)).Stage);
    Assert.Contains("успешно 1", vm.StatusText);
    Assert.Contains("ошибок 1", vm.StatusText);
    Assert.Contains("пропущено 1", vm.StatusText);
  }

  [Fact]
  public async Task QueueEditingIsLockedWhileBusy()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.IsBusy);

    Assert.False(vm.AddFilesCommand.CanExecute(null));
    Assert.False(vm.AddFolderCommand.CanExecute(null));
    Assert.False(vm.RemoveSelectedCommand.CanExecute(null));
    Assert.False(vm.ClearCommand.CanExecute(null));
    Assert.False(vm.CanEditQueue);

    _conversion.ConversionGate.SetResult();
    await startTask;

    Assert.True(vm.CanEditQueue);
  }

  [Fact]
  public async Task AddPathsDuringConversionIsRejected()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.IsBusy);
    vm.AddPaths(new[] { CreateFile("b.wav") });

    Assert.Single(vm.Items);
    Assert.Contains("Дождитесь", vm.StatusText);

    _conversion.ConversionGate.SetResult();
    await startTask;
  }

  [Fact]
  public async Task CancelRequestsCancellation()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("a.wav") });
    _conversion.ConversionGate = new TaskCompletionSource();

    var startTask = vm.StartCommand.ExecuteAsync(null);
    await WaitUntil(() => vm.IsBusy);
    vm.CancelCommand.Execute(null);
    await startTask;

    Assert.Contains("отменена", vm.StatusText);
  }

  [Fact]
  public async Task PlayPreparesSourceAndStartsPlayback()
  {
    var file = CreateFile("sound.wav");
    var vm = CreateViewModel();
    vm.AddPaths(new[] { file });
    vm.SelectedItem = vm.Items[0];

    await vm.PlayCommand.ExecuteAsync(null);

    Assert.Equal(file, _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public async Task PlaySelectionUsesTrimRange()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("sound.wav") });
    var item = vm.Items[0];
    item.TrimStartSeconds = 1.5;
    item.TrimEndSeconds = 4.0;
    vm.SelectedItem = item;

    await vm.PlaySelectionCommand.ExecuteAsync(null);

    var selection = Assert.Single(_playback.Selections);
    Assert.Equal(1.5, selection.Start, 3);
    Assert.Equal(4.0, selection.End, 3);
  }

  [Fact]
  public void PauseAndStopDelegateToPlayback()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("sound.wav") });
    vm.SelectedItem = vm.Items[0];
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
    vm.AddPaths(new[] { CreateFile("sound.wav") });
    vm.SelectedItem = vm.Items[0];

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
    vm.AddPaths(new[] { CreateFile("sound.wav") });
    vm.SelectedItem = vm.Items[0];

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
  public void CustomFfmpegPathPropagatesToService()
  {
    var vm = CreateViewModel();

    vm.FfmpegCustomPath = @"C:\ff\ffmpeg.exe";

    Assert.Contains(@"C:\ff\ffmpeg.exe", _conversion.CustomPaths);
  }

  [Fact]
  public async Task SelectingItemLoadsWaveformFromService()
  {
    _conversion.Waveform = new WaveformData(WaveformMins, WaveformMaxs, 64, 8000);
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("sound.wav") });

    vm.SelectedItem = vm.Items[0];

    await WaitUntil(() => vm.Items[0].Waveform is not null);
    Assert.NotNull(vm.Items[0].Waveform);
  }

  private MainViewModel CreateViewModel()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    return new MainViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      _conversion,
      _playback,
      new QueueManager(_conversion, _log),
      new ConversionRequestFactory(),
      new PresetCatalog(),
      new ConversionRunController(_conversion, new ConversionRequestFactory(), _log),
      _log);
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
