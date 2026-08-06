using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The dashboard's own geometry, at the glass: the card's fixed height, the two-column grid,
    /// the status rail and the two states that are defined by what they do <b>not</b> draw.
    /// <para>
    /// The design's claim about the card is a claim about the whole set — "each fixed height,
    /// buttons swap" (docs/design/handoff.md), "the card keeps its size and its buttons keep their
    /// positions … so a 2s refresh can't shift the layout under the cursor"
    /// (docs/design/mockups.md §2e). One state rendered on its own proves nothing about that; every
    /// test here therefore renders the whole set and compares its members.
    /// </para>
    /// </summary>
    public class DashboardCardRenderTests
    {
        /// <summary>
        /// Long enough for the 160 ms insert to finish. Transitions run on wall-clock time and
        /// ticking the render timer does not advance them, so a card measured too early is caught
        /// part-way up (docs/app/design-system.md, "Rendered-frame capture").
        /// </summary>
        /// <summary>How many times the scan bar's indicator is read when asking whether it moves.</summary>
        private const int IndicatorSamples = 3;

        /// <summary>The template part Fluent slides across an indeterminate <see cref="ProgressBar"/>.</summary>
        private const string IndeterminateIndicatorName = "IndeterminateProgressBarIndicator";

        /// <summary>
        /// The members <see cref="ITransition"/> hides behind explicit implementation; reflection is
        /// the only way in, exactly as in <see cref="MotionBudgetTests"/>.
        /// </summary>
        private const string AnimatedPropertyName = "Property";

        /// <inheritdoc cref="AnimatedPropertyName" />
        private const string DurationPropertyName = "Duration";

        private static readonly TimeSpan _insertSettled = TimeSpan.FromMilliseconds(320);

        /// <summary>
        /// Waits between height samples taken across one 160 ms insert. Several, rather than one
        /// well-chosen instant: transitions advance on wall-clock time, so a single sample on a
        /// loaded machine can land after the flight is over and see only the settled value.
        /// </summary>
        private static readonly TimeSpan[] _insertSamples =
        [
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30)
        ];

        /// <summary>The same, across one 220 ms status cross-fade.</summary>
        private static readonly TimeSpan[] _crossFadeSamples =
        [
            TimeSpan.FromMilliseconds(15),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(30),
            TimeSpan.FromMilliseconds(40),
            TimeSpan.FromMilliseconds(40)
        ];

        /// <summary>Every card kind the grid holds, by the name of the face it wears.</summary>
        public static TheoryData<string> CardKinds()
        {
            return new TheoryData<string>
            {
                nameof(DeviceCardState.Connected),
                nameof(DeviceCardState.Resting),
                nameof(DeviceCardState.CannotAccess),
                nameof(DeviceCardState.Scanning),
                WebToolKind
            };
        }

        private const string WebToolKind = "WebTool";

        [AvaloniaTheory]
        [MemberData(nameof(CardKinds))]
        public async Task EveryCardKind_RendersAtTheOneFixedHeight(string kind)
        {
            // The rail, the status line, the explanation and the button captions all change with the
            // state; the card's height may not, because the button under the pointer during a 2s
            // refresh has to still be there — and at the same place — when the refresh lands.
            using var scenes = new ViewSceneFactory();

            var view = CreateCard(scenes, kind);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var card = CardBorderOf(view);
            var expected = (double)DesignTokens.Resolve("HeightDeviceCard", ThemeVariant.Dark);

            Assert.Equal(expected, card.Bounds.Height, 1);
        }

        [AvaloniaFact]
        public async Task BetweenTheStates_OnlyTheRailTheStatusLineAndTheButtonsMove()
        {
            // The other half of the same rule, and the one a height assertion cannot see: a card
            // that stayed 212 tall while its art or its Configure button slid down the card would
            // still shift the layout under the cursor.
            using var scenes = new ViewSceneFactory();

            var anchors = new List<(string Kind, Rect Art, Point PrimaryAction)>();

            foreach (var state in Enum.GetValues<DeviceCardState>())
            {
                var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(state) };

                using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

                await SettleAsync(host).ConfigureAwait(true);

                var card = CardBorderOf(view);
                var art = Descendants<Border>(view).Single(border => border.Classes.Contains("deviceArtFrame"));
                var primary = Descendants<Button>(view).Single(button => button.Classes.Contains("primaryAction"));

                anchors.Add((
                    state.ToString(),
                    new Rect(TopLeftIn(art, card), art.Bounds.Size),
                    TopLeftIn(primary, card)));
            }

            var first = anchors[0];

            Assert.All(anchors, anchor =>
            {
                Assert.True(
                    Close(anchor.Art, first.Art),
                    $"{anchor.Kind} draws its device art at {anchor.Art}, not {first.Art}.");
                Assert.True(
                    Close(anchor.PrimaryAction, first.PrimaryAction),
                    $"{anchor.Kind} puts its primary action at {anchor.PrimaryAction}, not {first.PrimaryAction}.");
            });
        }

        [AvaloniaTheory]
        [InlineData("Connected", "StatusOkBrush")]
        [InlineData("CannotAccess", "StatusErrorBrush")]
        public async Task TheStatusRail_TakesTheStatusFillOfTheStateThatHasOne(string stateName, string brushKey)
        {
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(Enum.Parse<DeviceCardState>(stateName)) };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var rail = RailOf(view);

            Assert.True(rail.IsVisible, $"The {stateName} card drew no status rail.");
            Assert.Equal(DesignTokens.Resolve(brushKey, ThemeVariant.Dark), rail.Background);

            // 2px, flush on the left edge: inside the border and outside the padding — so it starts
            // where the frame's own hairline ends, not 14 in where the content does.
            var card = CardBorderOf(view);

            Assert.Equal((double)DesignTokens.Resolve("WidthCardStatusRail", ThemeVariant.Dark), rail.Bounds.Width, 1);
            Assert.Equal(card.BorderThickness.Left, TopLeftIn(rail, card).X, 1);
            Assert.Equal(card.Bounds.Height - card.BorderThickness.Top - card.BorderThickness.Bottom, rail.Bounds.Height, 1);
        }

        [AvaloniaTheory]
        [InlineData("Resting")]
        [InlineData("Scanning")]
        public async Task TheTwoStatesWithNoStatusOfTheirOwn_PaintNoRail(string stateName)
        {
            // Resting is "idle and quiet — no red, no spinner. This is the resting state, not an
            // error" (mockup 2e); a scan in flight is transient and reports nothing about a drive.
            // Neither may borrow a colour from the two states that do.
            //
            // The rail is still *there*, and deliberately so: it is the thing the design
            // cross-fades, and IsVisible would collapse it with nothing left for the fade to run
            // on. Painting nothing is what "no rail" means here.
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(Enum.Parse<DeviceCardState>(stateName)) };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var rail = RailOf(view);

            Assert.True(rail.IsVisible, "The rail was hidden, which is what makes the cross-fade impossible.");
            Assert.Equal(Colors.Transparent, ColorOf(rail.Background));

            // And at the glass: where the rail sits is the same colour as the card face beside it,
            // so the rail leaves no band at all. Compared against a neighbouring pixel rather than
            // against a token, because that holds whether or not the panel face is opaque.
            var frame = host.Capture();
            var probe = rail.TranslatePoint(new Point(0, rail.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The rail is not in the window's visual tree.");

            Assert.Equal(
                FramePixels.At(frame, (int)probe.X + 6, (int)probe.Y),
                FramePixels.At(frame, (int)probe.X, (int)probe.Y));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStatusRail_CarriesTheTwoHundredAndTwentyMillisecondCrossFade(string variantName)
        {
            // "Status changes cross-fade the status pill and card status fill over 220 ms; text
            // swaps are instant" (mockup 2b). The app bar's chip gets it from the StatusPill
            // control theme; the card has no chip — the design draws it a bare status *line* — so
            // the rail is the card's status fill and has to carry the fade itself.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Connected) };

            using var host = ThemedHost.Show(view, variant, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var transitions = RailOf(view).Transitions;

            Assert.NotNull(transitions);

            // The bare alias, never StatusFillTransitionsFull: MotionResourceBinder re-points it
            // for reduce-motion, and a rail bound to the concrete set would ignore that.
            Assert.Same(DesignTokens.Resolve("StatusFillTransitions", variant), transitions);
            Assert.Contains(Border.BackgroundProperty, transitions.Select(AnimatedPropertyOf));
        }

        [AvaloniaFact]
        public async Task TheStatusRail_WhenTheDriveChangesState_FadesBetweenTheTwoFills()
        {
            // The assertion the resource identity above cannot make: the fade actually runs on the
            // real card, driven by the real class binding. Mid-flight the rail is neither green nor
            // red, and the caption beside it has already swapped — text is instant.
            using var scenes = new ViewSceneFactory();

            var card = scenes.CreateDeviceCard(DeviceCardState.Connected);
            var view = new DeviceCardView { DataContext = card };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var rail = RailOf(view);
            var ok = DesignTokens.ResolveBrushColor("StatusOkBrush", ThemeVariant.Dark);
            var error = DesignTokens.ResolveBrushColor("StatusErrorBrush", ThemeVariant.Dark);

            Assert.Equal(ok, ColorOf(rail.Background));

            card.Update(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            var sampled = new List<Color>();

            foreach (var wait in _crossFadeSamples)
            {
                await Task.Delay(wait).ConfigureAwait(true);

                host.Capture();

                sampled.Add(ColorOf(rail.Background));
            }

            Assert.Contains(sampled, colour => colour != ok && colour != error);
            Assert.Equal(DeviceCardViewModel.CannotAccessStatusText, CaptionOf(view).Text);

            // ...and it lands on the new fill rather than stalling somewhere in between.
            await SettleAsync(host).ConfigureAwait(true);

            Assert.Equal(error, ColorOf(rail.Background));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ARestingCard_DrawsNoRedAndNoSpinner(string variantName)
        {
            // The design says it twice, so it is asserted here rather than left to the rail test: a
            // known device whose drive is simply not mounted is not broken and is not busy. The
            // dashed state mark takes the muted text role, not the error one.
            var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Resting) };

            using var host = ThemedHost.Show(view, variant, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var errorRoles = new[] { "StatusErrorBrush", "StatusErrorTextBrush", "StatusErrorTintBrush" }
                .Select(key => DesignTokens.Resolve(key, variant))
                .ToArray();

            foreach (var icon in Descendants<Icon>(view).Where(icon => icon.IsVisible))
            {
                Assert.DoesNotContain(icon.Stroke, errorRoles);
                Assert.DoesNotContain(icon.Fill, errorRoles);
                Assert.DoesNotContain("spinner", icon.Classes);
            }

            foreach (var text in Descendants<TextBlock>(view).Where(text => text.IsVisible))
            {
                Assert.DoesNotContain("statusError", text.Classes);
                Assert.DoesNotContain(text.Foreground, errorRoles);
            }

            Assert.All(
                Descendants<Border>(view).Where(border => border.IsVisible),
                border => Assert.DoesNotContain(border.Background, errorRoles));

            // The busy affordance is the one thing a resting card must not have at all, and it is
            // the state's whole point — so it is asserted as an absence rather than inferred from
            // the icons above.
            Assert.DoesNotContain(Descendants<ProgressBar>(view), bar => bar.IsVisible);
        }

        [AvaloniaFact]
        public async Task AScanningCard_AddsAThreePixelIndeterminateBarAndNothingElse()
        {
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Scanning) };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var card = CardBorderOf(view);
            var bar = Descendants<ProgressBar>(view).Single();

            Assert.True(bar.IsVisible);
            Assert.True(bar.IsIndeterminate);

            // 3px and flush on the card's bottom edge. Fluent's own MinHeight is what a bar this
            // thin has to clear, and it is cleared on the control rather than in its template.
            Assert.Equal(3, bar.Bounds.Height, 1);
            Assert.Equal(
                card.Bounds.Height - card.BorderThickness.Bottom - bar.Bounds.Height,
                TopLeftIn(bar, card).Y,
                1);
        }

        [AvaloniaFact]
        public async Task TheGrid_PutsTwoCardsInARow_TwelveApart_WithNoTrailingGutter()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(DashboardView).FullName!).ConfigureAwait(true);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            await SettleAsync(host).ConfigureAwait(true);

            var items = CardsOf(view);
            var gap = (double)DesignTokens.Resolve("CardGridGap", ThemeVariant.Dark);

            Assert.True(items.ItemCount >= 3, $"The dashboard scene rendered {items.ItemCount} cards; the grid needs three.");

            var first = ContainerAt(items, 0);
            var second = ContainerAt(items, 1);
            var third = ContainerAt(items, 2);

            // Two per row, and the two share a row.
            Assert.Equal(TopLeftIn(first, items).Y, TopLeftIn(second, items).Y, 1);
            Assert.Equal(gap, TopLeftIn(second, items).X - RightIn(first, items), 1);

            // The third wraps, one gap below.
            Assert.Equal(TopLeftIn(first, items).X, TopLeftIn(third, items).X, 1);
            Assert.Equal(TopLeftIn(first, items).Y + first.Bounds.Height + gap, TopLeftIn(third, items).Y, 1);

            // No trailing gutter, and nothing clipped: the right column ends on the grid's own right
            // edge rather than 12 short of it or 12 past it.
            Assert.Equal(items.Bounds.Width, RightIn(second, items), 1);
            Assert.Equal(0, TopLeftIn(first, items).X, 1);
        }

        [AvaloniaFact]
        public async Task ACard_IsBornCollapsedAndReleasedOnceItJoinsTheTree()
        {
            // The insert animation's whole mechanism. A transition never runs on an initial value,
            // so a card that is simply born at HeightDeviceCard appears rather than animating in;
            // `collapsed` is what turns the arrival into a height *change*, and it is a class rather
            // than IsVisible because IsVisible collapses a control instantly and leaves the
            // transition nothing to run on.
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Connected) };

            // Read off the authored content rather than the visual tree: the point is that the card
            // carries the class before it has ever been measured.
            var card = Assert.IsType<Border>(view.Content);

            Assert.Contains("collapsed", card.Classes);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            Assert.DoesNotContain("collapsed", card.Classes);
            Assert.Equal(
                (double)DesignTokens.Resolve("HeightDeviceCard", ThemeVariant.Dark),
                card.Bounds.Height,
                1);
        }

        [AvaloniaFact]
        public async Task ACard_ActuallyAnimatesItsHeightIn()
        {
            // The mechanism above holds with the animation deleted: born collapsed, released on
            // attach and settled at 212 is all still true of a card that simply snaps to its
            // height. What makes it an *insert* is the 160 ms in between, so that is asserted twice
            // — the budget's own set is on the card, and the height is caught part-way up.
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Connected) };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            var card = Assert.IsType<Border>(view.Content);
            var transitions = card.Transitions;

            Assert.NotNull(transitions);

            // The bare alias: MotionResourceBinder points it at the full or the reduced set, and
            // the reduced one is empty — reduce-motion drops height animation outright.
            Assert.Same(DesignTokens.Resolve("ListInsertTransitions", ThemeVariant.Dark), transitions);

            var insert = Assert.Single(transitions);

            Assert.Equal(Layoutable.HeightProperty, AnimatedPropertyOf(insert));
            Assert.Equal((TimeSpan)DesignTokens.Resolve("DurationListInsert", ThemeVariant.Dark), DurationOf(insert));

            // ...and it runs. Sampled across the flight rather than at one instant, because
            // transitions advance on wall-clock time and a loaded machine can skip a beat.
            var full = (double)DesignTokens.Resolve("HeightDeviceCard", ThemeVariant.Dark);
            var sampled = new List<double>();

            foreach (var wait in _insertSamples)
            {
                await Task.Delay(wait).ConfigureAwait(true);

                host.Capture();

                sampled.Add(card.Bounds.Height);
            }

            Assert.Contains(sampled, height => height > 1 && height < full - 1);
        }

        [AvaloniaFact]
        public async Task ACardThatHasBeenOnScreenBefore_IsRehostedWithoutAnimating()
        {
            // The insert animation belongs to a newly detected device. ViewLocator builds a fresh
            // view whenever CurrentView changes, so every trip back to the dashboard rebuilds every
            // card — and a card that animated then would make the whole grid grow in from zero on
            // an ordinary navigation, which is not an insertion of anything.
            using var scenes = new ViewSceneFactory();

            var model = scenes.CreateDeviceCard(DeviceCardState.Connected);
            var full = (double)DesignTokens.Resolve("HeightDeviceCard", ThemeVariant.Dark);

            using (var first = ThemedHost.Show(new DeviceCardView { DataContext = model }, ThemeVariant.Dark, 500, 300))
            {
                await SettleAsync(first).ConfigureAwait(true);
            }

            var rehosted = new DeviceCardView { DataContext = model };

            using var host = ThemedHost.Show(rehosted, ThemeVariant.Dark, 500, 300);

            // No delay at all: Show has already run the layout pass, so a card that was going to
            // animate is at the bottom of its flight right now and this reads it there.
            var card = Assert.IsType<Border>(rehosted.Content);

            Assert.DoesNotContain("collapsed", card.Classes);
            Assert.Equal(full, card.Bounds.Height, 1);
        }

        [AvaloniaFact]
        public async Task ACardInsertedBesideAFullHeightSibling_GrowsFromTheTopOfItsRow()
        {
            // A Layoutable with an explicit Height and the default Stretch is *centred* in its
            // slot, and a card joining a UniformGrid row that already holds a settled sibling is
            // arranged in a full-height slot from its very first frame. Left stretched it would sit
            // half a card down the row and grow outward from its middle — which no assertion about
            // its height can see, and which is not what "animates in at the end of the list" means.
            using var scenes = new ViewSceneFactory();

            var grid = new UniformGrid { Columns = 2, VerticalAlignment = VerticalAlignment.Top };
            var host = new Panel();

            host.Children.Add(grid);
            grid.Children.Add(new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Connected) });

            using var window = ThemedHost.Show(host, ThemeVariant.Dark, 1000, 400);

            await SettleAsync(window).ConfigureAwait(true);

            var arriving = new DeviceCardView { DataContext = scenes.CreateDeviceCard(DeviceCardState.Resting) };

            grid.Children.Add(arriving);

            await Task.Delay(60).ConfigureAwait(true);

            window.Capture();

            var settled = CardBorderOf((Control)grid.Children[0]);
            var inserting = CardBorderOf(arriving);

            Assert.Equal(TopLeftIn(settled, host).Y, TopLeftIn(inserting, host).Y, 1);
        }

        [AvaloniaTheory]
        [InlineData("Connected", false)]
        [InlineData("Resting", false)]
        [InlineData("CannotAccess", false)]
        [InlineData("Scanning", true)]
        public async Task TheScanBar_IsOnlyIndeterminateWhileTheCardIsScanning(string stateName, bool expected)
        {
            // Fluent drives the bar's endless slide off the `:indeterminate` pseudo-class, and
            // hiding a control does not tear its template down: every card goes through Scanning on
            // every 2 s refresh, so a hardcoded IsIndeterminate leaves that animation running
            // behind every card on the dashboard for the rest of the session. Bound, the
            // pseudo-class drops with the state and the animation stops with it.
            using var scenes = new ViewSceneFactory();

            var view = new DeviceCardView { DataContext = scenes.CreateDeviceCard(Enum.Parse<DeviceCardState>(stateName)) };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var bar = Descendants<ProgressBar>(view).Single();

            Assert.Equal(expected, bar.IsIndeterminate);
            Assert.Equal(expected, bar.Classes.Contains(":indeterminate"));
        }

        [AvaloniaFact]
        public async Task TheScanBar_WhenTheScanEnds_StopsMovingRatherThanRunningOnBehindTheCard()
        {
            // ...and the same thing at the mechanism: the indicator the animation slides is still
            // in the tree once the bar is hidden, so this reads it directly. Two samples that
            // differ mean the render loop is still animating a card nobody can see.
            using var scenes = new ViewSceneFactory();

            var model = scenes.CreateDeviceCard(DeviceCardState.Scanning);
            var view = new DeviceCardView { DataContext = model };

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 500, 300);

            await SettleAsync(host).ConfigureAwait(true);

            var moving = await SampleIndicatorAsync(host, view).ConfigureAwait(true);

            Assert.True(moving.Distinct().Count() > 1, "The scanning bar never moved, so this test proves nothing.");

            model.IsScanning = false;

            await SettleAsync(host).ConfigureAwait(true);

            var stopped = await SampleIndicatorAsync(host, view).ConfigureAwait(true);

            Assert.True(stopped.Distinct().Count() == 1, $"The hidden bar kept animating: {string.Join(", ", stopped)}.");
        }

        [AvaloniaFact]
        public async Task ARefreshIsDeferredWhileTheKeyboardIsInsideACard()
        {
            // "A refresh that arrives while a card's own button is under the cursor or focused is
            // deferred until focus leaves — the list never steals a click" (mockup 2b). The rule is
            // the view model's; noticing is the view's, because pointer and focus are Avalonia's.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(DashboardView).FullName!).ConfigureAwait(true);
            var dashboard = (DashboardViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            await SettleAsync(host).ConfigureAwait(true);

            Assert.False(dashboard.IsRefreshSuspended);

            var scanAll = Descendants<Button>(view)
                .First(button => Equals(button.Content, DashboardViewModel.ScanAllActionCaption));
            var configure = Descendants<Button>(view).First(button => button.Classes.Contains("primaryAction"));

            configure.Focus();
            host.Capture();

            Assert.True(dashboard.IsRefreshSuspended, "Focus inside a card did not park the refresh.");

            // And it un-parks: the header's own button is outside the grid, so moving there is
            // "focus left the list".
            scanAll.Focus();
            host.Capture();

            Assert.False(dashboard.IsRefreshSuspended, "Focus leaving the cards did not resume the refresh.");
        }

        private static Control CreateCard(ViewSceneFactory scenes, string kind)
        {
            if (kind == WebToolKind)
            {
                return new WebToolCardView { DataContext = scenes.CreateWebToolCard() };
            }

            return new DeviceCardView { DataContext = scenes.CreateDeviceCard(Enum.Parse<DeviceCardState>(kind)) };
        }

        /// <summary>The card's own frame, whichever card kind drew it.</summary>
        private static Border CardBorderOf(Control view)
        {
            return Descendants<Border>(view)
                .First(border => border.Classes.Contains("deviceCard") || border.Classes.Contains("webToolCard"));
        }

        private static Border RailOf(Control view)
        {
            return Descendants<Border>(view).Single(border => border.Classes.Contains("cardStatusRail"));
        }

        /// <summary>The card's status caption — the one the design swaps instantly while the rail fades.</summary>
        private static TextBlock CaptionOf(Control view)
        {
            return Descendants<TextBlock>(view).First(text => text.Classes.Contains("pill"));
        }

        private static Color ColorOf(IBrush? brush)
        {
            return brush is ISolidColorBrush solid
                ? solid.Color
                : throw new InvalidOperationException($"{brush?.GetType().Name ?? "null"} is not a solid colour.");
        }

        /// <summary>
        /// Where the scan bar's indicator sits, read <see cref="IndicatorSamples"/> times across
        /// real elapsed time. Its slide is a template animation on a part that survives the bar
        /// being hidden, so the position is the only honest answer to "is this still running".
        /// </summary>
        private static async Task<IReadOnlyList<double>> SampleIndicatorAsync(ThemedHost host, Control view)
        {
            var samples = new List<double>();

            for (var sample = 0; sample < IndicatorSamples; sample++)
            {
                await Task.Delay(_insertSettled).ConfigureAwait(true);

                host.Capture();

                var indicator = Descendants<Border>(view)
                    .Single(border => border.Name == IndeterminateIndicatorName);

                samples.Add(Math.Round(indicator.RenderTransform?.Value.M31 ?? 0, 3));
            }

            return samples;
        }

        /// <inheritdoc cref="AnimatedPropertyName" />
        private static AvaloniaProperty AnimatedPropertyOf(ITransition transition)
        {
            return (AvaloniaProperty)ReadTransitionProperty(transition, AnimatedPropertyName);
        }

        /// <inheritdoc cref="AnimatedPropertyName" />
        private static TimeSpan DurationOf(ITransition transition)
        {
            return (TimeSpan)ReadTransitionProperty(transition, DurationPropertyName);
        }

        private static object ReadTransitionProperty(ITransition transition, string name)
        {
            var property = transition.GetType().GetProperty(name)
                ?? throw new InvalidOperationException($"{transition.GetType().Name} has no {name}.");

            return property.GetValue(transition)
                ?? throw new InvalidOperationException($"{transition.GetType().Name}.{name} is null.");
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static ItemsControl CardsOf(Control view)
        {
            return Descendants<ItemsControl>(view).Single(items => items.Name == "Cards");
        }

        private static Control ContainerAt(ItemsControl items, int index)
        {
            return items.ContainerFromIndex(index)
                ?? throw new InvalidOperationException($"No container was generated for card {index}.");
        }

        private static Point TopLeftIn(Visual child, Visual ancestor)
        {
            return child.TranslatePoint(default, ancestor)
                ?? throw new InvalidOperationException("The two visuals share no tree.");
        }

        private static double RightIn(Visual child, Visual ancestor)
        {
            return (child.TranslatePoint(new Point(child.Bounds.Width, 0), ancestor)
                ?? throw new InvalidOperationException("The two visuals share no tree.")).X;
        }

        private static bool Close(Point left, Point right)
        {
            return Math.Abs(left.X - right.X) <= 0.5 && Math.Abs(left.Y - right.Y) <= 0.5;
        }

        private static bool Close(Rect left, Rect right)
        {
            return Close(left.Position, right.Position)
                && Math.Abs(left.Width - right.Width) <= 0.5
                && Math.Abs(left.Height - right.Height) <= 0.5;
        }

        private static IEnumerable<T> Descendants<T>(Control view) where T : Visual
        {
            return view.GetVisualDescendants().OfType<T>();
        }

        private static async Task SettleAsync(ThemedHost host)
        {
            await Task.Delay(_insertSettled).ConfigureAwait(true);

            host.Capture();
        }
    }
}
