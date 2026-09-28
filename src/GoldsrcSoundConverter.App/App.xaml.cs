using System.Windows;
using GoldsrcSoundConverter.App.Infrastructure.FilePicker;
using GoldsrcSoundConverter.App.ViewModels;
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
