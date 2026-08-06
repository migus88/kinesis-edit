using Avalonia;

namespace KinesisEdit
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            // No .WithInterFont(): the app ships IBM Plex Sans and IBM Plex Mono itself as
            // embedded Avalonia resources (Assets/Fonts, wired in Themes/Typography.axaml), so
            // there is no font package to register here.
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
