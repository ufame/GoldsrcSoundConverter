using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed partial class PresetViewModel : ObservableObject
{
  private readonly ConversionViewModel _conversion;
  private readonly IPresetCatalog _presetCatalog;

  private int _suspendCount;

  public PresetViewModel(ConversionViewModel conversion, IPresetCatalog presetCatalog)
  {
    _conversion = conversion;
    _presetCatalog = presetCatalog;

    _conversion.FormatChanged += OnFormatChanged;
    _conversion.OptionsChanged += OnOptionsChanged;

    Refresh();
  }

  public ObservableCollection<Cs16Preset> Items { get; } = new();

  [ObservableProperty]
  private Cs16Preset? _selectedItem;

  public void BeginBatch()
  {
    _suspendCount++;
  }

  public void EndBatch()
  {
    if (--_suspendCount <= 0)
    {
      _suspendCount = 0;
      SelectMatching();
    }
  }

  public void Refresh()
  {
    _suspendCount++;
    try
    {
      Items.Clear();
      foreach (var preset in _presetCatalog.ForFormat(_conversion.Format))
      {
        Items.Add(preset);
      }

      SelectedItem = _presetCatalog.Resolve(_conversion.BuildOptions());
    }
    finally
    {
      _suspendCount--;
    }
  }

  private void SelectMatching()
  {
    _suspendCount++;
    try
    {
      SelectedItem = _presetCatalog.Resolve(_conversion.BuildOptions());
    }
    finally
    {
      _suspendCount--;
    }
  }

  private void OnFormatChanged(object? sender, EventArgs e)
  {
    Refresh();
  }

  private void OnOptionsChanged(object? sender, EventArgs e)
  {
    if (_suspendCount == 0)
    {
      SelectMatching();
    }
  }

  partial void OnSelectedItemChanged(Cs16Preset? value)
  {
    if (_suspendCount > 0 || value is null)
    {
      return;
    }

    _suspendCount++;
    try
    {
      _conversion.SampleRate = value.SampleRate;
      _conversion.Channels = value.Channels;
      _conversion.BitDepth = value.BitDepth;
      _conversion.Mp3BitrateKbps = value.Mp3BitrateKbps;
    }
    finally
    {
      _suspendCount--;
    }
  }
}
