using System.Windows;
using OpenAHU.Desktop.Shell;

namespace OpenAHU.Desktop;

public partial class App : Application
{
    private BackendProcessHost? _backend;
    private ShellViewModel? _shell;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _backend = new BackendProcessHost();
        _shell = new ShellViewModel(_backend);
        var window = new MainWindow { DataContext = _shell };
        MainWindow = window;
        window.Show();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            if (_shell is not null)
            {
                await _shell.InitializeAsync();
            }
        }
        catch (Exception ex)
        {
            _shell?.SetStartupFailure(ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _shell?.ShutdownAsync().GetAwaiter().GetResult();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}
