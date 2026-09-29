using GoldsrcSoundConverter.App.Composition;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Settings;
using GoldsrcSoundConverter.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GoldsrcSoundConverter.Tests;

public sealed class CompositionTests
{
  [Fact]
  public void ScopeOwnsViewModelAndControllerLifetime()
  {
    var runController = new FakeRunController();
    using var provider = BuildProvider(runController);
    var scope = provider.CreateScope();

    var main = scope.ServiceProvider.GetRequiredService<MainViewModel>();

    Assert.NotNull(main.Queue);
    Assert.NotNull(main.Conversion);
    Assert.NotNull(main.Playback);
    Assert.NotNull(main.Presets);
    Assert.NotNull(main.Settings);
    Assert.NotNull(main.Log);

    scope.Dispose();

    Assert.True(runController.Disposed);
  }

  [Fact]
  public void ScopedResolutionsShareOneQueue()
  {
    using var provider = BuildProvider(new FakeRunController());
    using var scope = provider.CreateScope();

    var queue = scope.ServiceProvider.GetRequiredService<IQueueManager>();
    var queueVm = scope.ServiceProvider.GetRequiredService<QueueViewModel>();
    var conversion = scope.ServiceProvider.GetRequiredService<ConversionViewModel>();

    Assert.Same(queue.Items, queueVm.Items);
    Assert.Same(queueVm, scope.ServiceProvider.GetRequiredService<QueueViewModel>());
    Assert.NotNull(conversion);
  }

  private static ServiceProvider BuildProvider(FakeRunController runController)
  {
    var services = new ServiceCollection();
    services.AddGoldsrcSoundConverter();
    services.AddSingleton<ISettingsStore>(new FakeSettingsStore());
    services.AddSingleton<IFilePicker>(new FakeFilePicker());
    services.AddSingleton<IFolderLauncher>(new FakeFolderLauncher());
    services.AddSingleton<IConversionService>(new FakeConversionService());
    services.AddSingleton<IPlaybackController>(new FakePlaybackController());
    services.AddScoped<IConversionRunController>(_ => runController);
    return services.BuildServiceProvider();
  }
}
