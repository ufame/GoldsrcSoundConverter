using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Tests.Fakes;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class QueueViewModelTests : IDisposable
{
  private static readonly float[] WaveformMins = { -0.5f };
  private static readonly float[] WaveformMaxs = { 0.5f };

  private readonly TempDirectory _temp = new();
  private readonly FakeFilePicker _filePicker = new();
  private readonly FakeConversionService _conversion = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _temp.Dispose();
  }

  [Fact]
  public void AddFilesAddsSupportedFiles()
  {
    var file = CreateFile("sound.wav");
    _filePicker.FilesToPick = new[] { file };
    var vm = CreateViewModel();

    vm.AddFilesCommand.Execute(null);

    var item = Assert.Single(vm.Items);
    Assert.Equal(file, item.SourcePath);
    Assert.True(vm.HasSelection == false);
  }

  [Fact]
  public void AddPathsIgnoresDuplicatesAndUnsupportedFiles()
  {
    var wave = CreateFile("sound.wav");
    var text = CreateFile("notes.txt");
    var vm = CreateViewModel();

    vm.AddPaths(new[] { wave, wave, text });

    Assert.Single(vm.Items);
    Assert.Equal(wave, vm.Items[0].SourcePath);
  }

  [Fact]
  public void AddPathsRaisesEventsAndStatus()
  {
    var vm = CreateViewModel();
    var itemsChanged = 0;
    var itemsAdded = 0;
    string? status = null;
    vm.ItemsChanged += (_, _) => itemsChanged++;
    vm.ItemsAdded += (_, count) => itemsAdded = count;
    vm.StatusChanged += (_, message) => status = message;

    vm.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal(1, itemsChanged);
    Assert.Equal(1, itemsAdded);
    Assert.Equal("Добавлено файлов: 1", status);

    vm.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal(2, itemsChanged);
    Assert.Equal(1, itemsAdded);
    Assert.Equal("Новые файлы не найдены", status);
  }

  [Fact]
  public void AddPathsWhenLockedReportsStatus()
  {
    var vm = CreateViewModel();
    vm.IsLocked = true;

    var added = vm.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal(0, added);
    Assert.Empty(vm.Items);
    Assert.False(vm.CanEditQueue);

    string? status = null;
    vm.StatusChanged += (_, message) => status = message;
    vm.AddPaths(new[] { CreateFile("a.wav") });

    Assert.Equal("Дождитесь окончания конвертации", status);
  }

  [Fact]
  public void RemoveSelectedKeepsRemainingItemInstance()
  {
    var first = CreateFile("a.wav");
    var second = CreateFile("b.wav");
    var vm = CreateViewModel();
    vm.AddPaths(new[] { first, second });
    var secondItem = vm.Items[1];
    var secondId = secondItem.Id;

    vm.SelectedItem = vm.Items[0];
    vm.RemoveSelectedCommand.Execute(null);

    var remaining = Assert.Single(vm.Items);
    Assert.Same(secondItem, remaining);
    Assert.Equal(second, remaining.SourcePath);
    Assert.Equal(secondId, remaining.Id);
    Assert.Null(vm.SelectedItem);
  }

  [Fact]
  public void ClearEmptiesQueueAndReportsStatus()
  {
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("a.wav"), CreateFile("b.wav") });
    vm.SelectedItem = vm.Items[0];
    string? status = null;
    vm.StatusChanged += (_, message) => status = message;

    vm.ClearCommand.Execute(null);

    Assert.Empty(vm.Items);
    Assert.Null(vm.SelectedItem);
    Assert.Equal("Очередь очищена", status);
  }

  [Fact]
  public async Task SelectingItemLoadsWaveformAndRaisesItemUpdated()
  {
    _conversion.Waveform = new WaveformData(WaveformMins, WaveformMaxs, 64, 8000);
    var vm = CreateViewModel();
    vm.AddPaths(new[] { CreateFile("sound.wav") });
    QueueItemViewModel? updated = null;
    vm.ItemUpdated += (_, item) => updated = item;

    vm.SelectedItem = vm.Items[0];

    await WaitUntil(() => vm.Items[0].Waveform is not null);
    Assert.Same(vm.Items[0], updated);
  }

  [Fact]
  public async Task ItemProbedIsForwardedAsItemUpdated()
  {
    var vm = CreateViewModel();
    QueueItemViewModel? updated = null;
    vm.ItemUpdated += (_, item) => updated = item;
    vm.AddPaths(new[] { CreateFile("sound.wav") });

    await WaitUntil(() => updated is not null);

    Assert.NotNull(updated!.Info);
  }

  private QueueViewModel CreateViewModel()
  {
    var manager = new QueueManager(_conversion, _log);
    return new QueueViewModel(manager, _filePicker, new WaveformLoader(_conversion, _log));
  }

  private string CreateFile(string name)
  {
    var path = Path.Combine(_temp.Path, name);
    File.WriteAllText(path, "data");
    return path;
  }

  private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
  {
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (!condition())
    {
      if (DateTime.UtcNow > deadline)
      {
        throw new TimeoutException("Условие не выполнено за отведённое время.");
      }

      await Task.Delay(10);
    }
  }
}
