using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Core.Devices;
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
            // The same intent as before the control-theme layer landed, asserted against the
            // template that layer writes: the button's own label sits on the on-accent colour. The
            // part it reaches for is now `Label` in our own ControlTheme rather than Fluent's
            // `PART_ContentPresenter`, and the value arrives by inheritance from the Button instead
            // of from a state setter aimed at the presenter — which is the whole point of the
            // change (see Themes/ControlThemes/Shared.axaml).
            var variant = ToVariant(variantName);
            var button = new Button { Classes = { "primaryAction" }, Content = "Save" };

            using var host = ThemedHost.Show(button, variant, HostWidth, HostHeight);

            var presenter = button.GetVisualDescendants()
                .OfType<ContentPresenter>()
                .Single(candidate => candidate.Name == "Label");

            Assert.Equal(DesignTokens.Resolve("AccentTextBrush", variant), button.Foreground);
            Assert.Equal(DesignTokens.Resolve("AccentTextBrush", variant), presenter.Foreground);
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
            //
            // THE RULE IS PER LIST, NOT PER VIEW. The Settings tab now lays out two of them — the
            // board's settings rows and the app-preference rows below them — and each card's own
            // first row is the one with no separator. So the rows are grouped by their items panel,
            // which is exactly what the `ContentPresenter:nth-child(1)` selector in
            // Styles/Surfaces.axaml counts.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(viewTypeName);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var lists = view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("listRow"))
                .GroupBy(border => border.GetVisualParent()?.GetVisualParent())
                .Select(group => group.ToArray())
                .ToArray();

            Assert.True(lists.Length > 0, "The scene rendered no rows at all.");
            Assert.True(
                lists.Any(rows => rows.Length >= 2),
                "No list in the scene rendered two rows; the separator rule needs at least two.");

            foreach (var rows in lists)
            {
                Assert.Equal(0, rows[0].BorderThickness.Top);

                foreach (var row in rows.Skip(1))
                {
                    Assert.Equal(1, row.BorderThickness.Top);
                }

                // A separator is a top border and only a top border; a bottom one would leave a
                // trailing hairline under the last row.
                Assert.All(rows, row => Assert.Equal(0, row.BorderThickness.Bottom));
            }
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

            // An OPEN DEMO SESSION is what puts the shell into demo mode — "nothing is written".
            // Nothing detected is not the same fact: after a completed scan the dashboard's chip
            // reads v-Drive Error, which mockup 1a defines as "gone · unwritable" and mockup 1d
            // asks for by name.
            var window = new MainWindow(CreateDemoShell(scenes), new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, variant);

            var chip = AppBarStatusChipOf(window);

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

            var window = new MainWindow(CreateDemoShell(scenes), new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, variant);

            var frame = host.Capture();
            var chip = AppBarStatusChipOf(window);

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

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheHomeNavPill_OnTheDashboard_WearsTheActiveNavFace(string variantName)
        {
            // The app bar's three pills, on the screen the app opens on. `NavPill` writes every
            // selected setter as `.selected:not(:disabled)` — a deliberate qualifier, because
            // Avalonia walks BasedOn first and BaseButton's disabled face would otherwise be
            // overridden by a dead pill's live one. That makes "selected" and "disabled" mutually
            // exclusive at the glass, so a Home whose command could only run *while an editor was
            // open* was disabled in exactly the state IsHomeSelected is true: the dashboard drew
            // three identical grey pills and none of them read as where you are.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var window = new MainWindow(scenes.CreateShell(), new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, variant);

            var pills = NavPillsOf(window);
            var home = pills.Single(pill => Equals(pill.Content, "Home"));

            // IsEffectivelyEnabled, not IsEnabled: a Button takes its enablement from its Command
            // through IsEnabledCore, which leaves the styled property untouched. The pseudo-class
            // follows the effective one, and the pseudo-class is what the theme selects on.
            Assert.True(home.IsEffectivelyEnabled, "The Home pill was disabled on the dashboard, where it is the current location.");
            Assert.DoesNotContain(":disabled", home.Classes);
            Assert.Contains("selected", home.Classes);
            Assert.Equal(DesignTokens.Resolve("SurfaceRaisedBrush", variant), home.Background);

            // ...and at the glass, because the face is what the reader actually sees. Left of the
            // caption, inside the pill's own padding.
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceRaisedBrush", variant),
                PixelInside(host, window, home));

            // Settings and Help stay unavailable (#96) and must read as *unavailable nav* rather
            // than as the same thing Home is: a different face, not merely a different label.
            foreach (var pill in pills.Where(pill => !Equals(pill.Content, "Home")))
            {
                Assert.False(pill.IsEffectivelyEnabled, $"{pill.Content} is not implemented yet and must not read as live nav.");
                Assert.DoesNotContain("selected", pill.Classes);
                Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), pill.Background);
            }
        }

        [AvaloniaFact]
        public void TheHomeNavPill_WhileAnEditorIsOpen_StopsReadingAsTheCurrentLocation()
        {
            // The other half: the selected face has to be the dashboard's alone, or it says nothing.
            using var scenes = new ViewSceneFactory();

            var shell = scenes.CreateShell();
            var window = new MainWindow(shell, new Services.FakeNotificationService());

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            shell.OpenDevice(shell.Dashboard.DeviceCards[0].Snapshot);

            host.Capture();

            var home = NavPillsOf(window).Single(pill => Equals(pill.Content, "Home"));

            Assert.True(home.IsEffectivelyEnabled);
            Assert.DoesNotContain("selected", home.Classes);
            Assert.NotEqual(DesignTokens.Resolve("SurfaceRaisedBrush", ThemeVariant.Dark), home.Background);
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
            var context = await scenes.CreateDemoModeContextAsync(viewType).ConfigureAwait(true);

            // An editor that draws its own toolbar reads its chip from the shell, as data
            // (IShellChrome), and in the app that shell is reporting the demo session the editor
            // was opened under. A shell that never opened anything is a *dashboard* with nothing
            // detected, whose chip reads v-Drive Error (mockup 1d) — so the session is opened here
            // rather than inferred from an empty scan.
            if (context is DeviceEditorViewModel editorContext && editorContext.ProvidesOwnChrome)
            {
                var shell = scenes.CreateShell(withDevices: false);

                shell.OpenDevice(editorContext.Device);

                editorContext.Shell = shell;
            }

            view.DataContext = context;

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

        /// <summary>
        /// The shell's own status chip, picked out by the view model behind it rather than by being
        /// the only one in the window. A bare <c>Single</c> over the tree survived only while the
        /// scene had no cards: any card kind that ever carries a chip of its own — a future
        /// advisory, say — would have turned this into a crash rather than a failure, in a test that
        /// is not about cards at all.
        /// </summary>
        /// <summary>The app bar's Home / Settings / Help, in the order the strip draws them.</summary>
        private static IReadOnlyList<Button> NavPillsOf(MainWindow window)
        {
            return window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("navPill"))
                .ToArray();
        }

        /// <summary>The colour four pixels inside a control's left edge: on its face, clear of any glyph.</summary>
        private static Avalonia.Media.Color PixelInside(ThemedHost host, Visual root, Visual control)
        {
            var frame = host.Capture();
            var probe = control.TranslatePoint(new Point(4, control.Bounds.Height / 2), root)
                ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

            return FramePixels.At(frame, (int)probe.X, (int)probe.Y);
        }

        /// <summary>
        /// A shell whose chip is in the demo state, which is what an <b>open demo session</b>
        /// produces. The Advantage2 is used because its editor is the placeholder, which draws no
        /// chrome of its own — so the app bar this suite reads is still on screen.
        /// </summary>
        private static MainWindowViewModel CreateDemoShell(ViewSceneFactory scenes)
        {
            var shell = scenes.CreateShell(withDevices: false);

            shell.OpenDevice(KinesisEdit.Services.DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.Advantage2)));

            return shell;
        }

        private static Border AppBarStatusChipOf(MainWindow window)
        {
            return window.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Classes.Contains("statusChip") && border.DataContext is MainWindowViewModel);
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
