using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The Lighting tab at the glass (design mockup 2f): the board header that says what the board
    /// is showing, the mode rail beside it, the parameter footer whose direction row never changes
    /// shape, the paint line under the board, and the frame timer that makes the picture move.
    /// <para>
    /// Everything here is driven through the real view — the scene is the loaded Freestyle Edge RGB
    /// editor's own <c>Lighting</c> panel, and the modes are picked with the very command a rail row
    /// runs — because the claims are about what a user sees, not about what the view model holds.
    /// </para>
    /// </summary>
    public class LightingTabTests
    {
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheBoardHeader_NamesTheModeAndSaysThePreviewIsLive(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Wave);

            host.Capture();

            // The string is the view model's, and it is the whole point of the issue: the board is
            // the mode, and the line over it says which mode and whether it is moving.
            Assert.Equal(
                LightingModeCaptions.SolidCaption,
                LightingModeCaptions.For(LightingMode.Monochrome));
            Assert.Equal(
                LightingModeCaptions.For(LightingMode.Wave) + LightingTabViewModel.LivePreviewSuffix,
                lighting.BoardHeader);
            Assert.Contains(lighting.BoardHeader, VisibleTexts(view));

            SelectMode(lighting, LightingMode.Monochrome);

            host.Capture();

            Assert.Contains(lighting.BoardHeader, VisibleTexts(view));
            Assert.StartsWith(LightingModeCaptions.SolidCaption, lighting.BoardHeader, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public async Task TheDirectionRow_DrawsFourArrows_WhateverTheModeIs()
        {
            // 2f, verbatim: "Directions a mode can't use stay in place, struck through — the row
            // never changes shape as you move down the list." The assertion is therefore about the
            // COUNT being constant across every row of the rail, not about any one mode.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.NotEmpty(lighting.Modes);

            foreach (var mode in lighting.Modes)
            {
                lighting.SelectModeCommand.Execute(mode);

                host.Capture();

                var segments = DirectionSegments(view);

                Assert.Equal(LightingDirectionViewModel.Order.Count, segments.Length);
                Assert.Equal(4, segments.Length);

                // ...and the ones the mode cannot use are the struck ones, in place.
                var struck = segments.Count(button => button.Classes.Contains("unavailable"));

                Assert.Equal(4 - mode.Parameters.Directions.Count, struck);
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AnUnavailableArrow_IsStruckAndUnreachable_RatherThanRemoved(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            // Solid accepts no direction at all, so every one of the four is struck — the widest
            // case of the rule, and the one where a row that hid its unusable arrows would vanish.
            SelectMode(lighting, LightingMode.Monochrome);

            host.Capture();

            var segments = DirectionSegments(view);

            Assert.Equal(4, segments.Length);
            Assert.All(segments, button => Assert.Contains("unavailable", button.Classes));
            Assert.All(segments, button => Assert.False(button.IsHitTestVisible));
            Assert.All(segments, button => Assert.False(button.Focusable));

            // The slot keeps its size: the strike is a Path inside the same template, not a
            // different control.
            Assert.All(segments, button => Assert.True(button.Bounds.Width > 0 && button.Bounds.Height > 0));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ThePaintLine_IsOnScreen_WhateverTheModeIs(string variantName)
        {
            // Mockup 2f draws this row — caption, "Select all", "Clear" — on a board running WAVE,
            // which ignores paint, beside "the colors are still on file". A row that came and went
            // with the mode would take the paint controls off the very screen the design chose to
            // illustrate them on. The board's ability to paint is settled by the tab existing.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            Assert.NotEmpty(lighting.Modes);

            foreach (var mode in lighting.Modes)
            {
                lighting.SelectModeCommand.Execute(mode);

                host.Capture();

                var texts = VisibleTexts(view);

                Assert.Contains(lighting.Selection.Caption, texts);
                Assert.NotNull(VisibleButton(view, LightingPaintSelection.SelectAllCaption));
                Assert.NotNull(VisibleButton(view, LightingPaintSelection.ClearCaption));
                Assert.NotNull(VisibleButton(view, LightingTabViewModel.ResetAllCaption));
                Assert.Contains(
                    Descendants<Button>(view).Where(button => button.Classes.Contains("zoneButton")),
                    button => button.IsEffectivelyVisible);
            }

            // ...and the row is the same height in a paint-ignoring mode as in a paint-direct one,
            // which is what keeps the board fixed under the pointer while the rail is scrubbed.
            SelectMode(lighting, LightingMode.Wave);

            host.Capture();

            var underWave = BoardTop(view);

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            Assert.Equal(underWave, BoardTop(view));
        }

        [AvaloniaFact]
        public async Task PaintingUnderAPaintIgnoringMode_ReachesTheCapsAtFortyPercent()
        {
            // The other half of the rule, at the glass: the controls being on screen is only worth
            // something if pressing them shows. Painting under Wave lands on the cap, dimmed under
            // the travelling effect rather than replaced by it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Wave);

            var key = lighting.Board!.Keys[0];

            ClickCap(host, view, key, RawInputModifiers.None);

            lighting.Picker.Color = new LedColor(87, 196, 216);
            lighting.AdvancePreview(0.1);

            host.Capture();

            Assert.Equal(1, lighting.Selection.Count);
            Assert.True(key.HasPaintColor);
            Assert.Equal("#57C4D8", key.PaintColorHex);
            Assert.Equal(LightingEffectFrame.PaintOpacityDimmed, key.PaintOpacity);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task UnderWave_APaintedCapCompositesTheBlend_AndAnUnpaintedOneKeepsTheEffect(string variantName)
        {
            // THE ACCEPTANCE CRITERION, read off the board itself: "the paint layer renders at 40%
            // under effects that ignore it". Wave is the mode mockup 2f draws, and it lights every
            // key at intensity 1.0 — so a paint layer composited under it renders at 0%, with every
            // assertion about PaintOpacity still passing. Nothing but the pixels catches that, which
            // is why this test reads them.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Wave);

            var keys = lighting.Board!.Keys;
            var painted = keys[0];

            ClickCap(host, view, painted, RawInputModifiers.None);

            lighting.Picker.Color = new LedColor(0, 0, 255);

            // The selection ring would draw over the cap's own face, and the question here is the
            // face — so the paint is applied and then let go of, exactly as a user would.
            lighting.Selection.Clear();
            lighting.AdvancePreview(0.1);

            Dispatcher.UIThread.RunJobs();

            var unpainted = keys.First(key => !key.HasPaintColor && key.HasEffectColor);

            Assert.True(painted.HasPaintColor);
            Assert.True(painted.HasEffectColor);

            // Both caps at 1.0 under Wave — which is exactly what makes the order load-bearing.
            Assert.Equal(1.0, painted.EffectIntensity);
            Assert.Equal(1.0, unpainted.EffectIntensity);

            var frame = host.Capture();

            // The unpainted cap is the effect, undimmed: the preview must stay true where there is
            // nothing on file to show.
            AssertFace(Parse(unpainted.EffectColorHex!), frame, view, unpainted);

            // The painted one is effect·0.6 + paint·0.4 — the blend mockup 2f describes, with the
            // painted blue present and the wave's own hue still carrying the cap.
            AssertFace(
                Blend(Parse(painted.EffectColorHex!), Parse(painted.PaintColorHex!), painted.PaintOpacity),
                frame,
                view,
                painted);
        }

        [AvaloniaFact]
        public async Task ThePaintLine_CountsTheSelectionItAppliesTo()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Freestyle);

            var keys = lighting.Board!.Keys;

            lighting.SelectKeyCommand.Execute(keys[0]);
            lighting.SelectKeyCommand.Execute(keys[1]);

            host.Capture();

            Assert.Equal(2, lighting.Selection.Count);
            Assert.Contains("Paint · 2 keys selected", VisibleTexts(view));
        }

        [AvaloniaFact]
        public async Task TheModeRail_DrawsEveryModeAsANameOverItsParameterSummary()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rows = Descendants<Button>(view).Where(button => button.Classes.Contains("modeOption")).ToArray();

            Assert.Equal(lighting.Modes.Count, rows.Length);

            var texts = VisibleTexts(view);

            foreach (var mode in lighting.Modes)
            {
                Assert.Contains(mode.Caption, texts);
                Assert.Contains(mode.Summary, texts);
            }

            // The second line is the one the muted role hangs off; a row that stopped classing it
            // would read as two headings.
            var summaries = Descendants<TextBlock>(view)
                .Where(text => text.Classes.Contains("modeSummary"))
                .ToArray();

            Assert.Equal(lighting.Modes.Count, summaries.Length);
        }

        [AvaloniaFact]
        public async Task TheRail_IsTheWideInspectorColumn_AndCarriesTheRailsOwnHeading()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rail = Assert.Single(Descendants<Border>(view), border => border.Classes.Contains("inspectorRail"));
            var expected = (double)DesignTokens.Resolve("WidthInspectorRailWide", ThemeVariant.Dark);

            Assert.Equal(expected, rail.Bounds.Width);
            Assert.Contains(LightingTabViewModel.ModeRailCaption, VisibleTexts(view));
        }

        [AvaloniaFact]
        public async Task TheSpeedControl_IsNineBars_FilledToTheChosenSpeed_WithItsMonoReadout()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Wave);

            lighting.SetSpeedCommand.Execute(6);

            host.Capture();

            var bars = Descendants<Button>(view).Where(button => button.Classes.Contains("speedBar")).ToArray();

            Assert.Equal(LayerLightingState.MaximumSpeed, bars.Length);
            Assert.Equal(6, bars.Count(bar => bar.Classes.Contains("filled")));
            Assert.Contains("Speed 6 / 9", VisibleTexts(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheColourSwatch_ShowsItsHexInMono_AndDisclosesThePickerInsideTheRail(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Monochrome);

            host.Capture();

            var rail = Assert.Single(Descendants<LightingModeRailView>(view));
            var swatch = Assert.Single(
                Descendants<Button>(view),
                button => button.Classes.Contains("colorSlot") && button.IsEffectivelyVisible);

            // A hex is a value the file carries, so it is mono — the one class the mono law grants.
            var hex = Assert.Single(Descendants<TextBlock>(swatch), text => text.Classes.Contains("monoValue"));

            Assert.Equal(lighting.EffectColor.ColorHex, hex.Text);

            // Closed to start with: the rail shows its modes, not a colour wheel. (An undisclosed
            // picker is not merely hidden — an invisible container is never measured, so its
            // template is never applied and the picker is not in the visual tree at all.)
            Assert.False(rail.IsPickerOpen);
            Assert.DoesNotContain(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);

            Click(host, swatch);

            host.Capture();

            Assert.True(rail.IsPickerOpen);
            Assert.Contains(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);
            Assert.True(lighting.EffectColor.IsSelected);

            // ...and it is the mode list it opened in place of, not a scrim over the board.
            Assert.DoesNotContain(
                Descendants<Button>(view).Where(button => button.Classes.Contains("modeOption")),
                button => button.IsEffectivelyVisible);
            Assert.DoesNotContain(Descendants<Border>(view), border => border.Classes.Contains("overlayScrim"));

            var done = VisibleButton(view, LightingModeRailView.ClosePickerCaption);

            Assert.NotNull(done);

            Click(host, done!);

            Assert.False(rail.IsPickerOpen);
            Assert.Contains(
                Descendants<Button>(view).Where(button => button.Classes.Contains("modeOption")),
                button => button.IsEffectivelyVisible);
        }

        [AvaloniaFact]
        public async Task TheFrameTimer_RunsOnTheBudgetsInterval_WhileTheTabIsOnScreen()
        {
            using var scenes = new ViewSceneFactory();

            var view = (LightingTabView)await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.True(view.IsPreviewRunning);

            // The number is the motion budget's, read rather than restated: 33 ms in two places is
            // two places to change it.
            Assert.Equal(
                (TimeSpan)DesignTokens.Resolve("DurationLightingPreviewFrame", ThemeVariant.Dark),
                view.FrameInterval);
        }

        [AvaloniaFact]
        public async Task TheFrameTimer_Ticking_MovesTheBoard()
        {
            // The timer is what makes the picture an animation rather than a still, and only real
            // elapsed time drives a DispatcherTimer — so this waits, rather than forcing a tick.
            using var scenes = new ViewSceneFactory();

            var view = (LightingTabView)await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Spectrum);

            lighting.SetSpeedCommand.Execute(LayerLightingState.MaximumSpeed);

            var before = BoardColours(lighting);

            Assert.NotEmpty(before);

            await Task.Delay(250);

            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(before, BoardColours(lighting));
        }

        [AvaloniaFact]
        public async Task TheFrameTimer_StopsWhenTheSectionHostingItIsHidden_AndComesBackWithIt()
        {
            // The defect this exists for: the editor hosts every tab in a ContentControl whose
            // IsVisible is bound, so switching to Macros does NOT detach this view — it merely stops
            // being drawn, and a board repainting ~76 caps behind another section is pure waste.
            using var scenes = new ViewSceneFactory();

            var view = (LightingTabView)await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var section = new ContentControl { Content = view };

            using var host = ThemedHost.Show(section, ThemeVariant.Dark);

            Assert.True(view.IsPreviewRunning);

            section.IsVisible = false;

            Dispatcher.UIThread.RunJobs();

            Assert.False(view.IsPreviewRunning);

            section.IsVisible = true;

            Dispatcher.UIThread.RunJobs();

            Assert.True(view.IsPreviewRunning);
        }

        [AvaloniaFact]
        public async Task TheFrameTimer_StopsWhenTheViewItselfIsHidden()
        {
            using var scenes = new ViewSceneFactory();

            var view = (LightingTabView)await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.True(view.IsPreviewRunning);

            view.IsVisible = false;

            Dispatcher.UIThread.RunJobs();

            Assert.False(view.IsPreviewRunning);

            view.IsVisible = true;

            Dispatcher.UIThread.RunJobs();

            Assert.True(view.IsPreviewRunning);
        }

        [AvaloniaFact]
        public async Task TheFrameTimer_StopsWhenTheViewLeavesTheTree()
        {
            using var scenes = new ViewSceneFactory();

            var view = (LightingTabView)await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var section = new ContentControl { Content = view };

            using var host = ThemedHost.Show(section, ThemeVariant.Dark);

            Assert.True(view.IsPreviewRunning);

            section.Content = null;

            Dispatcher.UIThread.RunJobs();

            Assert.False(view.IsPreviewRunning);
        }

        [AvaloniaFact]
        public async Task ShiftClickingACap_ExtendsThePaintSelectionOverTheRun()
        {
            // KeyboardView reports one command per click and knows nothing about modifiers, so the
            // extend gesture is the view's own routing — driven here through the real pointer, which
            // is the only thing that proves the tunnel handler beats the cap's own button to it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            var keys = lighting.Board!.Keys;

            ClickCap(host, view, keys[0], RawInputModifiers.None);

            Assert.Equal(1, lighting.Selection.Count);

            ClickCap(host, view, keys[4], RawInputModifiers.Shift);

            // The run between the anchor and the target, inclusive — and the shifted press must not
            // have run the plain toggle as well, which would have taken the target back out again.
            Assert.Equal(5, lighting.Selection.Count);
            Assert.All(keys.Take(5), key => Assert.True(key.IsLightingSelected));
        }

        /// <summary>
        /// Asserts that the cap for <paramref name="key"/> reads <paramref name="expected"/> at the
        /// glass. The mean across the cap's face is what is compared, for the reason
        /// <c>KeycapThemeTests</c> records: the hatch under a translucent layer makes any single
        /// pixel a coin toss, and the mean is what the eye reads anyway.
        /// </summary>
        private static void AssertFace(
            Color expected,
            WriteableBitmap frame,
            Control view,
            KeyboardKeyViewModel key)
        {
            var actual = MeanFace(frame, view, key);
            var distance = Math.Abs(expected.R - actual.R)
                + Math.Abs(expected.G - actual.G)
                + Math.Abs(expected.B - actual.B);

            Assert.True(distance <= 6, $"Position {key.Index} expected about {expected}, painted {actual}.");
        }

        /// <summary>
        /// A run of pixels across one cap's face on the <b>real board</b>, averaged. The board is
        /// drawn through a <c>BoardScaleHost</c>, so the cap's own bounds are in mock units and the
        /// run has to be taken between its two corners <i>translated into the window</i> rather
        /// than off its width. The row is the cap's upper quarter, clear of the caption's own line
        /// and of the rounded corners, for the reason <c>KeycapThemeTests.FaceSamples</c> records.
        /// </summary>
        private static Color MeanFace(WriteableBitmap frame, Control view, KeyboardKeyViewModel key)
        {
            var cap = Descendants<KeyCapView>(view).FirstOrDefault(entry => ReferenceEquals(entry.DataContext, key))
                ?? throw new InvalidOperationException($"The board drew no cap for position {key.Index}.");
            var window = (Visual)cap.GetVisualRoot()!;
            var origin = cap.TranslatePoint(default, window)!.Value;
            var far = cap.TranslatePoint(new Point(cap.Bounds.Width, cap.Bounds.Height), window)!.Value;
            var inset = (far.X - origin.X) / 4;
            var row = (int)(origin.Y + ((far.Y - origin.Y) / 4));
            var samples = new List<Color>();

            for (var x = (int)(origin.X + inset); x < (int)(far.X - inset); x++)
            {
                samples.Add(FramePixels.At(frame, x, row));
            }

            Assert.NotEmpty(samples);

            return Color.FromRgb(
                (byte)Math.Round(samples.Average(sample => (double)sample.R)),
                (byte)Math.Round(samples.Average(sample => (double)sample.G)),
                (byte)Math.Round(samples.Average(sample => (double)sample.B)));
        }

        /// <summary>
        /// <paramref name="over"/> composited over <paramref name="under"/> at
        /// <paramref name="opacity"/> — the arithmetic the two fill layers perform, written out
        /// here so the expectation is not read from the same property the view binds.
        /// </summary>
        private static Color Blend(Color under, Color over, double opacity)
        {
            return Color.FromRgb(
                (byte)Math.Round((over.R * opacity) + (under.R * (1 - opacity))),
                (byte)Math.Round((over.G * opacity) + (under.G * (1 - opacity))),
                (byte)Math.Round((over.B * opacity) + (under.B * (1 - opacity))));
        }

        private static Color Parse(string hex)
        {
            Assert.True(Color.TryParse(hex, out var color), $"'{hex}' is not a colour.");

            return color;
        }

        private static void SelectMode(LightingTabViewModel lighting, LightingMode mode)
        {
            var row = lighting.Modes.FirstOrDefault(entry => entry.Mode == mode)
                ?? throw new InvalidOperationException($"The rail offers no {mode} row.");

            lighting.SelectModeCommand.Execute(row);

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Every colour the preview has pushed onto the shown layer's caps, as one string.</summary>
        private static string BoardColours(LightingTabViewModel lighting)
        {
            var board = lighting.Board
                ?? throw new InvalidOperationException("The lighting scene rendered no board.");

            return string.Join(
                '|',
                board.Keys.Select(key => $"{key.EffectColorHex}:{key.EffectIntensity:F3}"));
        }

        /// <summary>
        /// Where the picture sits inside the tab — the one thing on this screen that must not move
        /// as the rail's selection is scrubbed.
        /// </summary>
        private static Point BoardTop(Control view)
        {
            var board = Descendants<KeyboardView>(view).FirstOrDefault()
                ?? throw new InvalidOperationException("The lighting scene drew no board.");

            return board.TranslatePoint(default, view)
                ?? throw new InvalidOperationException("The board is not in the tab's tree.");
        }

        private static Button[] DirectionSegments(Control view)
        {
            return Descendants<Button>(view)
                .Where(button => button.Classes.Contains("directionSegment"))
                .ToArray();
        }

        private static Button? VisibleButton(Control view, string caption)
        {
            return Descendants<Button>(view)
                .FirstOrDefault(button => button.IsEffectivelyVisible && Equals(button.Content, caption));
        }

        private static IReadOnlyCollection<string> VisibleTexts(Control view)
        {
            return Descendants<TextBlock>(view)
                .Where(text => text.IsEffectivelyVisible && !string.IsNullOrEmpty(text.Text))
                .Select(text => text.Text!)
                .ToHashSet(StringComparer.Ordinal);
        }

        private static IEnumerable<T> Descendants<T>(Visual view) where T : Visual
        {
            return view.GetVisualDescendants().OfType<T>();
        }

        private static void Click(ThemedHost host, Control target, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            var centre = target.TranslatePoint(
                new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
                host.Window) ?? throw new InvalidOperationException("The control is not in the window's tree.");

            host.Window.MouseMove(centre, modifiers);
            host.Window.MouseDown(centre, MouseButton.Left, modifiers);
            host.Window.MouseUp(centre, MouseButton.Left, modifiers);

            Dispatcher.UIThread.RunJobs();
        }

        private static void ClickCap(
            ThemedHost host,
            Control view,
            KeyboardKeyViewModel key,
            RawInputModifiers modifiers)
        {
            var cap = Descendants<KeyCapView>(view).FirstOrDefault(entry => ReferenceEquals(entry.DataContext, key))
                ?? throw new InvalidOperationException($"The board drew no cap for position {key.Index}.");

            Click(host, cap, modifiers);
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
