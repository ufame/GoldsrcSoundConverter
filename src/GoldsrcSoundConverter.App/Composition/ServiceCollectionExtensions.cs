using GoldsrcSoundConverter.App.Infrastructure.Audio;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.Infrastructure.Shell;
using GoldsrcSoundConverter.App.Services;
using GoldsrcSoundConverter.App.ViewModels;
using GoldsrcSoundConverter.Core.Audio;
using GoldsrcSoundConverter.Core.Ffmpeg;
using GoldsrcSoundConverter.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GoldsrcSoundConverter.App.Composition;

public static class ServiceCollectionExtensions
{
  public static IServiceCollection AddGoldsrcSoundConverter(this IServiceCollection services)
  {
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

    return services;
  }
}
