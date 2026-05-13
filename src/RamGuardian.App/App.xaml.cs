namespace RamGuardian.App;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        if (MainWindow is IDisposable disposableWindow)
        {
            disposableWindow.Dispose();
        }

        base.OnExit(e);
    }
}
