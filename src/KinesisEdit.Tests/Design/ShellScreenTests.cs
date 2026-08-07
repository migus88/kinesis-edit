using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The shell's two nav screens at the glass — Settings and Help (issue #96).
    /// <para>
    /// The view-model halves are <c>ViewModels/SettingsScreenViewModelTests</c> and
    /// <c>ViewModels/HelpScreenViewModelTests</c>. This suite is only for what a rendered tree can
    /// answer and a view model cannot: that a pick made on the real control reaches the store and
    /// the applier, that the switches are the app's segmented control rather than Fluent's list,
    /// that the window row renders <b>no</b> control at all, and that every link out of the app is
    /// drawn with the external-link mark rather than an arrow character.
    /// </para>
    /// </summary>
    public class ShellScreenTests
    {
        /// <summary>
        /// How far a sampled hatch pixel may sit from the token it is drawn in. The bands are
        /// diagonal on a 12×10 rounded swatch, so every one of them is anti-aliased against the
        /// surface behind it; the closest sample measures ~6 units off. Wide enough to survive a
        /// rasteriser change, far narrower than the ~150 that separates the two variants' values.
        /// </summary>
        private const double AntiAliasTolerance = 24;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheSettingsScreen_UnderEachVariant_DrawsBothSwitchesAsSegmentedControls(string variantName)
        {
            // A one-of-N group is a SelectingItemsControl on the app's own trough, never a row of
            // buttons and never Fluent's list. A missing Theme= fails silently — the control keeps
            // Fluent's template and merely looks a little wrong.
            var variant = ToVariant(variantName);

            using var screen = CreateSettingsScreen(out _, out _, out _);

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var switches = view.GetVisualDescendants().OfType<ListBox>().ToArray();

            Assert.Equal(2, switches.Length);
            Assert.All(switches, list =>
            {
                Assert.Same(DesignTokens.Resolve("SegmentedControl", variant), list.Theme);
                Assert.Equal(3, list.ItemCount);
            });
        }

        [AvaloniaFact]
        public void TheSettingsScreen_Always_LightsTheSegmentTheStoreAlreadyHolds()
        {
            var store = new FakeHostPreferencesStore(new HostPreferences
            {
                Theme = AppThemePreference.Dark,
                Motion = MotionPreference.NeverReduce
            });

            using var screen = new SettingsScreenViewModel(store, _ => { }, _ => { });

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.Equal(
                SettingsScreenViewModel.DarkCaption,
                ((AppThemeOption)ThemeSwitch(view).SelectedItem!).Caption);
            Assert.Equal(
                SettingsScreenViewModel.NeverReduceCaption,
                ((MotionOption)MotionSwitch(view).SelectedItem!).Caption);
        }

        [AvaloniaFact]
        public void PickingAThemeSegment_OnTheRealScreen_PersistsItAndAppliesItLive()
        {
            // The acceptance criterion, driven through the control rather than the command: the
            // view's SelectionChanged handler is the only thing between the segment and the store,
            // and nothing in the view model can see whether it is wired.
            using var screen = CreateSettingsScreen(out var store, out var themes, out _);

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var list = ThemeSwitch(view);

            list.SelectedItem = screen.ThemeOptions.Single(option => option.Value == AppThemePreference.Light);

            Assert.Equal(AppThemePreference.Light, store.Current.Theme);
            Assert.Equal([AppThemePreference.Light], themes);
            Assert.Same(screen.SelectedThemeOption, list.SelectedItem);
        }

        [AvaloniaFact]
        public void PickingAMotionSegment_OnTheRealScreen_PersistsItAndAppliesItLive()
        {
            using var screen = CreateSettingsScreen(out var store, out _, out var motions);

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var list = MotionSwitch(view);

            list.SelectedItem = screen.MotionOptions.Single(option => option.Value == MotionPreference.AlwaysReduce);

            Assert.Equal(MotionPreference.AlwaysReduce, store.Current.Motion);
            Assert.Equal([MotionPreference.AlwaysReduce], motions);
            Assert.Same(screen.SelectedMotionOption, list.SelectedItem);
        }

        [AvaloniaFact]
        public void ARefusedPick_Always_SnapsTheSegmentBack()
        {
            // Nothing refuses a host preference today, so the refusal is staged: a store that keeps
            // whatever it was given. The handler is what puts the control back on the view model's
            // answer, and it is the same handler the lighting tab's firmware-gated layer relies on.
            using var screen = new SettingsScreenViewModel(new RefusingHostPreferencesStore(), _ => { }, _ => { });

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var list = ThemeSwitch(view);
            var kept = screen.SelectedThemeOption;

            list.SelectedItem = screen.ThemeOptions.Single(option => option.Value == AppThemePreference.Light);

            Assert.Same(kept, screen.SelectedThemeOption);
            Assert.Same(kept, list.SelectedItem);
        }

        [AvaloniaFact]
        public void TheSettingsScreen_Never_RendersAControlForTheWindowGeometry()
        {
            // The window's size and position are restored silently and there is no way to turn that
            // off, so the screen states it in prose and renders no control. A disabled switch would
            // claim a choice that does not exist.
            using var screen = CreateSettingsScreen(out _, out _, out _);

            var view = new SettingsScreenView { DataContext = screen };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.Empty(view.GetVisualDescendants().OfType<ToggleButton>());
            Assert.Empty(view.GetVisualDescendants().OfType<CheckBox>());
            Assert.Contains(
                SettingsScreenViewModel.WindowNote,
                view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheHelpScreen_UnderEachVariant_DrawsOneLinkPerDestination(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(HelpScreenView).FullName!);
            var screen = (HelpScreenViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var expected = screen.GeneralLinks.Count + screen.WebToolLinks.Count + screen.TroubleshootingLinks.Count;

            Assert.Equal(expected, LinkButtons(view).Count);
        }

        [AvaloniaFact]
        public async Task EveryHelpLink_IsDrawnWithTheExternalLinkMarkRatherThanAnArrowCharacter()
        {
            // "The arrow is geometry, not text." A caption carrying ↗ would be a glyph the shipped
            // families may not have, and only reading the tree can tell the two apart.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(HelpScreenView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var mark = DesignTokens.Resolve("IconExternalLink", ThemeVariant.Dark);
            var buttons = LinkButtons(view);

            Assert.NotEmpty(buttons);
            Assert.All(buttons, button =>
            {
                var marks = button.GetVisualDescendants().OfType<Icon>().ToArray();

                Assert.Single(marks);
                Assert.Same(mark, marks[0].Data);
            });

            var text = string.Concat(view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

            Assert.DoesNotContain('↗', text);
        }

        [AvaloniaFact]
        public async Task EveryHelpLink_ReachesTheLinkTheme()
        {
            // A bridge that went missing fails silently: the button keeps Fluent's template and the
            // link ramp becomes a grey face.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(HelpScreenView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var theme = DesignTokens.Resolve("LinkButton", ThemeVariant.Dark);

            Assert.All(LinkButtons(view), button => Assert.Same(theme, button.Theme));
        }

        [AvaloniaFact]
        public async Task ClickingAHelpLink_OpensThatPage()
        {
            // The rows carry no command of their own — the screen holds the one IUrlLauncher — so
            // the CommandParameter reaching the right row is the whole wiring.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(HelpScreenView).FullName!);
            var screen = (HelpScreenViewModel)view.DataContext!;
            var opened = new List<string>();

            // The scene's launcher is shared; a private one keeps the assertion about this click.
            var probe = new HelpScreenViewModel(new RecordingUrlLauncher(opened));

            view.DataContext = probe;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var buttons = LinkButtons(view);

            buttons[0].Command!.Execute(buttons[0].CommandParameter);

            Assert.Equal([probe.GeneralLinks[0].Url], opened);
            Assert.Equal(HelpScreenViewModel.KinesisSupportUrl, screen.GeneralLinks[0].Url);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheAboutCard_ShowsTheAppNameAndTheRealVersion_InSansRatherThanMono(string variantName)
        {
            // The mono law: mono means "this string exists verbatim in a config file". A version the
            // app reads out of its own assembly is the app speaking, however technical it looks.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(HelpScreenView).FullName!);
            var screen = (HelpScreenViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var blocks = view.GetVisualDescendants().OfType<TextBlock>().ToArray();
            var name = Assert.Single(blocks, block => block.Text == HelpScreenViewModel.AppName);
            var version = Assert.Single(blocks, block => block.Text == screen.VersionLine);

            Assert.Contains(HelpScreenViewModel.ReadVersion(typeof(HelpScreenViewModel).Assembly), version.Text!);
            Assert.Equal(DesignTokens.Resolve("FontSans", variant), name.FontFamily);
            Assert.Equal(DesignTokens.Resolve("FontSans", variant), version.FontFamily);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheHatchTile_AcrossALiveThemeFlip_RepaintsInTheNewVariantsToken(string startVariantName)
        {
            // The hatch is declared once per theme variant and painted from a role. The half of that
            // no assertion about the brush object can see is what the *pixels* do when the variant
            // changes while the app is running — which is exactly what the Settings screen's theme
            // switch makes possible. So this reads the frame, twice, on a real markup call site: the
            // board legend's locked swatch, whose Fill is {DynamicResource HatchBrush}.
            var start = ToVariant(startVariantName);
            var end = start == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(BoardLegendView).FullName!);

            using var host = ThemedHost.Show(view, start);

            var swatch = HatchSwatch(view);

            AssertHatchPaints(host, swatch, start, end);

            // The live flip. Nothing is rebuilt and nothing is reopened; the variant simply moves,
            // exactly as ThemeApplier moves it on the running application.
            host.Window.RequestedThemeVariant = end;

            Dispatcher.UIThread.RunJobs();

            AssertHatchPaints(host, swatch, end, start);
        }

        /// <summary>
        /// Asserts the hatch's interior actually paints <paramref name="expected"/>'s
        /// <c>SurfaceLineHigh</c> rather than <paramref name="forbidden"/>'s.
        /// <para>
        /// Two things make this a <i>nearest</i> match rather than an equality. The swatch is 12×10
        /// with a 2 px corner radius and the bands are diagonal, so every stripe pixel is
        /// anti-aliased against the surface under it and none lands on the token exactly — the
        /// closest sample sits a few units off. And the sampling starts two pixels in from every
        /// edge because the swatch's dashed <b>stroke</b> is drawn in the same role, so a sample on
        /// the border would pass whatever the fill did.
        /// </para>
        /// </summary>
        private static void AssertHatchPaints(
            ThemedHost host,
            Control swatch,
            ThemeVariant expected,
            ThemeVariant forbidden)
        {
            var frame = host.Capture();
            var origin = swatch.TranslatePoint(new Point(0, 0), host.Window)
                ?? throw new InvalidOperationException("The hatch swatch is not in the window's tree.");

            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", expected);
            var other = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", forbidden);
            var painted = new List<Color>();

            for (var x = 2; x < swatch.Bounds.Width - 2; x++)
            {
                for (var y = 2; y < swatch.Bounds.Height - 2; y++)
                {
                    painted.Add(FramePixels.At(frame, (int)origin.X + x, (int)origin.Y + y));
                }
            }

            Assert.NotEmpty(painted);

            var toExpected = painted.Min(pixel => Distance(pixel, stripe));
            var toForbidden = painted.Min(pixel => Distance(pixel, other));

            Assert.True(
                toExpected <= AntiAliasTolerance,
                $"No pixel of the hatch came within {AntiAliasTolerance} of {expected}'s SurfaceLineHigh; "
                + $"the closest was {toExpected:F1} away.");
            Assert.True(
                toExpected < toForbidden,
                $"The hatch is closer to {forbidden}'s SurfaceLineHigh ({toForbidden:F1}) than to "
                + $"{expected}'s ({toExpected:F1}); the variant flip did not repaint it.");
        }

        private static double Distance(Color first, Color second)
        {
            var red = first.R - second.R;
            var green = first.G - second.G;
            var blue = first.B - second.B;

            return Math.Sqrt((red * red) + (green * green) + (blue * blue));
        }

        private static Control HatchSwatch(Control view)
        {
            var hatch = DesignTokens.Resolve("HatchBrush", ThemeVariant.Dark);

            return view.GetVisualDescendants()
                .OfType<Shape>()
                .Single(shape => shape.Fill is DrawingBrush || ReferenceEquals(shape.Fill, hatch));
        }

        private static IReadOnlyList<Button> LinkButtons(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("link"))
                .ToArray();
        }

        private static ListBox ThemeSwitch(Control view)
        {
            return view.GetVisualDescendants().OfType<ListBox>().First();
        }

        private static ListBox MotionSwitch(Control view)
        {
            return view.GetVisualDescendants().OfType<ListBox>().Last();
        }

        private static SettingsScreenViewModel CreateSettingsScreen(
            out FakeHostPreferencesStore store,
            out List<AppThemePreference> appliedThemes,
            out List<MotionPreference> appliedMotions)
        {
            var themes = new List<AppThemePreference>();
            var motions = new List<MotionPreference>();

            store = new FakeHostPreferencesStore();
            appliedThemes = themes;
            appliedMotions = motions;

            return new SettingsScreenViewModel(store, themes.Add, motions.Add);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>A store that accepts nothing, standing in for a preference the app one day refuses.</summary>
        private sealed class RefusingHostPreferencesStore : IHostPreferencesStore
        {
            public HostPreferences Current { get; } = HostPreferences.Default;

            public event Action? Changed;

            public void Update(Func<HostPreferences, HostPreferences> mutate)
            {
                ArgumentNullException.ThrowIfNull(mutate);

                // Deliberately discarded: the caller asked, and the answer is no.
                mutate(Current);

                Changed?.Invoke();
            }
        }

        /// <summary>An <see cref="IUrlLauncher"/> that appends to a list the caller owns.</summary>
        private sealed class RecordingUrlLauncher : IUrlLauncher
        {
            private readonly List<string> _opened;

            public RecordingUrlLauncher(List<string> opened)
            {
                _opened = opened;
            }

            public bool Open(string url)
            {
                _opened.Add(url);

                return true;
            }
        }
    }
}
