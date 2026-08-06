using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Every view the app declares, constructed over a realistic view model, shown under
    /// <b>both</b> theme variants and rendered to a real frame.
    /// <para>
    /// This is the coarse net under the repaint. Loading the XAML is where a broken
    /// <c>StaticResource</c>, an unparseable brush or geometry, a <c>DataTemplate</c> naming a type
    /// that moved, or a selector referring to a template part that no longer exists throws;
    /// applying the styles and drawing the frame is where a setter whose value cannot be coerced to
    /// the property's type does. Running it twice, once per variant, is what catches a token that
    /// only one of the two dictionaries carries.
    /// </para>
    /// </summary>
    public class ViewRenderSmokeTests
    {
        /// <summary>Every view, paired with each theme variant.</summary>
        public static TheoryData<string, string> ViewsAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var viewType in ViewSceneFactory.ViewTypes())
            {
                cases.Add(viewType.FullName!, ThemeVariant.Dark.Key.ToString()!);
                cases.Add(viewType.FullName!, ThemeVariant.Light.Key.ToString()!);
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(ViewsAndVariants))]
        public async Task View_UnderEachVariant_LoadsAppliesItsStylesAndDraws(string viewTypeName, string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(viewTypeName);
            var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            using var host = ThemedHost.Show(view, variant);

            // Rasterising is the point: with the headless no-op renderer a broken brush never
            // reaches a draw call, and this whole suite would pass on markup that cannot be drawn.
            var frame = host.Capture();

            Assert.True(frame.PixelSize.Width > 0 && frame.PixelSize.Height > 0);
        }

        [AvaloniaFact]
        public void TheViewCatalog_CoversEveryScreenTheAppCanShow()
        {
            // A tripwire on the reflection above: a view added to a new folder, or the assembly
            // filter drifting, must not quietly shrink the suite.
            var names = ViewSceneFactory.ViewTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);

            Assert.Contains("MainWindow", names);
            Assert.Contains("DashboardView", names);
            Assert.Contains("DeviceCardView", names);
            Assert.Contains("WebToolCardView", names);
            Assert.Contains("NoDeviceView", names);
            Assert.Contains("KeyboardEditorView", names);
            Assert.Contains("LightingTabView", names);
            Assert.Contains("KeyboardSettingsView", names);
            Assert.Contains("SavantElitePedalView", names);
            Assert.Contains("KeyCapView", names);
            Assert.True(names.Count >= 19, $"Only {names.Count} views were discovered.");
        }

        [AvaloniaFact]
        public async Task EveryView_GetsADataContext_OrDeclaresItNeedsNone()
        {
            // The smoke test is only as good as its scenes: a view whose DataContext is null draws
            // an empty shell and would pass no matter how broken its styles are. Only the two views
            // that genuinely bind to no view model of their own may be null — the notification
            // overlay binds to itself, and the keystroke spike window is self-contained.
            var selfContained = new[] { "NotificationOverlay", "KeystrokeCaptureSpikeWindow" };
            var missing = new List<string>();

            foreach (var viewType in ViewSceneFactory.ViewTypes())
            {
                using var scenes = new ViewSceneFactory();

                var view = await scenes.CreateAsync(viewType.FullName!);

                using var host = ThemedHost.Show(view, ThemeVariant.Dark);

                if (view.DataContext is null && !selfContained.Contains(viewType.Name))
                {
                    missing.Add(viewType.FullName!);
                }
            }

            Assert.True(missing.Count == 0, $"These views render with no view model: {string.Join(", ", missing)}.");
        }

        [AvaloniaFact]
        public async Task TheEditorsTabs_EachRenderUnderBothVariants()
        {
            // The editor is one view whose three sections are toggled by IsVisible rather than by
            // separate views, so the smoke pass above only ever draws the Keys tab. Each of the
            // others is drawn here.
            foreach (var variant in DesignTokens.Variants)
            {
                using var scenes = new ViewSceneFactory();

                var editor = await scenes.CreateEditorAsync();
                var view = await scenes.CreateAsync(typeof(KinesisEdit.Views.KeyboardEditorView).FullName!);

                using var host = ThemedHost.Show(view, variant);

                // No filter: every tab the strip carries opens a working section, because a feature
                // the board lacks is not rendered at all (EditorTabViewModel).
                foreach (var tab in editor.Tabs)
                {
                    editor.SelectTabCommand.Execute(tab);

                    var frame = host.Capture();

                    Assert.True(frame.PixelSize.Width > 0, $"The {tab.Caption} tab drew nothing in {variant}.");
                }
            }
        }
    }
}
