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
  public void StartupMessageIsLogged()
  {
    var vm = CreateViewModel();

    Assert.Contains(vm.Log.Entries, entry => entry.Contains("Готово к работе", StringComparison.Ordinal));
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
    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });

    vm.Conversion.StartCommand.Execute(null);

    Assert.Contains("Готово: успешно 1", vm.StatusText);
  }

  [Fact]
  public void QueueStatusIsForwardedToShell()
  {
    var vm = CreateViewModel();

    vm.Queue.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal("Добавлено файлов: 1", vm.StatusText);
  }

  [Fact]
  public void SettingsStatusIsForwardedToShell()
  {
    var vm = CreateViewModel();

    vm.Settings.SaveSettingsCommand.Execute(null);

    Assert.Equal("Настройки сохранены", vm.StatusText);
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
            _log,
      outputDirectory);
    var playback = new PlaybackViewModel(queueVm, conversion, new PlaybackCoordinator(_playback, _log));
    var presets = new PresetViewModel(conversion, new PresetCatalog());
    var settings = new SettingsViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      conversion,
      presets,
      _log,
      outputDirectory);
    return new MainViewModel(queueVm, conversion, playback, presets, settings, new LogViewModel(_log));
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }
}
