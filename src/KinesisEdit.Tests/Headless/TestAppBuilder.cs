using Avalonia;
using Avalonia.Headless;
using KinesisEdit;
using KinesisEdit.Tests.Headless;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace KinesisEdit.Tests.Headless
{
    /// <summary>
    /// Boots the app for the UI tests. The application it starts is the real
    /// <see cref="App"/> — its <c>App.axaml</c>, and therefore its theme dictionaries, its style
    /// includes, its embedded IBM Plex families and its <see cref="ViewLocator"/> — so a test
    /// exercises the composition the app actually ships rather than a stub that happens to merge
    /// the same files.
    /// <para>
    /// Only the platform underneath differs: <see cref="AvaloniaHeadlessPlatform"/> replaces the
    /// windowing system, so nothing needs a display. Drawing is deliberately <b>not</b> stubbed —
    /// <see cref="AvaloniaHeadlessPlatformOptions.UseHeadlessDrawing"/> is false and Skia is
    /// registered in its place — because the frame captures of the design-system suite need real
    /// rasterised pixels, and because a style that only breaks while drawing (a brush that is not
    /// a brush, a geometry that will not parse) is invisible to a no-op renderer.
    /// </para>
    /// <para>
    /// <see cref="App.OnFrameworkInitializationCompleted"/> builds no window here: it only wires
    /// the shell under <c>IClassicDesktopStyleApplicationLifetime</c>, and the headless session
    /// runs without one. Tests build the windows they need themselves.
    /// </para>
    /// </summary>
    public static class TestAppBuilder
    {
        /// <summary>The builder <c>[AvaloniaTest]</c> starts the session with.</summary>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                });
        }
    }
}
