using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class LogViewModelTests
{
  private readonly LogBuffer _log = new();

  [Fact]
  public void AppendAddsEntryAndRaisesCollectionChanged()
  {
    var vm = new LogViewModel(_log);
    var changes = 0;
    vm.Entries.CollectionChanged += (_, _) => changes++;

    vm.Append("hello");

    Assert.Equal(1, changes);
    Assert.Contains(vm.Entries, entry => entry.Contains("hello", StringComparison.Ordinal));
  }

  [Fact]
  public void EntriesAreSharedWithBuffer()
  {
    var vm = new LogViewModel(_log);

    _log.Add("from service");

    Assert.Contains(vm.Entries, entry => entry.Contains("from service", StringComparison.Ordinal));
  }
}
