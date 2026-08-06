using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The control palette and the two style fixes on top of it, checked at the glass rather than
    /// in the resource dictionary: a control is rendered, and the pixel it painted is compared to
    /// the token it was supposed to paint. A resource can resolve correctly and still never reach
    /// the control — a Fluent state setter that targets the template's presenter outranks a
    /// Background set on the control itself — and only a frame can tell the difference.
    /// </summary>
    public class ControlRenderTests
    {
        private const double HostWidth = 200;

        private const double HostHeight = 100;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APlainButton_RestsOnTheBarSurface(string variantName)
        {
            var variant = ToVariant(variantName);

            Assert.Equal(DesignTokens.ResolveBrushColor("SurfaceBarBrush", variant), PaintOfCentredButton(new Button(), variant));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APrimaryAction_IsAnAccentFill(string variantName)
        {
            // The defect this replaces: the accent as a *foreground* on a neutral face, which is
            // roughly 2.3:1 in the light theme.
            var variant = ToVariant(variantName);
            var button = new Button { Classes = { "primaryAction" } };

            Assert.Equal(DesignTokens.ResolveBrushColor("AccentBrush", variant), PaintOfCentredButton(button, variant));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledPrimaryAction_LeavesTheAccentEntirely(string variantName)
        {
            // Save is disabled whenever there is nothing to save, and a disabled Save must not read
            // as a Save that would work.
            var variant = ToVariant(variantName);
            var button = new Button { Classes = { "primaryAction" }, IsEnabled = false };

            Assert.Equal(DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant), PaintOfCentredButton(button, variant));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APrimaryAction_CarriesTheOnAccentColour(string variantName)
        {
            var variant = ToVariant(variantName);
            var button = new Button { Classes = { "primaryAction" }, Content = "Save" };

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            var presenter = button.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(candidate => candidate.Name == "PART_ContentPresenter");

            Assert.Equal(
                DesignTokens.Resolve("AccentTextBrush", variant),
                presenter.Foreground);
        }

        /// <summary>Every view that lays rows out with <c>Border.listRow</c>.</summary>
        public static TheoryData<string> RowedViews()
        {
            return new TheoryData<string>
            {
                typeof(KeyboardSettingsView).FullName!,
                typeof(SavantElitePedalView).FullName!
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(RowedViews))]
        public async Task ListRowSeparators_FallBetweenRowsOnly(string viewTypeName)
        {
            // A top border on every row draws a hairline flush inside the card's own top edge, with
            // nothing above it to separate. The first row therefore carries none.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(viewTypeName);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var rows = view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("listRow"))
                .ToArray();

            Assert.True(rows.Length >= 2, $"The settings scene rendered {rows.Length} rows; the rule needs at least two.");
            Assert.Equal(0, rows[0].BorderThickness.Top);

            foreach (var row in rows.Skip(1))
            {
                Assert.Equal(1, row.BorderThickness.Top);
            }

            // A separator is a top border and only a top border; a bottom one would leave a
            // trailing hairline under the last row.
            Assert.All(rows, row => Assert.Equal(0, row.BorderThickness.Bottom));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheDemoModeChip_IsPurpleAndNotAmber(string variantName)
        {
            // The design fixes four statuses, each meaning one thing: demo mode means "nothing is
            // written" and amber means "advisory". Border.statusChip.demo existed and was
            // unreachable, because nothing ever set the class — a defect no rendering test could
            // see, since an unreachable class simply leaves the chip on whichever class *was* set.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            // No device detected anywhere, which is what puts the shell into demo mode.
            var window = new MainWindow(scenes.CreateShell(withDevices: false), new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, variant);

            var chip = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("statusChip"));

            Assert.Contains("demo", chip.Classes);
            Assert.DoesNotContain("warning", chip.Classes);
            Assert.DoesNotContain("ok", chip.Classes);
            Assert.DoesNotContain("error", chip.Classes);

            Assert.Equal(DesignTokens.Resolve("StatusDemoTintBrush", variant), chip.Background);
            Assert.Equal(DesignTokens.Resolve("StatusDemoTintBorderBrush", variant), chip.BorderBrush);

            var caption = chip.GetVisualDescendants().OfType<TextBlock>().Single();

            Assert.Equal(MainWindowViewModel.DemoModeIndicator, caption.Text);
            Assert.Equal(DesignTokens.Resolve("StatusDemoBrush", variant), caption.Foreground);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheDemoModeChip_PaintsItsTintOntoTheToolbar(string variantName)
        {
            // ...and the same thing at the glass. The chip's fill is translucent, so the pixel is
            // the tint composited over the toolbar; both operands are tokens, so the expectation is
            // computed rather than hardcoded.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var window = new MainWindow(scenes.CreateShell(withDevices: false), new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, variant);

            var frame = host.Capture();
            var chip = window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("statusChip"));

            // Inside the chip's 12px left padding: on its fill, clear of the caption's glyphs.
            var probe = chip.TranslatePoint(new Point(4, chip.Bounds.Height / 2), window)
                ?? throw new InvalidOperationException("The chip is not in the window's visual tree.");

            var painted = FramePixels.At(frame, (int)probe.X, (int)probe.Y);
            var expected = Composite(
                DesignTokens.ResolveBrushColor("StatusDemoTintBrush", variant),
                DesignTokens.ResolveBrushColor("SurfaceBarBrush", variant));

            AssertClose(expected, painted);

            // And is nowhere near what the amber ramp would have drawn.
            var advisory = Composite(
                DesignTokens.ResolveBrushColor("StatusAdvisoryTintBrush", variant),
                DesignTokens.ResolveBrushColor("SurfaceBarBrush", variant));

            Assert.True(Distance(advisory, painted) > 8, $"The chip painted {painted}, which is the advisory tint.");
        }

        /// <summary>Every view that renders a DEMO MODE pill of its own.</summary>
        public static TheoryData<string, string> DemoPillViewsAndVariants()
        {
            var cases = new TheoryData<string, string>();

            foreach (var viewType in new[]
            {
                typeof(KeyboardEditorView).FullName!,
                typeof(EditorPlaceholderView).FullName!,
                typeof(SavantElitePedalView).FullName!
            })
            {
                cases.Add(viewType, "Dark");
                cases.Add(viewType, "Light");
            }

            return cases;
        }

        [AvaloniaTheory]
        [MemberData(nameof(DemoPillViewsAndVariants))]
        public async Task EveryDemoModePill_TakesTheDemoRole(string viewTypeName, string variantName)
        {
            // The shell's chip was only the first of four. Each editor authors its own DEMO MODE
            // pill, and all three sat on the advisory amber — the same conflation, in three more
            // places, invisible to any test that only looked at the toolbar.
            var variant = ToVariant(variantName);
            var viewType = typeof(App).Assembly.GetType(viewTypeName, throwOnError: true)!;

            using var scenes = new ViewSceneFactory();

            var view = (Control)Activator.CreateInstance(viewType)!;

            view.DataContext = await scenes.CreateDemoModeContextAsync(viewType).ConfigureAwait(true);

            using var host = ThemedHost.Show(view, variant);

            var pill = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.Text == MainWindowViewModel.DemoModeIndicator);

            // Visible, because the scene really is in demo mode — a pill asserted on while hidden
            // would prove only that the markup compiled.
            Assert.True(pill.IsVisible, $"{viewType.Name} did not show its demo pill.");
            Assert.Contains("statusDemo", pill.Classes);
            Assert.DoesNotContain("statusWarning", pill.Classes);
            Assert.Equal(DesignTokens.Resolve("StatusDemoBrush", variant), pill.Foreground);
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>Source-over composite of a translucent <paramref name="source"/> onto an opaque background.</summary>
        private static Avalonia.Media.Color Composite(Avalonia.Media.Color source, Avalonia.Media.Color background)
        {
            var alpha = source.A / 255d;

            return Avalonia.Media.Color.FromRgb(
                (byte)Math.Round((source.R * alpha) + (background.R * (1 - alpha))),
                (byte)Math.Round((source.G * alpha) + (background.G * (1 - alpha))),
                (byte)Math.Round((source.B * alpha) + (background.B * (1 - alpha))));
        }

        /// <summary>Channel-sum distance, so a rounding difference is not a failure.</summary>
        private static int Distance(Avalonia.Media.Color left, Avalonia.Media.Color right)
        {
            return Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);
        }

        private static void AssertClose(Avalonia.Media.Color expected, Avalonia.Media.Color actual)
        {
            Assert.True(Distance(expected, actual) <= 3, $"Expected about {expected}, painted {actual}.");
        }

        /// <summary>
        /// Renders <paramref name="button"/> alone, centred and content-free, and reads the colour
        /// at the middle of the frame — which is the middle of the button's face.
        /// </summary>
        private static Avalonia.Media.Color PaintOfCentredButton(Button button, ThemeVariant variant)
        {
            button.Width = 120;
            button.Height = 40;
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            var frame = host.Capture();

            return FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2);
        }
    }
}
