using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
  private readonly TempDirectory _temp = new();
  private readonly FakeSettingsStore _settings = new();
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeFolderLauncher _folderLauncher = new();
  private readonly FakeConversionService _conversionService = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void LoadAppliesSettingsToConversionAndPreset()
  {
    _settings.Settings.Format = OutputAudioFormat.Mp3;
    _settings.Settings.SampleRate = 44100;
    _settings.Settings.Channels = TargetChannels.Stereo;
    _settings.Settings.Mp3BitrateKbps = 192;
    _settings.Settings.OutputDirectory = _temp.Path;

    var ctx = CreateViewModel();

    Assert.Equal(OutputAudioFormat.Mp3, ctx.Conversion.Format);
    Assert.Equal(44100, ctx.Conversion.SampleRate);
    Assert.Equal(TargetChannels.Stereo, ctx.Conversion.Channels);
    Assert.Equal(192, ctx.Conversion.Mp3BitrateKbps);
    Assert.Equal(_temp.Path, ctx.ViewModel.OutputDirectory);
    Assert.Equal("mp3-music-hq", ctx.Presets.SelectedItem!.Id);
  }

  [Fact]
  public void MissingOutputDirectoryFallsBackToDocuments()
  {
    _settings.Settings.OutputDirectory = "";
    var ctx = CreateViewModel();

    var expected = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
      "GoldsrcSoundConverter");
    Assert.Equal(expected, ctx.ViewModel.OutputDirectory);
  }

  [Fact]
  public void OutputDirectoryPropagatesToProvider()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();

    Assert.Equal(_temp.Path, ctx.OutputDirectoryProvider.Value);

    ctx.ViewModel.OutputDirectory = Path.Combine(_temp.Path, "out");

    Assert.Equal(ctx.ViewModel.OutputDirectory, ctx.OutputDirectoryProvider.Value);
  }

  [Fact]
  public void SavePersistsConversionStateAndWindowSize()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();
    ctx.Conversion.SampleRate = 11025;

    var saved = ctx.ViewModel.Save(1500, 950);

    Assert.True(saved);
    Assert.NotNull(_settings.LastSaved);
    Assert.Equal(11025, _settings.LastSaved!.SampleRate);
    Assert.Equal(ctx.Presets.SelectedItem!.Id, _settings.LastSaved.PresetId);
    Assert.Equal(_temp.Path, _settings.LastSaved.OutputDirectory);
    Assert.Equal(1500, _settings.LastSaved.WindowWidth);
    Assert.Equal(950, _settings.LastSaved.WindowHeight);
  }

  [Fact]
  public void SaveFailureIsLoggedAndReported()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    _settings.SaveException = new IOException("disk full");
    var ctx = CreateViewModel();

    var saved = ctx.ViewModel.Save();

    Assert.False(saved);
    Assert.Contains(_log.Entries, entry => entry.Contains("disk full", StringComparison.Ordinal));
  }

  [Fact]
  public void SaveSettingsCommandReportsStatus()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();
    string? status = null;
    ctx.ViewModel.StatusChanged += (_, message) => status = message;

    ctx.ViewModel.SaveSettingsCommand.Execute(null);

    Assert.Equal("Настройки сохранены", status);
  }

  [Fact]
  public void BrowseOutputDirectoryUsesPicker()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();
    _filePicker.FolderToPick = Path.Combine(_temp.Path, "picked");

    ctx.ViewModel.BrowseOutputDirectoryCommand.Execute(null);

    Assert.Equal(Path.Combine(_temp.Path, "picked"), ctx.ViewModel.OutputDirectory);
  }

  [Fact]
  public void OpenOutputFolderUsesLauncher()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();

    ctx.ViewModel.OpenOutputFolderCommand.Execute(null);

    Assert.Contains(_temp.Path, _folderLauncher.OpenedPaths);
  }

  [Fact]
  public void OpenMissingOutputFolderReportsStatus()
  {
    _settings.Settings.OutputDirectory = _temp.Path;
    var ctx = CreateViewModel();
    ctx.ViewModel.OutputDirectory = Path.Combine(_temp.Path, "missing");
    string? status = null;
    ctx.ViewModel.StatusChanged += (_, message) => status = message;

    ctx.ViewModel.OpenOutputFolderCommand.Execute(null);

    Assert.Empty(_folderLauncher.OpenedPaths);
    Assert.Contains("не создана", status);
  }

  private Context CreateViewModel()
  {
    var queue = new QueueManager(_conversionService, _log);
    var queueVm = new QueueViewModel(queue, _filePicker, new WaveformLoader(_conversionService, _log));
    var outputDirectory = new OutputDirectoryProvider();
    var conversion = new ConversionViewModel(
      queueVm,
      _filePicker,
      _conversionService,
      new ConversionRequestFactory(),
      new ConversionRunController(_conversionService, _log),
      new QueueConversionPresenter(queue),
      _log,
      outputDirectory);
    var presets = new PresetViewModel(conversion, new PresetCatalog());
    var vm = new SettingsViewModel(
      _settings,
      _filePicker,
      _folderLauncher,
      conversion,
      presets,
      _log,
      outputDirectory);
    return new Context(vm, conversion, presets, outputDirectory);
  }

  private sealed record Context(
    SettingsViewModel ViewModel,
    ConversionViewModel Conversion,
    PresetViewModel Presets,
    OutputDirectoryProvider OutputDirectoryProvider);
}
