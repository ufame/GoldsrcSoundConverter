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
    services.AddSingleton<IConversionRequestFactory, ConversionRequestFactory>();
    services.AddSingleton<IPresetCatalog, PresetCatalog>();
    services.AddSingleton<IPlaybackCoordinator, PlaybackCoordinator>();
    services.AddSingleton<IWaveformLoader, WaveformLoader>();

    // Per-window state: the view model, its queue and the run controller must share one instance.
    services.AddScoped<IQueueManager, QueueManager>();
    services.AddScoped<IConversionRunController, ConversionRunController>();
    services.AddScoped<IQueueConversionPresenter, QueueConversionPresenter>();
    services.AddScoped<QueueViewModel>();
    services.AddScoped<OutputDirectoryProvider>();
    services.AddScoped<ConversionViewModel>(sp => new ConversionViewModel(
      sp.GetRequiredService<QueueViewModel>(),
      sp.GetRequiredService<IFilePicker>(),
      sp.GetRequiredService<IConversionService>(),
      sp.GetRequiredService<IConversionRequestFactory>(),
      sp.GetRequiredService<IConversionRunController>(),
      sp.GetRequiredService<IQueueConversionPresenter>(),
      sp.GetRequiredService<ILogBuffer>(),
      sp.GetRequiredService<OutputDirectoryProvider>()));
    services.AddScoped<PlaybackViewModel>();
    services.AddScoped<PresetViewModel>();
    services.AddScoped<SettingsViewModel>();
    services.AddScoped<MainViewModel>();
    services.AddScoped<MainWindow>();

    return services;
  }
}
