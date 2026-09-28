using GoldsrcSoundConverter.App.Services;

namespace GoldsrcSoundConverter.Tests;

public sealed class LogBufferTests
{
  [Fact]
  public void AddsTimestampedEntries()
  {
    var log = new LogBuffer();

    log.Add("готово");

    var entry = Assert.Single(log.Entries);
    Assert.Matches(@"^\[\d{2}:\d{2}:\d{2}\] готово$", entry);
  }

  [Fact]
  public void CapsEntriesAtMax()
  {
    var log = new LogBuffer();

    for (var i = 0; i < LogBuffer.MaxEntries + 50; i++)
    {
      log.Add($"message {i}");
    }

    Assert.Equal(LogBuffer.MaxEntries, log.Entries.Count);
    Assert.Contains($"message {LogBuffer.MaxEntries + 49}", log.Entries[^1]);
    Assert.DoesNotContain(log.Entries, entry => entry.Contains("message 0", StringComparison.Ordinal));
  }
}
