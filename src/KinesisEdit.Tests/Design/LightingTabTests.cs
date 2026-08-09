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
        public async Task TheRail_DrawsOnlyThePropertiesTheSelectedModeHas()
        {
            // ISSUE #128, ITEM 3b, ASSERTED AS A MATRIX. The user's words: "I want to know that the
            // controls I'm seeing are part of the mode I've selected." So for every mode the device
            // offers, what is on screen has to equal what Core says the mode accepts — the very
            // table LightingModeParametersTests holds Core to, read here off the glass instead.
            //
            // The picker is in the matrix on its own column, AcceptsPaint: it is on screen wherever
            // the per-key colours it paints can reach the file, which is every mode but the two
            // that write nothing — those colours belong to the LAYER rather than to the effect
            // running over them (mockup 2f: "the colors are still on file"), and a layer with no
            // file body has nowhere to keep them. See ThePicker_IsAbsentUnderAModeThatWritesNothing.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.NotEmpty(lighting.Modes);

            foreach (var mode in lighting.Modes)
            {
                lighting.SelectModeCommand.Execute(mode);

                host.Capture();

                var parameters = mode.Parameters;

                Assert.Equal(parameters.AcceptsEffectColor, IsSwatchShown(view, lighting.EffectColor));
                Assert.Equal(parameters.AcceptsBaseColor, IsSwatchShown(view, lighting.BaseColor));
                Assert.Equal(
                    parameters.AcceptsSpeed,
                    Descendants<Button>(view).Any(button => button.Classes.Contains("speedBar") && button.IsEffectivelyVisible));

                // Only the arrows the mode really accepts, and none at all when it has none.
                var arrows = DirectionSegments(view).Where(button => button.IsEffectivelyVisible).ToArray();

                Assert.Equal(parameters.Directions.Count, arrows.Length);
                Assert.DoesNotContain(arrows, button => button.Classes.Contains("unavailable"));

                Assert.Equal(
                    parameters.AcceptsPaint,
                    Descendants<ColorPickerView>(view).Any(picker => picker.IsEffectivelyVisible));
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ThePicker_IsAbsentUnderAModeThatWritesNothing(string variantName)
        {
            // THE DEFECT A CAPTURED FRAME CAUGHT, in the one control issue #128 left ungated. Under
            // Off the rail said "The backlight is off for this layer, and nothing is written to the
            // file for it" and then, directly under it, drew PICK A COLOR, a full colour wheel and
            // "those colours stay on file and show through under the effect". Both halves cannot be
            // true: LightingSectionSerializer returns before its first line for Disable and Pitch
            // Black (specs/07-lighting.md §2.2), so a colour picked there reaches no file and there
            // is no effect for it to show through under.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            // A mode that paints: the picker and its line are both drawn, swatch or no swatch.
            SelectMode(lighting, LightingMode.Reactive);

            host.Capture();

            Assert.True(lighting.Parameters.AcceptsPaint);
            Assert.Contains(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);
            Assert.Contains(LightingModeRailView.PickerLabel, VisibleTexts(view));
            Assert.Contains(lighting.PickerHint, VisibleTexts(view));

            SelectMode(lighting, LightingMode.Disabled);

            host.Capture();

            // ...and under Off the whole section goes: the wheel, its label and its line.
            var texts = VisibleTexts(view);

            Assert.False(lighting.Parameters.AcceptsPaint);
            Assert.DoesNotContain(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);
            Assert.DoesNotContain(LightingModeRailView.PickerLabel, texts);
            Assert.DoesNotContain(LightingHintCatalog.PickerPaintOnlyHint, texts);
            Assert.DoesNotContain(LightingHintCatalog.PickerWithSwatchHint, texts);

            // What is left is the mode itself and the one line that says why there is nothing else:
            // a properties panel with no properties, rather than a promise the file cannot keep.
            Assert.Contains(lighting.ModeHint, texts);
            Assert.DoesNotContain(Descendants<Button>(view), button => button.Classes.Contains("colorSlot") && button.IsEffectivelyVisible);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AModeWithNoDirection_DrawsNoDirectionBlockAtAll(string variantName)
        {
            // THE DELIBERATE REVERSAL OF MOCKUP 2f (issue #128). 2f said, verbatim: "Directions a
            // mode can't use stay in place, struck through — the row never changes shape as you
            // move down the list." That was bought with the scrubable fourteen-row list, where a
            // block growing and shrinking under the pointer would have been worse than a struck
            // arrow. The list is a dropdown now, so the cost is gone and the app's own law applies
            // again: features a mode lacks are not rendered at all rather than disabled.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            // Solid accepts no direction at all — the widest case of the rule.
            SelectMode(lighting, LightingMode.Monochrome);

            host.Capture();

            Assert.False(lighting.Parameters.AcceptsDirection);
            Assert.DoesNotContain(DirectionSegments(view), button => button.IsEffectivelyVisible);
            Assert.DoesNotContain(LightingModeRailView.DirectionLabel, VisibleTexts(view));

            // Rebound offers two of the four, so exactly those two are drawn — and neither is
            // struck, because the struck face has nothing left to say.
            SelectMode(lighting, LightingMode.Rebound);

            host.Capture();

            var arrows = DirectionSegments(view).Where(button => button.IsEffectivelyVisible).ToArray();

            Assert.Equal(2, arrows.Length);
            Assert.All(arrows, button => Assert.True(button.IsHitTestVisible));
            Assert.All(arrows, button => Assert.DoesNotContain("unavailable", button.Classes));
            Assert.Contains(LightingModeRailView.DirectionLabel, VisibleTexts(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task EveryPropertyOnTheRail_CarriesItsOwnLineOfExplanation(string variantName)
        {
            // ISSUE #128, ITEM 4. Inline, not a tooltip: the ask was that the meaning be visible,
            // and Reactive is the case that prompted it — two same-shaped colour rows that nothing
            // on screen told apart.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Reactive);

            host.Capture();

            var texts = VisibleTexts(view);

            Assert.Contains(lighting.ModeHint, texts);
            Assert.Contains(lighting.EffectColor.Hint, texts);
            Assert.Contains(lighting.BaseColor.Hint, texts);
            Assert.Contains(lighting.SpeedHint, texts);
            Assert.Contains(lighting.PickerHint, texts);

            // They are the settings panel's helper-text role, not a fourth voice: `meta muted`.
            var hint = Descendants<TextBlock>(view).First(text => text.Text == lighting.EffectColor.Hint);

            Assert.Contains("meta", hint.Classes);
            Assert.Contains("muted", hint.Classes);
            Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);

            // A mode with a direction shows that line too; Reactive has none, so it must not.
            Assert.DoesNotContain(lighting.DirectionHint, texts);

            SelectMode(lighting, LightingMode.Wave);

            host.Capture();

            Assert.Contains(lighting.DirectionHint, VisibleTexts(view));
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
        public async Task TheModePicker_IsADropdownOfEveryModeTheFirmwareOffers()
        {
            // ISSUE #128, ITEM 3a. The rail was a scrolled column of fourteen buttons taking most
            // of its height; it is one control now, and the modes it offers are unchanged — the
            // device's own menu, LightingAvailability's answer.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var picker = ModePickerOf(view);

            // The device's own menu, whatever that is on this scene's firmware: the counts
            // themselves (14 with the Ripple/Fireball gate open, 12 with it shut) are
            // LightingTabViewModelTests', because they are a rule rather than a rendering.
            Assert.NotEmpty(lighting.Modes);
            Assert.Equal(lighting.Modes.Count, picker.ItemCount);
            Assert.Same(lighting.SelectedModeOption, picker.SelectedItem);
            Assert.Equal(lighting.Modes, picker.ItemsSource);

            // The rail still says what the control is for — 2f's own heading, minus the gesture it
            // named ("click to preview" described a list of rows).
            Assert.Contains(LightingTabViewModel.ModeRailCaption, VisibleTexts(view));

            // The column of buttons is gone, and with it most of the rail's height.
            Assert.DoesNotContain(Descendants<Button>(view), button => button.Classes.Contains("modeOption"));
            Assert.DoesNotContain(Descendants<ItemsControl>(view), items => ReferenceEquals(items.ItemsSource, lighting.Modes) && items is not ComboBox);
        }

        [AvaloniaFact]
        public async Task TheModePicker_DrawsAModeAsItsNameOverItsParameterSummary()
        {
            // The row markup is the list's, verbatim: the mark, the name, and Core's parameter
            // summary underneath — which is what makes the control an answer to "what will change
            // if I pick this one" rather than a list of names.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            SelectMode(lighting, LightingMode.Wave);

            host.Capture();

            var wave = lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave);
            var texts = VisibleTexts(view);

            Assert.Contains(wave.Caption, texts);
            Assert.Contains(wave.Summary, texts);

            // The second line keeps the muted role it had inside the button row. It took it from
            // `Button.modeOption :is(TextBlock).modeSummary`, which cannot reach a ComboBoxItem, so
            // the two generic classes that style spelled are named at the call site instead.
            var summary = Assert.Single(
                Descendants<TextBlock>(view),
                text => text.Classes.Contains("modeSummary") && text.IsEffectivelyVisible);

            Assert.Equal(wave.Summary, summary.Text);
            Assert.Contains("meta", summary.Classes);
            Assert.Contains("muted", summary.Classes);

            // Both marks are in the cell, and exactly one of them draws: an outline mode answers
            // the stroke converter and a solid one the fill, never both.
            var marks = Descendants<Icon>(view).Where(icon => icon.Classes.Contains("modeMark")).ToArray();

            Assert.Equal(2, marks.Length);
            Assert.Single(marks, icon => icon.Data is not null);
        }

        [AvaloniaFact]
        public async Task PickingAMode_WritesThroughSelectModeCommand()
        {
            // The repo's established shape for a selector over a property the view model owns:
            // SelectedItem reads it one way, and the SelectionChanged handler runs the command.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var picker = ModePickerOf(view);
            var loop = lighting.Modes.Single(mode => mode.Mode == LightingMode.Loop);

            picker.SelectedItem = loop;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(LightingMode.Loop, lighting.SelectedMode);
            Assert.Equal(LightingMode.Loop, lighting.SelectedLayer!.State.Mode);
            Assert.Same(loop, picker.SelectedItem);
        }

        // ===== THE RAIL'S COLUMN IS NOT ASSERTED HERE ANY MORE (issue #128) ====================
        // Four tests used to live at this point — that the rail carries no Width of its own, that
        // its ColumnDefinition is bounded by WidthInspectorRailMin/Max, that dragging the seam
        // widens it and stores what the user chose, and that a width arriving from the store
        // reaches the column. Every one of them scanned the LightingTabView scene, and the rail is
        // not in it: the editor shell owns one full-height rail column now and swaps its contents
        // per tab, so there is one column, one seam and one width for both rails rather than two
        // that agree. All four claims are held over the editor, where the column actually is —
        // EditorChromeTests.TheRailsColumn_IsBoundedByTheGeometryTokens,
        // .TheRailsWidth_IsOneNumberForBothTabs (which drives the real seam on the Lighting tab and
        // reads the width back on the Keys tab), .TheRail_RunsTheWholeHeightOfTheBody and
        // .OnASectionWithNoRail_TheColumnAndItsSeam_CollapseToNothing. What is left in this file is
        // what the rail *contains*, which is what this suite is for.


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
        public async Task AZone_SelectsItsKeysAndPaintsThem_WithoutEverTakingASelectedFace(string variantName)
        {
            // A zone button is a user pointing at a named set of keys, so it commits on the spot
            // (issue #128) — the Apply it was split from in #124 is gone.
            //
            // WHAT IT NO LONGER DOES IS LIGHT UP (issue #131). The chip carried the ToggleSegment's
            // accent face while every one of its keys was selected, and the zones overlap, so one
            // chip's press repainted another's face — and after `Select all` they were all lit at
            // once. The selection is read off the caps' rings, which is where it actually is; the
            // row below the board is a row of plain buttons that do things.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            SelectMode(lighting, LightingMode.Freestyle);

            lighting.Picker.Color = new LedColor(0, 0, 255);

            host.Capture();

            var zone = lighting.Zones.First(entry => entry.KeyCodes.Count > 0);
            var button = ZoneButtonFor(view, zone);
            var restingFace = DesignTokens.Resolve("SurfaceBarBrush", variant);

            Assert.DoesNotContain("selected", button.Classes);
            Assert.Equal(restingFace, button.Background);
            Assert.DoesNotContain(lighting.Board!.Keys, key => key.HasPaintColor);

            lighting.SelectZoneCommand.Execute(zone);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // The keys moved and the colour landed...
            Assert.Equal(zone.KeyCodes.Count, lighting.Selection.Count);
            Assert.Equal(zone.KeyCodes.Count, lighting.Board.Keys.Count(key => key.HasPaintColor));
            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(0, 0, 255))),
                lighting.Board.Keys.First(key => key.HasPaintColor).PaintColorHex);

            // ...and the chip is exactly as it was drawn before the press, in both variants.
            Assert.DoesNotContain("selected", button.Classes);
            Assert.Equal(restingFace, button.Background);
            Assert.NotEqual(DesignTokens.Resolve("AccentBrush", variant), button.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task NoZoneChip_ChangesAnotherChipsAppearance_HoweverTheSelectionMoves(string variantName)
        {
            // THE REPORTED SEQUENCE of issue #131, driven through real clicks on the real chips:
            // `Game`, then `WASD`, then `WASD` again. WASD's four keys are all inside Game's
            // twenty-nine, so the derived face made the third click un-light `Game` while
            // twenty-five of its keys stayed selected. Nothing on the row may move but the board.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, variant);

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            var chips = lighting.Zones.Select(zone => ZoneButtonFor(view, zone)).ToArray();
            var game = ZoneButtonFor(view, lighting.Zones.Single(zone => zone.Caption == "Game"));
            var wasd = ZoneButtonFor(view, lighting.Zones.Single(zone => zone.Caption == "WASD"));

            AssertNoChipIsLit(chips, variant);

            Click(host, game);

            Assert.Equal(29, lighting.Selection.Count);

            AssertNoChipIsLit(chips, variant);

            Click(host, wasd);

            // Plain subtraction: four keys out, twenty-five still selected on the board.
            Assert.Equal(25, lighting.Selection.Count);
            Assert.Equal(25, lighting.Board!.Keys.Count(key => key.IsLightingSelected));

            AssertNoChipIsLit(chips, variant);

            // And `Select all`, which used to light every chip at once, lights none of them.
            Click(host, VisibleButton(view, LightingPaintSelection.SelectAllCaption)!);

            Assert.Equal(lighting.Board.Keys.Count, lighting.Selection.Count);

            AssertNoChipIsLit(chips, variant);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task Clear_EmptiesTheSelection_RatherThanDoingNothingVisible(string variantName)
        {
            // ISSUE #131's second half, at the glass and through real input: `Clear` sits beside
            // `Select all` and undoes it. It used to paint the selected keys black, which is why
            // the report read "the Clear button does nothing" — with nothing selected, over
            // unpainted keys, or under a mode drawing paint at 0 %, it changed no pixel and was not
            // even disabled for it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            var clear = VisibleButton(view, LightingPaintSelection.ClearCaption);

            Assert.NotNull(clear);

            // Nothing selected: the button is on screen and refuses, rather than looking live and
            // doing nothing.
            Assert.False(clear!.IsEffectivelyEnabled);

            lighting.Picker.Color = new LedColor(0, 0, 255);

            Click(host, VisibleButton(view, LightingPaintSelection.SelectAllCaption)!);

            Assert.Equal(lighting.Board!.Keys.Count, lighting.Selection.Count);

            host.Capture();

            Assert.True(clear.IsEffectivelyEnabled);

            Click(host, clear);

            host.Capture();

            Assert.Equal(0, lighting.Selection.Count);
            Assert.DoesNotContain(lighting.Board.Keys, key => key.IsLightingSelected);
            Assert.Contains(
                LightingPaintSelection.CaptionPrefix + LightingPaintSelection.EmptyCaptionSuffix,
                VisibleTexts(view));
            Assert.False(clear.IsEffectivelyEnabled);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRail_HasNoApplyButtonLeft(string variantName)
        {
            // ISSUE #128, ITEM 3c. Every control on this rail applies immediately, so the one
            // control whose whole job was "commit, now" had nothing left to say — the flow it
            // existed for (a colour held before the keys were picked) is done by the gesture that
            // picks them. It was the rail's only accent action, so the claim is asserted as "no
            // primaryAction anywhere on the rail" rather than against a caption that is gone.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Freestyle);

            lighting.SelectAllKeysCommand.Execute(null);

            host.Capture();

            // Even with a selection — the state that used to light it up — there is no such button.
            Assert.True(lighting.Selection.HasSelection);
            Assert.DoesNotContain(
                Descendants<Button>(view),
                button => button.Classes.Contains("primaryAction"));

            // The command survives the button: it is still what Clear is built out of.
            Assert.True(lighting.PaintSelectionCommand.CanExecute(null));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task SelectAll_WithAHeldColour_DoesNotRepaintTheLayer_ButACapClickDoes(string variantName)
        {
            // THE TRAP THE MISSING APPLY MUST NOT HAVE BEEN REPLACED BY: painting on every
            // selection change would make `Select all` plus a held colour repaint the whole layer
            // in one click, with nothing but Reset All to undo it — precisely the regression issue
            // #124 removed when ApplyZoneCommand became SelectZoneCommand. Both halves are driven
            // through real pointer input, because the difference between the two gestures is
            // exactly which command the view runs.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            // The colour is chosen FIRST, over an empty selection, so nothing is written yet.
            lighting.Picker.Color = new LedColor(0, 0, 255);

            Assert.DoesNotContain(lighting.Board!.Keys, key => key.HasPaintColor);

            var selectAll = VisibleButton(view, LightingPaintSelection.SelectAllCaption);

            Assert.NotNull(selectAll);

            Click(host, selectAll!);

            Assert.Equal(lighting.Board.Keys.Count, lighting.Selection.Count);
            Assert.DoesNotContain(lighting.Board.Keys, key => key.HasPaintColor);

            // ...while a click on a cap — a user pointing at one key — does commit, which is what
            // makes the missing button a simplification rather than a loss. The selection is let go
            // of through the `Clear` button, which is what that button does since issue #131 and
            // the reason this half of the test can be driven at the glass at all.
            Click(host, VisibleButton(view, LightingPaintSelection.ClearCaption)!);

            Assert.Equal(0, lighting.Selection.Count);
            Assert.DoesNotContain(lighting.Board.Keys, key => key.HasPaintColor);

            Dispatcher.UIThread.RunJobs();

            ClickCap(host, view, lighting.Board.Keys[0], RawInputModifiers.None);

            Assert.Equal(1, lighting.Board.Keys.Count(key => key.HasPaintColor));
            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(0, 0, 255))),
                lighting.Board.Keys[0].PaintColorHex);
        }

        [AvaloniaFact]
        public async Task TheSpeedControl_IsNineBars_FilledToTheChosenSpeed_WithItsMonoReadout()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
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
        public async Task TheColourSwatch_ShowsItsHexInMono_AndTargetsTheAlwaysOpenPicker(string variantName)
        {
            // THE PICKER NO LONGER DISCLOSES (issue #128). It used to open IN PLACE OF the mode
            // list, which was the only part of the rail tall enough to hold it; there is no list to
            // displace, so it is simply always on screen and a swatch is a target rather than a
            // disclosure. A toggle that could only ever hide a control the user had just asked for
            // earned nothing.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Reactive);

            host.Capture();

            // Reactive is the two-swatch case the hints exist for: both rows are drawn, and each
            // shows the colour the file carries in mono.
            var effect = SwatchFor(view, lighting.EffectColor);
            var basis = SwatchFor(view, lighting.BaseColor);

            Assert.NotNull(effect);
            Assert.NotNull(basis);

            // A hex is a value the file carries, so it is mono — the one class the mono law grants.
            var hex = Assert.Single(Descendants<TextBlock>(effect!), text => text.Classes.Contains("monoValue"));

            Assert.Equal(lighting.EffectColor.ColorHex, hex.Text);

            // Open from the first frame, before anything was clicked.
            Assert.Contains(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);
            Assert.True(lighting.EffectColor.IsSelected);

            Click(host, basis!);

            host.Capture();

            // Clicking a swatch re-points the picker; it does not open or close anything, and the
            // mode dropdown above it is untouched.
            Assert.True(lighting.BaseColor.IsSelected);
            Assert.False(lighting.EffectColor.IsSelected);
            Assert.Contains(Descendants<ColorPickerView>(view), picker => picker.IsEffectivelyVisible);
            Assert.True(ModePickerOf(view).IsEffectivelyVisible);
            Assert.DoesNotContain(Descendants<Border>(view), border => border.Classes.Contains("overlayScrim"));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheEffectSwatch_IsCalledPaintColour_WhereTheFileCarriesNoEffectLine(string variantName)
        {
            // specs/07-lighting.md §2.2 writes no effect-colour line for Freestyle: the body is
            // per-key lines, so the swatch is literally "the colour you paint with" (§4). Naming it
            // "Effect Color" there points at a line the file will never carry — which is the same
            // confusion the hints exist to remove, one level up.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingModeRailView).FullName!);
            var lighting = (LightingTabViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            SelectMode(lighting, LightingMode.Freestyle);

            host.Capture();

            Assert.Contains(LightingColorSlotViewModel.PaintColorCaption, VisibleTexts(view));
            Assert.DoesNotContain(LightingColorSlotViewModel.EffectColorCaption, VisibleTexts(view));

            SelectMode(lighting, LightingMode.Monochrome);

            host.Capture();

            Assert.Contains(LightingColorSlotViewModel.EffectColorCaption, VisibleTexts(view));
            Assert.DoesNotContain(LightingColorSlotViewModel.PaintColorCaption, VisibleTexts(view));
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

        /// <summary>
        /// The centred block under the layer switch: the board header, the picture, the paint line
        /// and the zones, as one thing. It is the <see cref="Grid"/> the board hangs off — a Grid
        /// and not a <see cref="StackPanel"/> because issue #123 forbids a vertically-oriented
        /// StackPanel above a <c>BoardScaleHost</c> (one measures its children against infinite
        /// height, so the picture takes its scale from the width alone and overflows the slot).
        /// Centring the block and fitting the board to its row are independent, and this helper
        /// finding a Grid is what says so.
        /// </summary>
        private static Control BoardBlockOf(Control view)
        {
            var board = Descendants<KeyboardView>(view).FirstOrDefault()
                ?? throw new InvalidOperationException("The lighting scene drew no board.");

            return board.GetSelfAndVisualAncestors()
                .OfType<Grid>()
                .First(grid => grid.GetVisualParent() is Grid);
        }

        /// <summary>
        /// Every zone chip is at its ordinary button face — no <c>selected</c> class, no accent fill
        /// and no on-accent label (issue #131). It is asserted as "not the lit face" rather than as
        /// "exactly the resting face" on purpose: a chip the pointer was just clicked on is legally
        /// <c>:pointerover</c>, and hovering is not what this claim is about.
        /// </summary>
        private static void AssertNoChipIsLit(IReadOnlyList<Button> chips, ThemeVariant variant)
        {
            var accentFill = DesignTokens.Resolve("AccentBrush", variant);
            var accentLabel = DesignTokens.Resolve("AccentTextBrush", variant);

            Assert.NotEmpty(chips);
            Assert.All(
                chips,
                chip =>
                {
                    Assert.DoesNotContain("selected", chip.Classes);
                    Assert.NotEqual(accentFill, chip.Background);
                    Assert.NotEqual(accentLabel, chip.Foreground);
                });
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

        /// <summary>
        /// The rail's mode dropdown, found by what it lists rather than by being the only
        /// <see cref="ComboBox"/> around: the colour picker's own <c>ColorView</c> carries parts
        /// this scan would otherwise trip over.
        /// </summary>
        private static ComboBox ModePickerOf(Control view)
        {
            return Assert.Single(
                Descendants<ComboBox>(view),
                box => box.ItemsSource is IEnumerable<LightingModeViewModel>);
        }

        /// <summary>
        /// The rail's button for one colour slot, or null when the mode does not have it. Null
        /// rather than hidden is the honest answer: an invisible container is never measured, so
        /// its template is never applied and the button is not in the visual tree at all.
        /// </summary>
        private static Button? SwatchFor(Control view, LightingColorSlotViewModel slot)
        {
            return Descendants<Button>(view)
                .FirstOrDefault(button => button.Classes.Contains("colorSlot")
                    && ReferenceEquals(button.CommandParameter, slot));
        }

        private static bool IsSwatchShown(Control view, LightingColorSlotViewModel slot)
        {
            return SwatchFor(view, slot) is { IsEffectivelyVisible: true };
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
