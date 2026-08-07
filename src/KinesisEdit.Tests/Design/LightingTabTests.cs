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
using KinesisEdit.Services;
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
        public async Task ShowingTheTab_DoesNotWipeThePaintSelection(string variantName)
        {
            // THE DEFECT THIS PINS, which every view-model test passed straight through: the layer
            // switcher is a ListBox, a ListBox raises SelectionChanged while it BINDS, and the
            // handler ran SelectLayerCommand — which resets the paint selection because moving
            // layer must. So merely showing the tab cleared it, and so did leaving for the Keys tab
            // and coming back, because the tab is hidden rather than unloaded and is re-shown.
            //
            // It has to be asserted through the view: nothing that talks to the view model alone
            // ever raises the event that caused it. A captured frame is what showed it — the paint
            // line read "no keys selected" over a board whose caps had just been cleared.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;
            var board = lighting.Board!;

            lighting.SelectKeyCommand.Execute(board.Keys[0]);
            lighting.SelectKeyCommand.Execute(board.Keys[3]);

            Assert.Equal(2, lighting.Selection.Count);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            Assert.Equal(2, lighting.Selection.Count);
            Assert.Contains(lighting.Selection.Caption, VisibleTexts(view));
            Assert.Equal(2, board.Keys.Count(key => key.IsLightingSelected));

            // And again on a re-show, which is what switching tabs away and back does.
            view.IsVisible = false;
            host.Capture();
            view.IsVisible = true;
            host.Capture();

            Assert.Equal(2, lighting.Selection.Count);
        }

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

            // The cap wears the SOFTENED colour, not the stored one (issue #124) — and the
            // expectation is computed rather than written out, so a re-tune of the tint constants
            // moves this test with the app instead of failing it.
            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(87, 196, 216))),
                key.PaintColorHex);
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
        public async Task TheRail_IsTheEditorsOwnBoundedColumn_AndCarriesTheRailsOwnHeading()
        {
            // It used to assert `WidthInspectorRailWide`, 300, set on the rail's own Border. Issue
            // #124 moved the width onto the grid COLUMN — a GridSplitter resizes a column, and a
            // local Width would have outranked it — and pointed that column at the very
            // InspectorRailWidthViewModel the Keys tab's rail reads. So the rail opens at the
            // user's stored width, inside the same band the key inspector's column carries.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rail = Assert.Single(Descendants<Border>(view), border => border.Classes.Contains("inspectorRail"));

            Assert.True(
                double.IsNaN(rail.Width),
                "The mode rail still carries a Width of its own; the column owns it now.");
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Bounds.Width);
            Assert.Contains(LightingTabViewModel.ModeRailCaption, VisibleTexts(view));
        }

        [AvaloniaFact]
        public async Task TheRailsColumn_IsBoundedByTheSameGeometryTokensAsTheKeyInspectors()
        {
            // The drag's own band, on this tab too: the column carries it, so the seam cannot be
            // pulled outside it in the first place, and the view model clamps the stored number to
            // the same two.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var column = RailColumnOf(view);

            Assert.Equal((double)DesignTokens.Resolve("WidthInspectorRailMin", ThemeVariant.Dark), column.MinWidth);
            Assert.Equal((double)DesignTokens.Resolve("WidthInspectorRailMax", ThemeVariant.Dark), column.MaxWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, column.Width.Value);
            Assert.True(
                column.Width.IsAbsolute,
                "The mode rail's column is not a fixed width; the splitter has nothing to resize.");
        }

        [AvaloniaFact]
        public async Task DraggingTheLightingSeam_WidensTheRailAndStoresWhatTheUserChose()
        {
            // The whole mechanism end to end, through Avalonia's own input pipeline — a real press
            // on the seam, a real move, a real release — exactly as EditorChromeTests drives the
            // Keys tab's. Nothing here pokes the view model, because the failure this catches is a
            // splitter resizing a column the rail does not live in, which every property-level
            // assertion would miss.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var rail = Assert.Single(Descendants<Border>(view), border => border.Classes.Contains("inspectorRail"));
            var seam = Assert.Single(Descendants<GridSplitter>(view));

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, rail.Bounds.Width);

            const double pull = 60;

            var grip = seam.TranslatePoint(new Point(seam.Bounds.Width / 2, seam.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The seam is not in the window's visual tree.");

            host.Window.MouseMove(grip);
            host.Window.MouseDown(grip, MouseButton.Left);
            host.Window.MouseMove(grip - new Point(pull, 0), RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(grip - new Point(pull, 0), MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // Left widens it: the rail is the right-hand column, so the seam moving left gives it
            // room and the board's star column gives the room up.
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, lighting.Rail.Width);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth + pull, rail.Bounds.Width);
        }

        [AvaloniaFact]
        public async Task AStoredWidth_ReachesTheModeRailsColumn()
        {
            // The other direction, and the half a drag cannot prove: a width that arrives from the
            // preference store — or from a drag on the OTHER tab's seam — has to land on this
            // column, or the two rails would only agree until one of them was moved.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            lighting.Rail.Width = 420;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(420, RailBorderOf(view).Bounds.Width);

            // ...and the macro panel's 300 px floor is NOT inherited here. The Keys tab's rail is
            // entitled to it; this tab has no macro panel, so a narrow rail stays narrow.
            lighting.Rail.Width = HostPreferences.MinimumInspectorRailWidth;
            lighting.Rail.IsWide = true;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(HostPreferences.MinimumInspectorRailWidth, RailBorderOf(view).Bounds.Width);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheBoardBlock_IsCentredInItsColumn_LikeTheKeysBoard(string variantName)
        {
            // Issue #124, and it reverses a comment this file's own view used to carry: the block
            // was anchored to the top of its column because the paint line came and went with the
            // mode and slid a centred board ~70 px. That row is on screen in every mode now, so the
            // cause is gone and the anchoring was outliving it. The measurement is off the glass —
            // the gap above the block against the gap below it — because a VerticalAlignment that
            // resolves and never reaches the arrange pass looks identical to a property test.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var block = BoardBlockOf(view);
            var column = (Grid)block.GetVisualParent()!;
            var top = block.TranslatePoint(default, column)!.Value.Y;
            var above = top - column.RowDefinitions[0].ActualHeight;
            var below = column.Bounds.Height - (top + block.Bounds.Height);

            Assert.True(above > 1, $"The board block sits {above} from the top of its row; it is not centred.");
            Assert.True(
                Math.Abs(above - below) <= 1,
                $"The board block has {above} above it and {below} below it; it is not centred.");

            // ...and nothing under it overlaps it: the paint line and the zones are the last two
            // children of the very block that was measured, so a collision would be a negative gap.
            Assert.True(below >= 0, $"The board block overflows its row by {-below}.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AZone_SelectsItsKeysAndShowsItsSelectedFace_WithoutPaintingAnything(string variantName)
        {
            // Issue #124 split one gesture in two. A zone used to paint on the spot, which left no
            // way to say "these keys" without also committing a colour to them; it selects now, and
            // Apply commits. The button therefore needs a face — it had none at all, because
            // `Button.zoneButton` carried padding, a margin and a cursor and no ControlTheme.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            var zone = lighting.Zones.First(entry => entry.KeyCodes.Count > 0);
            var button = ZoneButtonFor(view, zone);

            Assert.DoesNotContain("selected", button.Classes);

            lighting.SelectZoneCommand.Execute(zone);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(zone.IsSelected);
            Assert.Contains("selected", button.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), button.Background);

            // The other half of the split, and the one a face cannot show: the keys are selected and
            // NOTHING on the layer was painted.
            Assert.Equal(zone.KeyCodes.Count, lighting.Selection.Count);
            Assert.DoesNotContain(lighting.Board!.Keys, key => key.HasPaintColor);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task Apply_SitsInTheRailFooter_IsGatedOnTheSelection_AndPaintsIt(string variantName)
        {
            // The commit of the select-then-apply flow (issue #124). It is the one control on this
            // screen that can paint a selection made AFTER the colour was chosen — the picker paints
            // live, but only what was selected while it was being dragged.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            // It sits WITH Color / Speed / Direction — searched from the footer down rather than
            // from the screen down, so a button that landed anywhere else would not be found.
            var footer = Assert.Single(
                Descendants<Border>(view),
                border => border.Classes.Contains("inspectorFooter"));
            var apply = Assert.Single(
                Descendants<Button>(footer),
                button => Equals(button.Content, LightingModeRailView.ApplyCaption));

            // Nothing selected: the command cannot run, so the button is dead. No visibility logic
            // decides that — CanExecute does.
            Assert.False(lighting.Selection.HasSelection);
            Assert.False(apply.IsEffectivelyEnabled);

            // The colour is chosen FIRST, over an empty selection, so the live paint writes nothing.
            lighting.Picker.Color = new LedColor(0, 0, 255);

            Assert.DoesNotContain(lighting.Board!.Keys, key => key.HasPaintColor);

            var zone = lighting.Zones.First(entry => entry.KeyCodes.Count > 0);

            lighting.SelectZoneCommand.Execute(zone);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(apply.IsEffectivelyEnabled);
            Assert.DoesNotContain(lighting.Board.Keys, key => key.HasPaintColor);

            Click(host, apply);

            Assert.Equal(zone.KeyCodes.Count, lighting.Board.Keys.Count(key => key.HasPaintColor));
            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(0, 0, 255))),
                lighting.Board.Keys.First(key => key.HasPaintColor).PaintColorHex);
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

        /// <summary>The mode rail's own frame.</summary>
        private static Border RailBorderOf(Control view)
        {
            return Assert.Single(Descendants<Border>(view), border => border.Classes.Contains("inspectorRail"));
        }

        /// <summary>
        /// The rail's <see cref="ColumnDefinition"/> — where its width lives since issue #124,
        /// reached through the seam exactly as <c>EditorChromeTests</c> reaches the Keys tab's.
        /// </summary>
        private static ColumnDefinition RailColumnOf(Control view)
        {
            var seam = Assert.Single(Descendants<GridSplitter>(view));
            var grid = (Grid)seam.GetVisualParent()!;

            return grid.ColumnDefinitions[Grid.GetColumn(RailBorderOf(view).FindAncestorOfType<LightingModeRailView>()!)];
        }

        /// <summary>
        /// The centred block under the layer switch: the board header, the picture, the paint line
        /// and the zones, as one thing. It is the <see cref="StackPanel"/> the board hangs off.
        /// </summary>
        private static Control BoardBlockOf(Control view)
        {
            var board = Descendants<KeyboardView>(view).FirstOrDefault()
                ?? throw new InvalidOperationException("The lighting scene drew no board.");

            return board.GetSelfAndVisualAncestors()
                .OfType<StackPanel>()
                .First(panel => panel.GetVisualParent() is Grid);
        }

        /// <summary>The chip drawn for one zone of the paint row.</summary>
        private static Button ZoneButtonFor(Control view, LightingZoneViewModel zone)
        {
            return Descendants<Button>(view)
                .Where(button => button.Classes.Contains("zoneButton"))
                .FirstOrDefault(button => ReferenceEquals(button.DataContext, zone))
                ?? throw new InvalidOperationException($"The paint row drew no chip for '{zone.Caption}'.");
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
