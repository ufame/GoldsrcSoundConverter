using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Models;
using GoldsrcSoundConverter.Tests.Fakes;

namespace GoldsrcSoundConverter.Tests;

public sealed class PlaybackCoordinatorTests : IDisposable
{
  private readonly FakePlaybackController _playback = new();
  private readonly LogBuffer _log = new();

  public void Dispose()
  {
    _playback.Dispose();
  }

  private PlaybackCoordinator CreateCoordinator()
  {
    return new PlaybackCoordinator(_playback, new ConversionRequestFactory(), _log);
  }

  private static QueueItemViewModel Item()
  {
    return new QueueItemViewModel(@"C:\in\clip.ogg", null)
    {
      TrimEndSeconds = 10,
    };
  }

  private static ConversionOptions Options()
  {
    return new ConversionOptions { OutputDirectory = @"C:\out" };
  }

  [Fact]
  public async Task PlayPreparesSourceAppliesVolumeAndStarts()
  {
    var coordinator = CreateCoordinator();
    coordinator.Volume = 0.5;
    var item = Item();

    var result = await coordinator.PlayAsync(item, null);

    Assert.True(result.Success);
    Assert.Equal(item.SourcePath, _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
    Assert.Equal(0.5, _playback.Volume, 3);
  }

  [Fact]
  public async Task PrepareFailureReturnsStatusAndLogs()
  {
    _playback.PrepareException = new InvalidOperationException("нет декодера");
    var coordinator = CreateCoordinator();

    var result = await coordinator.PlayAsync(Item(), null);

    Assert.False(result.Success);
    Assert.Contains("нет декодера", result.Status);
    Assert.Contains(_log.Entries, entry => entry.Contains("нет декодера", StringComparison.Ordinal));
    Assert.Equal(0, _playback.PlayCount);
  }

  [Fact]
  public async Task PlaySelectionUsesTrimRange()
  {
    var coordinator = CreateCoordinator();
    var item = Item();
    item.TrimStartSeconds = 2;
    item.TrimEndSeconds = 5;

    var result = await coordinator.PlaySelectionAsync(item, null);

    Assert.True(result.Success);
    var selection = Assert.Single(_playback.Selections);
    Assert.Equal(2, selection.Start, 3);
    Assert.Equal(5, selection.End, 3);
  }

  [Fact]
  public async Task PlaySelectionWithoutTrimFallsBackToFullPlayback()
  {
    var coordinator = CreateCoordinator();
    var item = Item();
    item.TrimEndSeconds = 0;

    var result = await coordinator.PlaySelectionAsync(item, null);

    Assert.True(result.Success);
    Assert.Empty(_playback.Selections);
    Assert.Equal(1, _playback.PlayCount);
  }

  [Fact]
  public async Task PreviewResultRendersAndPlays()
  {
    var coordinator = CreateCoordinator();
    var item = Item();

    var result = await coordinator.PreviewResultAsync(item, Options(), null);

    Assert.True(result.Success);
    Assert.NotNull(_playback.PreparedPath);
    Assert.EndsWith(".result.wav", _playback.PreparedPath);
    Assert.Equal(1, _playback.PlayCount);
    Assert.Equal("Воспроизведение результата", result.Status);
  }

  [Fact]
  public async Task PreviewResultFailureReturnsStatus()
  {
    _playback.PrepareResultException = new InvalidOperationException("boom");
    var coordinator = CreateCoordinator();

    var result = await coordinator.PreviewResultAsync(Item(), Options(), null);

    Assert.False(result.Success);
    Assert.Contains("boom", result.Status);
  }

  [Fact]
  public void TrimCommandsUpdateItemAndReturnStatus()
  {
    var coordinator = CreateCoordinator();
    _playback.PositionSeconds = 3;
    var item = Item();

    var startStatus = coordinator.SetTrimStart(item);
    Assert.Equal(3, item.TrimStartSeconds, 3);
    Assert.StartsWith("Начало обрезки", startStatus);

    _playback.PositionSeconds = 7;
    var endStatus = coordinator.SetTrimEnd(item);
    Assert.Equal(7, item.TrimEndSeconds, 3);
    Assert.StartsWith("Конец обрезки", endStatus);

    var resetStatus = coordinator.ResetTrim(item);
    Assert.Equal(0, item.TrimStartSeconds, 3);
    Assert.Equal(item.DurationSeconds, item.TrimEndSeconds, 3);
    Assert.Equal("Обрезка сброшена", resetStatus);
  }

  [Fact]
  public void PositionAndEventsAreForwarded()
  {
    var coordinator = CreateCoordinator();
    var raised = 0;
    coordinator.PositionChanged += (_, _) => raised++;
    _playback.PositionSeconds = 12;
    _playback.TotalSeconds = 30;

    Assert.Equal(12, coordinator.PositionSeconds, 3);
    Assert.Equal(30, coordinator.TotalSeconds, 3);

    _playback.RaisePositionChanged();
    Assert.Equal(1, raised);
  }
}
