using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.Core.Models;

namespace GoldsrcSoundConverter.App.ViewModels;

public sealed partial class QueueViewModel : ObservableObject, IDisposable
{
  private readonly IQueueManager _queue;
  private readonly IFilePicker _filePicker;
  private readonly IWaveformLoader _waveforms;

  public QueueViewModel(IQueueManager queue, IFilePicker filePicker, IWaveformLoader waveforms)
  {
    _queue = queue;
    _filePicker = filePicker;
    _waveforms = waveforms;
    _queue.ItemProbed += (_, item) => ItemUpdated?.Invoke(this, item);
  }

  public ObservableCollection<QueueItemViewModel> Items => _queue.Items;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(HasSelection))]
  private QueueItemViewModel? _selectedItem;

  [ObservableProperty]
  [NotifyPropertyChangedFor(nameof(CanEditQueue))]
  private bool _isLocked;

  public bool HasSelection => SelectedItem is not null;

  public bool CanEditQueue => !IsLocked;

  public event EventHandler? ItemsChanged;

  public event EventHandler<int>? ItemsAdded;

  public event EventHandler<QueueItemViewModel>? ItemUpdated;

  public event EventHandler<string>? StatusChanged;

  public int AddPaths(IEnumerable<string> paths)
  {
    if (IsLocked)
    {
      StatusChanged?.Invoke(this, "Дождитесь окончания конвертации");
      return 0;
    }

    var added = _queue.Add(paths);
    ItemsChanged?.Invoke(this, EventArgs.Empty);

    if (added > 0)
    {
      ItemsAdded?.Invoke(this, added);
    }

    StatusChanged?.Invoke(this, added > 0
      ? $"Добавлено файлов: {added}"
      : "Новые файлы не найдены");
    return added;
  }

  public void RecalculateTargetSizes(ConversionOptions options)
  {
    _queue.RecalculateTargetSizes(options);
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void AddFiles()
  {
    var files = _filePicker.PickFiles();
    if (files.Count > 0)
    {
      AddPaths(files);
    }
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void AddFolder()
  {
    var folder = _filePicker.PickFolder("Выберите папку со звуками");
    if (folder is not null)
    {
      AddPaths(new[] { folder });
    }
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void RemoveSelected()
  {
    if (SelectedItem is null)
    {
      return;
    }

    _queue.Remove(SelectedItem);
    SelectedItem = null;
    ItemsChanged?.Invoke(this, EventArgs.Empty);
  }

  [RelayCommand(CanExecute = nameof(CanEditQueue))]
  private void Clear()
  {
    _queue.Clear();
    SelectedItem = null;
    ItemsChanged?.Invoke(this, EventArgs.Empty);
    StatusChanged?.Invoke(this, "Очередь очищена");
  }

  public void Dispose()
  {
    _queue.Dispose();
    GC.SuppressFinalize(this);
  }

  partial void OnSelectedItemChanged(QueueItemViewModel? value)
  {
    if (value is not null)
    {
      _ = LoadWaveformAsync(value);
    }
  }

  private async Task LoadWaveformAsync(QueueItemViewModel item)
  {
    await _waveforms.EnsureLoadedAsync(item).ConfigureAwait(true);
    if (item.Waveform is not null)
    {
      ItemUpdated?.Invoke(this, item);
    }
  }
}
