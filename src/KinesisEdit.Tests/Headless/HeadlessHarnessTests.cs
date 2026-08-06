using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Avalonia.Styling;
using KinesisEdit.Tests.Design;

namespace KinesisEdit.Tests.Headless
{
    /// <summary>
    /// The harness asserting on itself. Two claims are load-bearing for every UI suite in this
    /// assembly and neither is obvious from reading <see cref="TestAppBuilder"/>: that what boots
    /// is the <b>real</b> <see cref="App"/>, and that it needs no display — CI has none.
    /// </summary>
    public class HeadlessHarnessTests
    {
        /// <summary>Assemblies that only ever load when Avalonia talks to a real window server.</summary>
        private static readonly string[] _nativeWindowingAssemblies =
        [
            "Avalonia.Native",
            "Avalonia.X11",
            "Avalonia.Win32"
        ];

        [AvaloniaFact]
        public void TheSession_BootsTheApplicationTheAppShips()
        {
            // Not a stand-in that merges the same files: the composition in App.axaml — the view
            // locator, the converters, the theme includes, the style order — is part of what every
            // other suite is testing.
            Assert.IsType<App>(Application.Current);
        }

        [AvaloniaFact]
        public void TheSession_CarriesTheAppsOwnViewLocator()
        {
            Assert.Contains(Application.Current!.DataTemplates, template => template is ViewLocator);
        }

        [AvaloniaFact]
        public void TheSession_CarriesTheDesignSystem()
        {
            // One token from each of the four token files, which is enough to prove the merge in
            // App.axaml happened rather than the dictionaries being loaded by a test helper.
            Assert.True(DesignTokens.TryResolve("SurfaceCanvasBrush", ThemeVariant.Dark, out _));
            Assert.True(DesignTokens.TryResolve("RadiusPanel", ThemeVariant.Dark, out _));
            Assert.True(DesignTokens.TryResolve("FontSans", ThemeVariant.Dark, out _));
            Assert.True(DesignTokens.TryResolve("DurationToast", ThemeVariant.Dark, out _));
        }

        [AvaloniaFact]
        public void TheSession_RunsOnTheHeadlessWindowingPlatform()
        {
            // The acceptance criterion behind this whole harness: `dotnet test` must run with no
            // display attached. Headless windowing is what makes that true — it never opens a
            // connection to a window server — and a native backend appearing in the process is the
            // one way that could quietly stop being true.
            Assert.Contains("Avalonia.Headless", LoadedAssemblies(), StringComparer.Ordinal);

            var native = LoadedAssemblies().Intersect(_nativeWindowingAssemblies, StringComparer.Ordinal).ToArray();

            Assert.True(native.Length == 0, $"A native windowing backend was loaded: {string.Join(", ", native)}.");
        }

        [AvaloniaFact]
        public void TheSession_RasterisesRatherThanStubbingTheDrawing()
        {
            // UseHeadlessDrawing is off and Skia is registered in its place, so a frame is real
            // pixels. Without it CaptureRenderedFrame answers nothing and a style that only breaks
            // in a draw call never breaks at all.
            using var host = ThemedHost.Show(new TextBlock { Text = "Kinesis Edit" }, ThemeVariant.Dark, 200, 80);

            var frame = host.Capture();

            Assert.Equal(200, frame.PixelSize.Width);
            Assert.Equal(80, frame.PixelSize.Height);
        }

        [AvaloniaFact]
        public void TheSession_DrawsWithSkia()
        {
            Assert.Contains("Avalonia.Skia", LoadedAssemblies(), StringComparer.Ordinal);
        }

        [AvaloniaFact]
        public void AWindow_UnderEachVariant_IsPaintedWithTheCanvasToken()
        {
            // End to end, and the only assertion in the suite that reaches the glass: the window
            // style is the app's own (Styles/Surfaces.axaml), the brush is the variant's token, and
            // the number read back is the pixel Skia actually drew.
            foreach (var variant in DesignTokens.Variants)
            {
                using var host = ThemedHost.Show(new TextBlock(), variant, 200, 80);

                Assert.Equal(DesignTokens.Resolve("SurfaceCanvasBrush", variant), host.Window.Background);
                Assert.Equal(
                    DesignTokens.ResolveColor("SurfaceCanvasColor", variant),
                    FramePixels.At(host.Capture(), 4, 4));
            }
        }

        private static IReadOnlyCollection<string> LoadedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetName().Name)
                .Where(name => name is not null)
                .Select(name => name!)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
