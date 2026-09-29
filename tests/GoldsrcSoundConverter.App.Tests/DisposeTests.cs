using GoldsrcSoundConverter.App.Infrastructure.Audio;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class DisposeTests : IDisposable
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
  public void ConversionServiceDisposesIdempotently()
  {
    var service = new ConversionService(new FakeProcessRunner(), new FfmpegBootstrapper(_temp.Path));

    service.Dispose();
    service.Dispose();
  }

  [Fact]
  public void ConversionRunControllerDisposesIdempotently()
  {
    var controller = new ConversionRunController(_conversion, _log);

    controller.Dispose();
    controller.Dispose();
  }

  [Fact]
  public void PlaybackControllerDisposesPreviewIdempotently()
  {
    var preview = new FakeAudioPreview();
    var controller = new PlaybackController(preview, _conversion);

    controller.Dispose();
    controller.Dispose();

    Assert.False(preview.HasTrack);
  }

  [Fact]
  public void AudioPreviewServiceDisposesWithoutTrack()
  {
    var service = new AudioPreviewService();

    service.Dispose();
    service.Dispose();

    Assert.False(service.HasTrack);
  }

  [Fact]
  public void MainViewModelDisposesIdempotently()
  {
    var vm = CreateViewModel();

    vm.Dispose();
    vm.Dispose();
  }

  private MainViewModel CreateViewModel()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var queue = new QueueManager(_conversion, _log);
    var queueVm = new QueueViewModel(queue, _filePicker, new WaveformLoader(_conversion, _log));
    var outputDirectory = new OutputDirectoryProvider { Value = _temp.Path };
    var conversion = new ConversionViewModel(
      queueVm,
      _filePicker,
      _conversion,
      new ConversionRequestFactory(),
      new ConversionRunController(_conversion, _log),
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
}
