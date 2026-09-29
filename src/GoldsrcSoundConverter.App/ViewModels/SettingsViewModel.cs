using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Core.Settings;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
  private readonly ISettingsStore _settingsStore;
  private readonly IFilePicker _filePicker;
  private readonly IFolderLauncher _folderLauncher;
  private readonly ConversionViewModel _conversion;
  private readonly PresetViewModel _presets;
  private readonly ILogBuffer _log;
  private readonly OutputDirectoryProvider _outputDirectoryProvider;

  public SettingsViewModel(
    ISettingsStore settingsStore,
    IFilePicker filePicker,
    IFolderLauncher folderLauncher,
    ConversionViewModel conversion,
    PresetViewModel presets,
    ILogBuffer log,
    OutputDirectoryProvider outputDirectoryProvider)
  {
    _settingsStore = settingsStore;
    _filePicker = filePicker;
    _folderLauncher = folderLauncher;
    _conversion = conversion;
    _presets = presets;
    _log = log;
    _outputDirectoryProvider = outputDirectoryProvider;

    var settings = _settingsStore.Load();
    InitialWindowWidth = settings.WindowWidth > 400 ? settings.WindowWidth : 1400;
    InitialWindowHeight = settings.WindowHeight > 300 ? settings.WindowHeight : 900;
    Apply(settings);
  }

  public double InitialWindowWidth { get; }

  public double InitialWindowHeight { get; }

  [ObservableProperty]
  private string _outputDirectory = string.Empty;

  public event EventHandler<string>? StatusChanged;

  public void SaveWithWindow(double width, double height)
  {
    Save(width, height);
  }

  public bool Save(double? windowWidth = null, double? windowHeight = null)
  {
    try
    {
      var settings = new AppSettings
      {
        FfmpegCustomPath = _conversion.FfmpegCustomPath,
        OutputDirectory = OutputDirectory,
        Format = _conversion.Format,
        PresetId = _presets.SelectedItem?.Id ?? Cs16Presets.CustomId,
        SampleRate = _conversion.SampleRate,
        Channels = _conversion.Channels,
        BitDepth = _conversion.BitDepth,
        Mp3BitrateKbps = _conversion.Mp3BitrateKbps,
        NormalizePeak = _conversion.NormalizePeak,
        AsciiNames = _conversion.AsciiNames,
        LowercaseNames = _conversion.LowercaseNames,
        PreserveStructure = _conversion.PreserveStructure,
        CollisionPolicy = _conversion.CollisionPolicy,
        Parallelism = _conversion.Parallelism,
      };

      if (windowWidth is > 400)
      {
        settings.WindowWidth = windowWidth.Value;
      }

      if (windowHeight is > 300)
      {
        settings.WindowHeight = windowHeight.Value;
      }

      _settingsStore.Save(settings);
      return true;
    }
    catch (Exception ex)
    {
      _log.Add("Не удалось сохранить настройки: " + ex.Message);
      return false;
    }
  }

  [RelayCommand]
  private void BrowseOutputDirectory()
  {
    var folder = _filePicker.PickFolder("Папка для результатов", OutputDirectory);
    if (folder is not null)
    {
      OutputDirectory = folder;
    }
  }

  [RelayCommand]
  private void OpenOutputFolder()
  {
    try
    {
      if (!Directory.Exists(OutputDirectory))
      {
        StatusChanged?.Invoke(this, "Папка результатов ещё не создана");
        return;
      }

      _folderLauncher.Open(OutputDirectory);
    }
    catch (Exception ex)
    {
      StatusChanged?.Invoke(this, "Не удалось открыть папку: " + ex.Message);
    }
  }

  [RelayCommand]
  private void SaveSettings()
  {
    if (Save())
    {
      StatusChanged?.Invoke(this, "Настройки сохранены");
    }
  }

  private void Apply(AppSettings settings)
  {
    _presets.BeginBatch();
    try
    {
      _conversion.Format = settings.Format;
      _conversion.SampleRate = settings.SampleRate;
      _conversion.Channels = settings.Channels;
      _conversion.BitDepth = settings.BitDepth;
      _conversion.Mp3BitrateKbps = settings.Mp3BitrateKbps;
      _conversion.NormalizePeak = settings.NormalizePeak;
      _conversion.AsciiNames = settings.AsciiNames;
      _conversion.LowercaseNames = settings.LowercaseNames;
      _conversion.PreserveStructure = settings.PreserveStructure;
      _conversion.CollisionPolicy = settings.CollisionPolicy;
      _conversion.Parallelism = settings.Parallelism;
      _conversion.FfmpegCustomPath = settings.FfmpegCustomPath;
      OutputDirectory = settings.OutputDirectory;

      if (string.IsNullOrWhiteSpace(OutputDirectory))
      {
        OutputDirectory = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
          "GoldsrcSoundConverter");
      }
    }
    finally
    {
      _presets.EndBatch();
    }
  }

  partial void OnOutputDirectoryChanged(string value)
  {
    _outputDirectoryProvider.Value = value;
    _conversion.RecalculateTargetSizes();
  }
}
