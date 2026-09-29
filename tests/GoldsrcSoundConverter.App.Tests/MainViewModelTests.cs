using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
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
  public void StartSavesSettingsOnRunFinished()
  {
    var vm = CreateViewModel();
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });
    var savesBefore = _settings.SaveCount;

    vm.Conversion.StartCommand.Execute(null);

    Assert.Equal(savesBefore + 1, _settings.SaveCount);
  }

  [Fact]
  public void ConversionStatusIsForwardedToShell()
  {
    var vm = CreateViewModel();
    string? status = null;
    vm.PropertyChanged += (_, e) =>
    {
      if (e.PropertyName == nameof(MainViewModel.StatusText))
      {
        status = vm.StatusText;
      }
    };
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });

    vm.Conversion.StartCommand.Execute(null);

    Assert.Contains("Готово: успешно 1", status);
  }

  [Fact]
  public void QueueStatusIsForwardedToShell()
  {
    var vm = CreateViewModel();

    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal("Добавлено файлов: 1", vm.StatusText);
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
  public void SaveSettingsCommandPersistsAndReportsStatus()
  {
    var vm = CreateViewModel();
    var savesBefore = _settings.SaveCount;

    vm.SaveSettingsCommand.Execute(null);

    Assert.Equal(savesBefore + 1, _settings.SaveCount);
    Assert.Equal("Настройки сохранены", vm.StatusText);
  }

  [Fact]
  public void LoadedSettingsAreAppliedToConversion()
  {
    _settings.Settings.Format = Core.Models.OutputAudioFormat.Mp3;
    _settings.Settings.SampleRate = 44100;
    _settings.Settings.Channels = Core.Models.TargetChannels.Stereo;
    _settings.Settings.Mp3BitrateKbps = 192;

    var vm = CreateViewModel();

    Assert.Equal(Core.Models.OutputAudioFormat.Mp3, vm.Conversion.Format);
    Assert.Equal(44100, vm.Conversion.SampleRate);
    Assert.Equal(Core.Models.TargetChannels.Stereo, vm.Conversion.Channels);
    Assert.Equal(192, vm.Conversion.Mp3BitrateKbps);
    Assert.Equal("mp3-music-hq", vm.Presets.SelectedItem!.Id);
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
    var playback = new PlaybackViewModel(queueVm, conversion, new PlaybackCoordinator(_playback, _log));
    var presets = new PresetViewModel(conversion, new PresetCatalog());
    return new MainViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      queueVm,
      conversion,
      playback,
      presets,
      _log,
      outputDirectory);
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }
}
