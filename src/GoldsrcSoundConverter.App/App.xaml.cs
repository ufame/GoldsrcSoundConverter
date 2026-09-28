using System.Windows;
using GoldsrcSoundConverter.App.Composition;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GoldsrcSoundConverter.App;

public partial class App : Application
{
  private IHost? _host;

  protected override void OnStartup(StartupEventArgs e)
  {
    base.OnStartup(e);

    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddGoldsrcSoundConverter();

    _host = builder.Build();
    _host.Start();

    var window = _host.Services.GetRequiredService<MainWindow>();
    MainWindow = window;
    window.Show();
  }

  protected override void OnExit(ExitEventArgs e)
  {
    if (_host is not null)
    {
      _host.StopAsync().GetAwaiter().GetResult();
      _host.Dispose();
    }

    base.OnExit(e);
  }
}
