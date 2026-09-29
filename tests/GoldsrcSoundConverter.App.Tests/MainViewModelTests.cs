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
  public void PresetListMatchesFormatAndResolvesSelection()
  {
    var vm = CreateViewModel();

    Assert.NotEmpty(vm.Presets);
    Assert.NotNull(vm.SelectedPreset);

    vm.Conversion.Format = Core.Models.OutputAudioFormat.Mp3;

    Assert.All(
      vm.Presets.Where(preset => !preset.IsCustom),
      preset => Assert.Equal(Core.Models.OutputAudioFormat.Mp3, preset.Format));
    Assert.Contains(vm.Presets, preset => preset.IsCustom);
    Assert.NotNull(vm.SelectedPreset);
  }

  [Fact]
  public void ApplyingPresetUpdatesConversionOptions()
  {
    var vm = CreateViewModel();
    var preset = vm.Presets.First(p => p.SampleRate != vm.Conversion.SampleRate);

    vm.SelectedPreset = preset;

    Assert.Equal(preset.SampleRate, vm.Conversion.SampleRate);
    Assert.Equal(preset.Channels, vm.Conversion.Channels);
    Assert.Equal(preset.BitDepth, vm.Conversion.BitDepth);
    Assert.Equal(preset.Mp3BitrateKbps, vm.Conversion.Mp3BitrateKbps);
  }

  [Fact]
  public void EditingConversionOptionsSelectsCustomPreset()
  {
    var vm = CreateViewModel();

    vm.Conversion.SampleRate = vm.Conversion.SampleRate == 44100 ? 22050 : 44100;

    Assert.Equal(Core.Models.Cs16Presets.CustomId, vm.SelectedPreset?.Id);
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
    return new MainViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      queueVm,
      conversion,
      playback,
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
}
