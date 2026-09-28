using System.Windows;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GoldsrcSoundConverter.App;

public partial class App : Application
{
  private ServiceProvider? _services;

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    var services = new ServiceCollection();
    services.AddSingleton<ISettingsStore>(new SettingsStore());
    services.AddSingleton<IFilePicker, WpfFilePicker>();
    services.AddSingleton<IFolderLauncher, WindowsFolderLauncher>();
    services.AddSingleton<IProcessRunner, ProcessRunner>();
    services.AddSingleton<FfmpegBootstrapper>();
    services.AddSingleton<IConversionService, ConversionService>();
    services.AddSingleton<IAudioPreview, AudioPreviewService>();
    services.AddSingleton<IPlaybackController, PlaybackController>();
    services.AddSingleton<ILogBuffer, LogBuffer>();
    services.AddTransient<IQueueManager, QueueManager>();
    services.AddSingleton<IConversionRequestFactory, ConversionRequestFactory>();
    services.AddSingleton<IPresetCatalog, PresetCatalog>();
    services.AddTransient<IConversionRunController, ConversionRunController>();
    services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();
    services.AddSingleton<IWaveformLoader, WaveformLoader>();
    services.AddTransient<MainViewModel>();
    services.AddTransient<MainWindow>();

    _services = services.BuildServiceProvider();

    var window = _services.GetRequiredService<MainWindow>();
    MainWindow = window;
    window.Show();
  }

  protected override void OnExit(ExitEventArgs e)
  {
    _services?.Dispose();
    base.OnExit(e);
  }
}
