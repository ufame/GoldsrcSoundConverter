using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class PlaybackViewModelTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeConversionService _conversion = new();
  private readonly FakePlaybackController _playback = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public async Task PlayPreparesSourceAndStartsPlayback()
  {
    var file = CreateFile("sound.wav");
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { file });
    queue.SelectedItem = queue.Items[0];

    await vm.PlayCommand.ExecuteAsync(null);

    Assert.Equal(file, _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public async Task PlaySelectionUsesTrimRange()
  {
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    var item = queue.Items[0];
    item.TrimStartSeconds = 1.5;
    item.TrimEndSeconds = 4.0;
    queue.SelectedItem = item;

    await vm.PlaySelectionCommand.ExecuteAsync(null);

    var selection = Assert.Single(_playback.Selections);
    Assert.Equal(1.5, selection.Start, 3);
    Assert.Equal(4.0, selection.End, 3);
  }

  [Fact]
  public void PauseAndStopDelegateToPlayback()
  {
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];
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
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];

    await vm.PreviewResultCommand.ExecuteAsync(null);

    Assert.NotNull(_playback.PreparedPath);
    Assert.EndsWith(".result.wav", _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public void PlayFailureReportsStatus()
  {
    _playback.PrepareException = new InvalidOperationException("нет декодера");
    var (vm, queue) = CreateViewModel();
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];

    vm.PlayCommand.Execute(null);

    Assert.Contains("нет декодера", status);
  }

  [Fact]
  public void TrimCommandsReportStatus()
  {
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;

    vm.SetTrimStartCommand.Execute(null);

    Assert.NotNull(status);

    vm.ResetTrimCommand.Execute(null);

    Assert.NotNull(status);
  }

  [Fact]
  public void VolumePropagatesToPlayback()
  {
    var (vm, _) = CreateViewModel();

    vm.PlaybackVolume = 0.25;

    Assert.Equal(0.25, _playback.Volume, 3);
  }

  [Fact]
  public void CommandsRequireSelection()
  {
    var (vm, queue) = CreateViewModel();

    Assert.False(vm.HasSelection);
    Assert.False(vm.PlayCommand.CanExecute(null));
    Assert.False(vm.StopCommand.CanExecute(null));

    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];

    Assert.True(vm.HasSelection);
    Assert.True(vm.PlayCommand.CanExecute(null));
    Assert.True(vm.StopCommand.CanExecute(null));
  }

  [Fact]
  public void SelectionChangeResetsPlaybackState()
  {
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.wav") });
    queue.SelectedItem = queue.Items[0];
    vm.PlaybackPositionSeconds = 12.5;
    var stopsBefore = _playback.StopCount;

    queue.SelectedItem = queue.Items[1];

    Assert.Equal(stopsBefore + 1, _playback.StopCount);
    Assert.Equal(0, vm.PlaybackPositionSeconds, 3);
    Assert.Equal("00:00.000 / 00:00.000", vm.PlaybackPositionText);
    Assert.Equal("b.wav", vm.EditorTitle);
  }

  [Fact]
  public void ClearingSelectionResetsEditorTitle()
  {
    var (vm, queue) = CreateViewModel();
    queue.AddPaths(new[] { CreateFile("sound.wav") });
    queue.SelectedItem = queue.Items[0];

    queue.SelectedItem = null;

    Assert.False(vm.HasSelection);
    Assert.Equal("Выберите файл в очереди", vm.EditorTitle);
  }

  [Fact]
  public void PositionChangedUpdatesText()
  {
    var (vm, _) = CreateViewModel();
    _playback.PositionSeconds = 5;
    _playback.TotalSeconds = 10;

    _playback.RaisePositionChanged();

    Assert.Equal(5, vm.PlaybackPositionSeconds, 3);
    Assert.Equal("00:05.000 / 00:10.000", vm.PlaybackPositionText);
  }

  [Fact]
  public void DisposeDetachesFromPlaybackEvents()
  {
    var (vm, _) = CreateViewModel();

    vm.Dispose();

    _playback.PositionSeconds = 42;
    _playback.RaisePositionChanged();

    Assert.Equal(0, vm.PlaybackPositionSeconds, 3);
  }

  private (PlaybackViewModel ViewModel, QueueViewModel Queue) CreateViewModel()
  {
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
    return (new PlaybackViewModel(queueVm, conversion, new PlaybackCoordinator(_playback, _log)), queueVm);
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }
}
