using System.Windows;
using GoldsrcSoundConverter.App.Composition;
using Microsoft.Extensions.DependencyInjection;

namespace GoldsrcSoundConverter.App;

public partial class App : Application
{
  private ServiceProvider? _services;

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    var services = new ServiceCollection();
    services.AddGoldsrcSoundConverter();

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
