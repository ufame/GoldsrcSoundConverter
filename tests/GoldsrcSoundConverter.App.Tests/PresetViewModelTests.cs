using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class PresetViewModelTests
{
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  [Fact]
  public void InitialSelectionMatchesConversionOptions()
  {
    var (vm, _) = CreateViewModel();

    Assert.NotNull(vm.SelectedItem);
    Assert.Equal("wav-sound", vm.SelectedItem!.Id);
  }

  [Fact]
  public void FormatChangeRebuildsListAndKeepsCustom()
  {
    var (vm, conversion) = CreateViewModel();

    conversion.Format = OutputAudioFormat.Mp3;

    Assert.All(vm.Items.Where(preset => !preset.IsCustom), preset => Assert.Equal(OutputAudioFormat.Mp3, preset.Format));
    Assert.Contains(vm.Items, preset => preset.IsCustom);
    Assert.NotNull(vm.SelectedItem);
  }

  [Fact]
  public void ApplyingPresetUpdatesConversionOptions()
  {
    var (vm, conversion) = CreateViewModel();
    var target = vm.Items.First(preset => preset.Id == "wav-stereo");

    vm.SelectedItem = target;

    Assert.Equal(target.SampleRate, conversion.SampleRate);
    Assert.Equal(target.Channels, conversion.Channels);
    Assert.Equal(target.BitDepth, conversion.BitDepth);
    Assert.Equal(target.Mp3BitrateKbps, conversion.Mp3BitrateKbps);
    Assert.Equal("wav-stereo", vm.SelectedItem!.Id);
  }

  [Fact]
  public void EditingOptionsSelectsCustomPreset()
  {
    var (vm, conversion) = CreateViewModel();

    conversion.SampleRate = 44100;

    Assert.Equal(Cs16Presets.CustomId, vm.SelectedItem!.Id);
  }

  [Fact]
  public void BatchUpdateDefersSelectionUntilEnd()
  {
    var (vm, conversion) = CreateViewModel();

    vm.BeginBatch();
    conversion.SampleRate = 44100;
    Assert.Equal("wav-sound", vm.SelectedItem!.Id);

    vm.EndBatch();

    Assert.Equal(Cs16Presets.CustomId, vm.SelectedItem!.Id);
  }

  [Fact]
  public void BatchUpdateResolvesMatchingPreset()
  {
    var (vm, conversion) = CreateViewModel();
    var target = vm.Items.First(preset => preset.Id == "wav-stereo");

    vm.BeginBatch();
    conversion.SampleRate = target.SampleRate;
    conversion.Channels = target.Channels;
    conversion.BitDepth = target.BitDepth;
    conversion.Mp3BitrateKbps = target.Mp3BitrateKbps;
    vm.EndBatch();

    Assert.Equal("wav-stereo", vm.SelectedItem!.Id);
  }

  private (PresetViewModel ViewModel, ConversionViewModel Conversion) CreateViewModel()
  {
    var queue = new QueueManager(_conversion, _log);
    var queueVm = new QueueViewModel(queue, _filePicker, new WaveformLoader(_conversion, _log));
    var outputDirectory = new OutputDirectoryProvider { Value = @"C:\out" };
    var conversion = new ConversionViewModel(
      queueVm,
      _filePicker,
      _conversion,
      new ConversionRequestFactory(),
      new ConversionRunController(_conversion, _log),
      new QueueConversionPresenter(queue),
      _log,
      outputDirectory);
    return (new PresetViewModel(conversion, new PresetCatalog()), conversion);
  }
}
