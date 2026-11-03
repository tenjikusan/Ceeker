using Microsoft.UI.Xaml;

namespace Wormy.App;

public partial class App : Application
{
    private Window? window;
    public App() { Environment.SetEnvironmentVariable("ORT_DISABLE_TELEMETRY","1"); InitializeComponent(); }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        var previewPath = arguments.Length == 3 && arguments[1] == "--preview" ? arguments[2] : null;
        var libraryPath = arguments.Length == 3 && arguments[1] is "--library" or "--test-library" ? arguments[2] : null;
        window = new MainWindow(previewPath, libraryPath,arguments.Length==3 && arguments[1]=="--test-library");
        window.Activate();
        ((MainWindow)window).QueueExternalCommand(arguments);
    }
}
