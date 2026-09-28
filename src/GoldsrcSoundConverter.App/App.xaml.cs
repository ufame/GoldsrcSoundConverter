using System.Windows;
using GoldsrcSoundConverter.App.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GoldsrcSoundConverter.App;

public partial class App : Application
{
  private IHost? _host;
  private IServiceScope? _windowScope;

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddGoldsrcSoundConverter();

    _host = builder.Build();
    _host.Start();

    _windowScope = _host.Services.CreateScope();
    var window = _windowScope.ServiceProvider.GetRequiredService<MainWindow>();
    MainWindow = window;
    window.Show();
  }

  protected override void OnExit(ExitEventArgs e)
  {
    _windowScope?.Dispose();

    if (_host is not null)
    {
      _host.StopAsync().GetAwaiter().GetResult();
      _host.Dispose();
    }

    base.OnExit(e);
  }
}
