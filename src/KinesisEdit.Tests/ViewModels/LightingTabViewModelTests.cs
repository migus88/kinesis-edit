using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Lighting tab of specs/07-lighting.md §3/§4 as design mockup 2f redraws it. Everything
    /// asserted here is a rule the panel reads off Core — mode membership, the per-mode parameter
    /// set, the firmware gates, the zone key sets, the black-clears-the-key contract, the sampled
    /// preview frame — so a Core change that moved one of them shows up as a failure here rather
    /// than as a silently wrong editor.
    /// </summary>
    public sealed class LightingTabViewModelTests
    {
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeAppPreferencesStore _preferences = new();

        [AvaloniaFact]
        public void IsSupported_ForTheFreestyleEdgeRgb_IsTrueAndForEveryOtherBoardFalse()
        {
            // Per-key RGB without an edge strip is exactly the board whose led file is the plain
            // two-layer key-backlight model this panel edits.
            Assert.True(IsSupported(DeviceId.FreestyleEdgeRgb));
            Assert.False(IsSupported(DeviceId.Tko));
            Assert.False(IsSupported(DeviceId.Advantage360));
            Assert.False(IsSupported(DeviceId.FreestylePro));
            Assert.False(IsSupported(DeviceId.Advantage2));
            Assert.False(IsSupported(DeviceId.FreestyleEdge));
        }

        [AvaloniaFact]
        public void Modes_ForCurrentFirmware_AreTheThirteenEffectsThenOff()
        {
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.121", ledFirmware: "1.0.58"));

            Assert.Equal(14, tab.Modes.Count);
            Assert.Equal(LightingMode.Disabled, tab.Modes[^1].Mode);
            Assert.Equal(LightingModeCaptions.OffCaption, tab.Modes[^1].Caption);
            Assert.DoesNotContain(LightingMode.PitchBlack, tab.Modes.Select(mode => mode.Mode));
            Assert.DoesNotContain(LightingMode.FrozenWave, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));

            // Mockup 2f omits Freestyle; §3's RGB menu offers it, and the catalog wins membership.
            Assert.Contains(LightingMode.Freestyle, tab.Modes.Select(mode => mode.Mode));
        }

        [AvaloniaFact]
        public void Modes_BelowTheRippleAndFireballGate_OmitThoseTwo()
        {
            // specs/07-lighting.md §3: KBD ≥ 1.0.121 and LED ≥ 1.0.58.
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.120", ledFirmware: "1.0.58"));

            Assert.Equal(12, tab.Modes.Count);
            Assert.DoesNotContain(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.DoesNotContain(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));
        }

        [AvaloniaFact]
        public void Modes_InDemoMode_AreAllOffered()
        {
            var tab = CreateAttachedTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            Assert.Contains(LightingMode.Ripple, tab.Modes.Select(mode => mode.Mode));
            Assert.Contains(LightingMode.Fireball, tab.Modes.Select(mode => mode.Mode));
        }

        [AvaloniaFact]
        public void Modes_CarryTheDesignsCaptionsAndCoresSummaries()
        {
            var tab = CreateAttachedTab();

            // The three the design renames — and nothing else in the app spells a mode name.
            Assert.Equal(
                LightingModeCaptions.SolidCaption,
                tab.Modes.Single(mode => mode.Mode == LightingMode.Monochrome).Caption);
            Assert.Equal(
                LightingModeCaptions.OffCaption,
                tab.Modes.Single(mode => mode.Mode == LightingMode.Disabled).Caption);
            Assert.Equal(LightingModeCaptions.PitchBlackCaption, LightingModeCaptions.For(LightingMode.PitchBlack));

            // Every other caption is still the catalog's §3 display name.
            Assert.Equal(
                LightingModeCatalog.Find(LightingMode.Wave).DisplayName,
                tab.Modes.Single(mode => mode.Mode == LightingMode.Wave).Caption);

            // And every row's second line is Core's, never the app's reading of the §3 table.
            Assert.All(
                tab.Modes,
                mode => Assert.Equal(
                    LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode.Mode, true).Summary,
                    mode.Summary));
        }

        [AvaloniaFact]
        public void SelectedModeOption_IsTheRowTheDropdownShows_AndFollowsTheLayer()
        {
            // The mode rail is a ComboBox since issue #128, and a selector needs an ITEM rather
            // than the enum SelectedMode carries. The two can never disagree: both are written in
            // the same two places (ReadFromState and SelectMode).
            var tab = CreateAttachedTab();

            Assert.NotNull(tab.SelectedModeOption);
            Assert.Equal(tab.SelectedMode, tab.SelectedModeOption!.Mode);

            SelectMode(tab, LightingMode.Rebound);

            Assert.Same(tab.Modes.Single(mode => mode.Mode == LightingMode.Rebound), tab.SelectedModeOption);

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Equal(LightingMode.Disabled, tab.SelectedModeOption!.Mode);
        }

        [AvaloniaFact]
        public void SelectModeCommand_ForTheModeAlreadyOpen_WritesNothingAndAnnouncesNothing()
        {
            // A ComboBox raises SelectionChanged while it BINDS, so this command runs whenever the
            // tab is shown — not only when the user picks a mode. Without the identity guard,
            // merely opening the Lighting tab announced a write and turned the editor's Save amber
            // over a profile nobody had edited.
            var tab = CreateAttachedTab();
            var changes = 0;

            SelectMode(tab, LightingMode.Wave);

            tab.ModelChanged += (_, _) => changes++;

            SelectMode(tab, LightingMode.Wave);

            Assert.Equal(0, changes);
            Assert.Equal(LightingMode.Wave, tab.SelectedMode);
        }

        [AvaloniaFact]
        public void ModeWithNoRowOnThisFirmware_LeavesTheDropdownEmptyButStillNamesItself()
        {
            // A led file may carry a mode the device's own menu does not offer — Ripple below the
            // KBD 1.0.121 / LED 1.0.58 gate (§3). The dropdown then has nothing to select, and its
            // placeholder is ModeCaption, so the layer still says what it is set to.
            var lighting = new LightingModel();

            lighting.TopLayer.Mode = LightingMode.Ripple;

            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.120"), lighting);

            Assert.Equal(LightingMode.Ripple, tab.SelectedMode);
            Assert.Null(tab.SelectedModeOption);
            Assert.Equal(LightingModeCaptions.For(LightingMode.Ripple), tab.ModeCaption);
        }

        [AvaloniaTheory]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        [InlineData(LightingMode.Starlight)]
        [InlineData(LightingMode.Rebound)]
        [InlineData(LightingMode.Loop)]
        [InlineData(LightingMode.Rain)]
        [InlineData(LightingMode.Monochrome)]
        [InlineData(LightingMode.Freestyle)]
        [InlineData(LightingMode.Breathe)]
        [InlineData(LightingMode.Wave)]
        [InlineData(LightingMode.Spectrum)]
        [InlineData(LightingMode.Pulse)]
        [InlineData(LightingMode.Disabled)]
        public void EveryRenderedProperty_CarriesAHintForTheSelectedMode(LightingMode mode)
        {
            // Issue #128, item 4: every property the rail draws explains itself in place. The hints
            // are per MODE and per PROPERTY — the question that prompted them was "what is the
            // difference between Effect and Base color in reactive mode", which one sentence per
            // property could not have answered.
            var tab = CreateAttachedTab();

            SelectMode(tab, mode);

            Assert.NotEmpty(tab.ModeHint);
            Assert.Equal(LightingHintCatalog.ForMode(mode), tab.ModeHint);

            if (tab.Parameters.AcceptsEffectColor)
            {
                Assert.NotEmpty(tab.EffectColor.Hint);
            }

            if (tab.Parameters.AcceptsBaseColor)
            {
                Assert.NotEmpty(tab.BaseColor.Hint);
                Assert.NotEqual(tab.EffectColor.Hint, tab.BaseColor.Hint);
            }

            if (tab.Parameters.AcceptsSpeed)
            {
                Assert.NotEmpty(tab.SpeedHint);
            }

            if (tab.Parameters.AcceptsDirection)
            {
                Assert.NotEmpty(tab.DirectionHint);
            }

            // The picker is on screen in every mode that has a colour of some kind (issue #135
            // moved that gate off AcceptsPaint), and its line has two forms: it names the board
            // only where the board can actually be painted.
            if (tab.Parameters.AcceptsAnyColor)
            {
                Assert.Equal(
                    tab.CanSelectKeys
                        ? LightingHintCatalog.PickerWithSwatchHint
                        : LightingHintCatalog.PickerSwatchOnlyHint,
                    tab.PickerHint);
            }
        }

        [AvaloniaFact]
        public void TheReactiveHints_SayWhichColourIsTheFlashAndWhichIsTheRest()
        {
            // The literal question issue #128 was raised over, and the answer specs/07-lighting.md
            // §2.2 gives: Reactive writes `[reactive]>[R][G][B][spdN]` over a `[mono]>[R][G][B]`
            // base line, and §3 says it "lights keys on key-press over the base color".
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Reactive);

            Assert.Contains("strike", tab.EffectColor.Hint, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rest", tab.BaseColor.Hint, StringComparison.OrdinalIgnoreCase);
        }

        [AvaloniaTheory]
        [InlineData(LightingMode.Freestyle, LightingColorSlotViewModel.PaintColorCaption)]
        [InlineData(LightingMode.Breathe, LightingColorSlotViewModel.PaintColorCaption)]
        [InlineData(LightingMode.Monochrome, LightingColorSlotViewModel.EffectColorCaption)]
        [InlineData(LightingMode.Reactive, LightingColorSlotViewModel.EffectColorCaption)]
        [InlineData(LightingMode.Loop, LightingColorSlotViewModel.EffectColorCaption)]
        public void TheEffectSwatch_IsCalledPaintColourWhereTheFileCarriesNoEffectLine(
            LightingMode mode,
            string caption)
        {
            // specs/07-lighting.md §2.2 writes no effect-colour line for the per-key modes: their
            // body is per-key lines, so the swatch there is literally "the colour you paint with"
            // (§4) and "Effect Color" would name a line the file will never carry. The verdict is
            // the catalog's WritesEffectColor flag, never a list restated in the app.
            var tab = CreateAttachedTab();

            SelectMode(tab, mode);

            Assert.Equal(caption, tab.EffectColor.Caption);
            Assert.Equal(LightingColorSlotViewModel.BaseColorCaption, tab.BaseColor.Caption);
        }

        [AvaloniaTheory]
        [InlineData(LightingMode.Disabled, false, false, false, false, false, false)]
        [InlineData(LightingMode.Freestyle, true, false, false, false, true, true)]
        [InlineData(LightingMode.Monochrome, true, false, false, false, false, true)]
        [InlineData(LightingMode.Breathe, true, false, true, false, true, true)]
        [InlineData(LightingMode.Spectrum, false, false, true, false, false, true)]
        [InlineData(LightingMode.Wave, false, false, true, true, false, true)]
        [InlineData(LightingMode.Reactive, true, true, true, false, false, true)]
        [InlineData(LightingMode.Ripple, true, true, true, false, false, true)]
        [InlineData(LightingMode.Fireball, true, true, true, false, false, true)]
        [InlineData(LightingMode.Starlight, true, true, true, false, false, true)]
        [InlineData(LightingMode.Rebound, true, true, true, true, false, true)]
        [InlineData(LightingMode.Loop, true, true, true, true, false, true)]
        [InlineData(LightingMode.Pulse, false, false, true, false, false, true)]
        [InlineData(LightingMode.Rain, true, true, true, false, false, true)]
        public void Parameters_ForEachMode_MatchTheSpecTable(
            LightingMode mode,
            bool acceptsEffectColor,
            bool acceptsBaseColor,
            bool acceptsSpeed,
            bool acceptsDirection,
            bool hasPerKeyColors,
            bool acceptsPaint)
        {
            // The §3 "Which parameter panels each mode shows" table, with the Fireball row's
            // "no direction UI on RGB" already applied — asked of the tab, so that this asserts the
            // panel routes through Core rather than that Core is right about itself.
            var tab = CreateAttachedTab();

            // One key selected to start from, taken in a per-key mode — which since issue #135 is
            // the only kind of mode a selection can be taken in. What the assertions below turn on
            // is whether the switch to `mode` keeps that selection or drops it, and the answer is
            // exactly HasPerKeyColors.
            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectKeyCommand.Execute(tab.Board!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(1, tab.Selection.Count);

            SelectMode(tab, mode);

            Assert.Equal(acceptsEffectColor, tab.Parameters.AcceptsEffectColor);
            Assert.Equal(acceptsBaseColor, tab.Parameters.AcceptsBaseColor);
            Assert.Equal(acceptsSpeed, tab.Parameters.AcceptsSpeed);
            Assert.Equal(acceptsDirection, tab.Parameters.AcceptsDirection);
            Assert.Equal(hasPerKeyColors, tab.Parameters.HasPerKeyColors);
            Assert.Equal(acceptsEffectColor, tab.EffectColor.IsVisible);
            Assert.Equal(acceptsBaseColor, tab.BaseColor.IsVisible);

            // The colour picker's own gate, and the one that is not about the effect at all: Off
            // writes no file body (§2.2), so a colour picked in it reaches nothing — every other
            // mode here can hold paint whether or not it has a swatch.
            Assert.Equal(acceptsPaint, tab.Parameters.AcceptsPaint);

            // ...and the selection controls ARE among the things the mode decides (issue #135).
            // A mode whose file body is not per-key colour lines has no selection to offer, so the
            // one taken above is gone and every command that grows or holds one is unavailable.
            // This reversed the old rule, which was that none of them was gated on the mode.
            Assert.Equal(hasPerKeyColors, tab.CanSelectKeys);
            Assert.Equal(hasPerKeyColors, tab.Selection.HasSelection);
            Assert.Equal(hasPerKeyColors, tab.SelectZoneCommand.CanExecute(tab.Zones[0]));
            Assert.Equal(hasPerKeyColors, tab.SelectAllKeysCommand.CanExecute(null));
            Assert.Equal(hasPerKeyColors, tab.PaintSelectionCommand.CanExecute(null));
            Assert.Equal(hasPerKeyColors, tab.ClearSelectionCommand.CanExecute(null));

            // `Reset All` is deliberately NOT gated with them: it erases the per-key colours the
            // layer carries on file, which exist and are worth erasing whatever effect is running
            // over them. It is a file action, not a selection one.
            Assert.True(tab.ResetAllCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void Parameters_BelowTheLayerCustomizationGate_HideTheBaseColour()
        {
            var tab = CreateAttachedTab(CreateSnapshot(ledFirmware: "1.0.43"));

            SelectMode(tab, LightingMode.Reactive);

            Assert.True(tab.Parameters.AcceptsEffectColor);
            Assert.False(tab.Parameters.AcceptsBaseColor);
            Assert.False(tab.BaseColor.IsVisible);
        }

        [AvaloniaFact]
        public void Directions_AreAlwaysTheFourArrows_WithTheModesOwnMarkedAvailable()
        {
            // The LIST is still four long — one entry per LightingDirection, each carrying Core's
            // verdict — and it is what SelectDirectionCommand validates against. Since issue #128
            // the rail DRAWS only the available ones (see LightingTabTests); the two are separate
            // claims on purpose, because a write must be refused whether or not a control was ever
            // rendered for it.
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);

            Assert.Equal(
                new[] { "Down", "Left", "Up", "Right" },
                tab.Directions.Select(entry => entry.Caption));
            Assert.All(tab.Directions, entry => Assert.True(entry.IsAvailable));

            SelectMode(tab, LightingMode.Loop);

            Assert.Equal(4, tab.Directions.Count);
            Assert.All(tab.Directions, entry => Assert.True(entry.IsAvailable));
        }

        [AvaloniaFact]
        public void Directions_ForRebound_KeepFourEntriesAndRelabelTheTwoItOffers()
        {
            // Only the two it offers are renamed: calling an unusable arrow "Horizontal" would say
            // the mode has two horizontals. The other two are never drawn (issue #128), so their
            // captions only ever reach a tooltip that is not shown.
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Rebound);

            Assert.Equal(4, tab.Directions.Count);
            Assert.Equal(
                new[]
                {
                    LightingDirection.Down,
                    LightingDirection.Left,
                    LightingDirection.Up,
                    LightingDirection.Right
                },
                tab.Directions.Select(entry => entry.Direction));
            Assert.Equal(
                new[] { "Down", LightingDirectionViewModel.HorizontalCaption, LightingDirectionViewModel.VerticalCaption, "Right" },
                tab.Directions.Select(entry => entry.Caption));
            Assert.Equal(
                new[] { false, true, true, false },
                tab.Directions.Select(entry => entry.IsAvailable));
        }

        [AvaloniaFact]
        public void Directions_ForFireballOnTheRgb_AreAllFourAndNoneAvailable()
        {
            // Fireball carries a direction token in the file but has no direction panel on the RGB
            // (§3), so all four entries exist and none is available — which is what makes
            // AcceptsDirection false and takes the whole block off the rail (issue #128).
            var tab = CreateAttachedTab(CreateSnapshot(keyboardFirmware: "1.0.121", ledFirmware: "1.0.58"));

            SelectMode(tab, LightingMode.Fireball);

            Assert.Equal(4, tab.Directions.Count);
            Assert.All(tab.Directions, entry => Assert.False(entry.IsAvailable));
            Assert.False(tab.Parameters.AcceptsDirection);
        }

        [AvaloniaFact]
        public void SelectDirectionCommand_WritesThroughToTheLayer()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Wave);
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right));

            Assert.Equal(LightingDirection.Right, lighting.TopLayer.Direction);
            Assert.True(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right).IsSelected);
        }

        [AvaloniaFact]
        public void SelectDirectionCommand_ForAnArrowTheModeCannotUse_IsANoOp()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Rebound);

            var struckThrough = tab.Directions.Single(entry => entry.Direction == LightingDirection.Right);
            var changes = 0;

            tab.ModelChanged += (_, _) => changes++;
            tab.SelectDirectionCommand.Execute(struckThrough);

            Assert.Equal(LightingDirection.Left, lighting.TopLayer.Direction);
            Assert.False(struckThrough.IsSelected);
            Assert.Equal(0, changes);
        }

        [AvaloniaFact]
        public void SelectMode_WithADirectionTheModeRejects_NormalisesItToTheDefault()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.Direction = LightingDirection.Right;

            var tab = CreateAttachedTab(lighting: lighting);

            // Rebound accepts left/up only (§2.4 item 5); the file would be written as left, so
            // the control and the model agree on it up front.
            SelectMode(tab, LightingMode.Rebound);

            Assert.Equal(LightingDirection.Left, lighting.TopLayer.Direction);
        }

        [AvaloniaFact]
        public void Speed_IsClampedAndWrittenThrough()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Breathe);

            tab.Speed = 7;

            Assert.Equal(7, lighting.TopLayer.Speed);

            tab.Speed = 42;

            Assert.Equal(LayerLightingState.MaximumSpeed, tab.Speed);
            Assert.Equal(LayerLightingState.MaximumSpeed, lighting.TopLayer.Speed);
        }

        [AvaloniaFact]
        public void SpeedControl_IsNineBarsFilledToTheChosenSpeed_WithAMonoReadout()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);

            Assert.Equal(9, tab.SpeedControl.Segments.Count);
            Assert.Equal(
                Enumerable.Range(LayerLightingState.MinimumSpeed, 9),
                tab.SpeedControl.Segments.Select(segment => segment.Speed));

            tab.SetSpeedCommand.Execute(6);

            Assert.Equal(6, tab.Speed);
            Assert.Equal("Speed 6 / 9", tab.SpeedControl.Readout);
            Assert.Equal(6, tab.SpeedControl.Segments.Count(segment => segment.IsFilled));

            // The bars are the same clamp as the property, because they are the same property.
            tab.SetSpeedCommand.Execute(99);

            Assert.Equal(LayerLightingState.MaximumSpeed, tab.SpeedControl.Speed);
            Assert.Equal("Speed 9 / 9", tab.SpeedControl.Readout);
        }

        [AvaloniaFact]
        public void SpeedControl_InAModeWithoutASpeed_IsUnavailable()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Monochrome);

            Assert.False(tab.SetSpeedCommand.CanExecute(4));
        }

        [AvaloniaFact]
        public void Picker_WhileTheEffectSwatchIsSelected_WritesTheEffectColour()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Monochrome);
            tab.Picker.Color = new LedColor(255, 128, 0);

            Assert.Equal(new LedColor(255, 128, 0), lighting.TopLayer.EffectColor);
            Assert.Equal("#FF8000", tab.EffectColor.ColorHex);
        }

        [AvaloniaFact]
        public void SelectColorSlotCommand_ForTheBaseSwatch_PointsThePickerAtIt()
        {
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Reactive);

            Assert.True(tab.EffectColor.IsSelected);

            tab.SelectColorSlotCommand.Execute(tab.BaseColor);

            Assert.True(tab.BaseColor.IsSelected);
            Assert.False(tab.EffectColor.IsSelected);

            tab.Picker.Color = new LedColor(10, 20, 30);

            Assert.Equal(new LedColor(10, 20, 30), lighting.TopLayer.BaseColor);
            Assert.Equal(LedColor.DefaultEffectColor, lighting.TopLayer.EffectColor);
        }

        [AvaloniaFact]
        public void SelectColorSlotCommand_ForAHiddenSwatch_IsRefused()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Monochrome);

            tab.SelectColorSlotCommand.Execute(tab.BaseColor);

            Assert.False(tab.BaseColor.IsSelected);
            Assert.True(tab.EffectColor.IsSelected);
        }

        [AvaloniaFact]
        public void SelectLayerCommand_ForTheFnLayer_ReReadsEveryControlFromIt()
        {
            // The two layers are fully independent (specs/07-lighting.md §4).
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(CreateSnapshot(), lighting);

            SelectMode(tab, LightingMode.Wave);
            tab.Speed = 3;
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Up));

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Same(tab.Layers[1], tab.SelectedLayer);
            Assert.Equal(LightingMode.Disabled, tab.SelectedMode);
            Assert.Equal(LayerLightingState.DefaultSpeed, tab.Speed);
            Assert.False(tab.Parameters.AcceptsDirection);

            SelectMode(tab, LightingMode.Monochrome);
            tab.Picker.Color = new LedColor(1, 2, 3);

            Assert.Equal(new LedColor(1, 2, 3), lighting.FnLayer.EffectColor);
            Assert.Equal(LightingMode.Wave, lighting.TopLayer.Mode);
            Assert.Equal(3, lighting.TopLayer.Speed);
            Assert.Equal(LightingDirection.Up, lighting.TopLayer.Direction);

            tab.SelectLayerCommand.Execute(tab.Layers[0]);

            Assert.Equal(LightingMode.Wave, tab.SelectedMode);
            Assert.Equal(3, tab.Speed);
        }

        [AvaloniaFact]
        public void Layers_BelowTheLayerCustomizationGate_LeaveTheFnLayerUnreachable()
        {
            var tab = CreateAttachedTab(CreateSnapshot(ledFirmware: "1.0.43"));

            Assert.False(tab.IsLayerCustomizationAvailable);
            Assert.Equal(LightingTabViewModel.LayerCustomizationLockedHint, tab.LayerLockHint);
            Assert.True(tab.Layers[0].IsEnabled);
            Assert.False(tab.Layers[1].IsEnabled);

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Same(tab.Layers[0], tab.SelectedLayer);
        }

        [AvaloniaFact]
        public void ReSelectingTheLayerAlreadyOpen_LeavesThePaintSelectionAlone()
        {
            // The switcher is a ListBox, and a ListBox raises SelectionChanged while it binds, so
            // this command runs whenever the tab is SHOWN — not only when the user moves layer.
            // Without the identity guard that wiped the selection before the first frame, and again
            // every time the tab was left and returned to.
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            // A per-key mode, because that is the only kind a selection exists in (issue #135).
            SelectMode(tab, LightingMode.Freestyle);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(1, tab.Selection.Count);

            tab.SelectLayerCommand.Execute(tab.SelectedLayer);

            Assert.Equal(1, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void MovingToTheOtherLayer_ClearsThePaintSelection()
        {
            // The other half of the guard: a real layer change still resets, because the keys the
            // selection names belong to the layer that is leaving.
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            SelectMode(tab, LightingMode.Freestyle);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(1, tab.Selection.Count);

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Equal(0, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void SelectKeyCommand_TogglesAKeyInAndOutOfThePaintSelection()
        {
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            SelectMode(tab, LightingMode.Freestyle);

            var key = boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            Assert.Equal(LightingPaintSelection.CaptionPrefix + LightingPaintSelection.EmptyCaptionSuffix, tab.Selection.Caption);

            tab.SelectKeyCommand.Execute(key);

            Assert.True(key.IsLightingSelected);
            Assert.Equal(1, tab.Selection.Count);
            Assert.Equal(LightingPaintSelection.CaptionPrefix + LightingPaintSelection.SingularCaptionSuffix, tab.Selection.Caption);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);

            Assert.Equal("Paint · 2 keys selected", tab.Selection.Caption);

            tab.SelectKeyCommand.Execute(key);

            Assert.False(key.IsLightingSelected);
            Assert.Equal(1, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void ExtendSelectionCommand_SelectsTheRunOfKeysBetweenTheAnchorAndTheTarget()
        {
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            SelectMode(tab, LightingMode.Freestyle);

            tab.SelectKeyCommand.Execute(boards[0].Keys[3]);
            tab.ExtendSelectionCommand.Execute(boards[0].Keys[6]);

            // "Between" is over the layer's key order, which is the order the caps are built in.
            Assert.Equal(4, tab.Selection.Count);
            Assert.All(
                boards[0].Keys.Skip(3).Take(4),
                key => Assert.True(key.IsLightingSelected));
            Assert.False(boards[0].Keys[7].IsLightingSelected);

            // Extending never removes: a mis-aimed shift-click costs one more click, not a restart.
            tab.ExtendSelectionCommand.Execute(boards[0].Keys[8]);

            Assert.Equal(6, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void ExtendSelectionCommand_WithNoAnchorYet_BehavesLikeAPlainClick()
        {
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            SelectMode(tab, LightingMode.Freestyle);

            tab.ExtendSelectionCommand.Execute(boards[0].Keys[5]);

            Assert.Equal(1, tab.Selection.Count);
            Assert.True(boards[0].Keys[5].IsLightingSelected);
        }

        [AvaloniaFact]
        public void SelectAllKeysCommand_SelectsEveryKeyOfTheShownLayer_AndClearTakesThemAllBackOut()
        {
            // The two bulk controls of the paint line are each other's opposite since issue #131:
            // `Select all` takes the layer, `Clear` empties the selection. Before it, `Clear`
            // painted the selected keys black — which left "Select all" with no undo but Reset All,
            // and left the button itself doing nothing visible in most states it was pressed in.
            var boards = BuildBoards(new LightingModel());
            var tab = CreateTab();

            tab.Attach(new LightingModel(), boards);

            SelectMode(tab, LightingMode.Freestyle);

            Assert.False(tab.ClearSelectionCommand.CanExecute(null));

            tab.SelectAllKeysCommand.Execute(null);

            Assert.Equal(boards[0].Keys.Count, tab.Selection.Count);
            Assert.All(boards[0].Keys, key => Assert.True(key.IsLightingSelected));
            Assert.True(tab.ClearSelectionCommand.CanExecute(null));

            tab.ClearSelectionCommand.Execute(null);

            Assert.Equal(0, tab.Selection.Count);
            Assert.All(boards[0].Keys, key => Assert.False(key.IsLightingSelected));
            Assert.Equal(
                LightingPaintSelection.CaptionPrefix + LightingPaintSelection.EmptyCaptionSuffix,
                tab.Selection.Caption);
            Assert.False(tab.ClearSelectionCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void Picker_WithASelection_PaintsEverySelectedKeyAndAnnouncesItOnce()
        {
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);
            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);

            var changes = 0;

            tab.ModelChanged += (_, _) => changes++;
            tab.Picker.Color = new LedColor(0, 128, 255);

            Assert.Equal(2, lighting.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(0, 128, 255), lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("1").Code]);
            Assert.Equal(new LedColor(0, 128, 255), lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("2").Code]);
            // The cap draws the SOFTENED face of that colour; the stored value asserted above is the
            // one the file keeps. Both are checked here on purpose — this is the seam where the two
            // could silently become one number again.
            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(0, 128, 255))),
                boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex].PaintColorHex);
            Assert.Equal(1, changes);
        }

        [AvaloniaFact]
        public void ASelectionMadeAfterTheColour_IsPaintedByTheGestureThatMadeIt()
        {
            // THE FLOW THE `Apply` BUTTON EXISTED FOR (issue #124), now done by the gesture itself
            // (issue #128). The picker only writes on ColorChanged, so a colour chosen BEFORE the
            // keys were picked had no way to land: that one gap is what the footer button was for,
            // and it was the last control on the rail that had to be pressed to commit anything.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(9, 9, 9);

            Assert.Empty(lighting.TopLayer.KeyColors);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(new LedColor(9, 9, 9), lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("1").Code]);

            // ...and the write is announced, or the editor's Save stays grey over an edited profile.
            var changes = 0;

            tab.ModelChanged += (_, _) => changes++;
            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);

            Assert.Equal(1, changes);
            Assert.Equal(new LedColor(9, 9, 9), lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("2").Code]);

            // The command itself survives the button, because it is still the honest name for "put
            // this colour on everything selected" and it is what Clear is built out of.
            tab.Picker.Color = new LedColor(1, 1, 1);
            tab.PaintSelectionCommand.Execute(null);

            Assert.Equal(2, lighting.TopLayer.KeyColors.Count);
            Assert.All(lighting.TopLayer.KeyColors.Values, color => Assert.Equal(new LedColor(1, 1, 1), color));
        }

        [AvaloniaFact]
        public void SelectAllKeysCommand_WithAColourAlreadyInThePicker_PaintsNothing()
        {
            // THE NAMED TRAP of issue #128, and the reason a paint gesture is three commands rather
            // than a subscription to LightingPaintSelection.Changed: "Select all" plus a held colour
            // would repaint the whole layer in one click, with nothing but Reset All to undo it —
            // exactly the regression #124 removed when ApplyZoneCommand became SelectZoneCommand.
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);
            var changes = 0;

            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(200, 30, 30);
            tab.ModelChanged += (_, _) => changes++;

            tab.SelectAllKeysCommand.Execute(null);

            Assert.Equal(tab.Board!.Keys.Count, tab.Selection.Count);
            Assert.Empty(lighting.TopLayer.KeyColors);
            Assert.Equal(0, changes);
        }

        [AvaloniaFact]
        public void APaintGesture_PaintsOnlyWhatItAdded_AndNeverTheRestOfTheSelection()
        {
            // Painting the WHOLE selection on every gesture would turn one cap click into a repaint
            // of everything currently selected — which `Select all` makes the entire layer, with
            // nothing but Reset All to undo it. `Select all` is the sharpest case precisely because
            // it paints nothing itself, so it leaves ~95 selected keys with no colour on them: the
            // very next gesture has to leave all of them alone.
            //
            // Until issue #131 this was written around `Clear`, which erased the selected keys'
            // colours and left them selected. Clear empties the selection now, so the case is made
            // with the bulk selector instead — the claim it protects is the same one.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(255, 0, 0);
            tab.SelectAllKeysCommand.Execute(null);

            Assert.Equal(boards[0].Keys.Count, tab.Selection.Count);
            Assert.Empty(lighting.TopLayer.KeyColors);

            var key = boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            // It is already selected, so this click takes it OUT — a gesture that adds nothing and
            // therefore paints nothing.
            tab.SelectKeyCommand.Execute(key);

            Assert.Empty(lighting.TopLayer.KeyColors);

            // ...and this one puts it back, which adds exactly one key and paints exactly that key.
            tab.SelectKeyCommand.Execute(key);

            Assert.Equal(boards[0].Keys.Count, tab.Selection.Count);
            Assert.Single(lighting.TopLayer.KeyColors);
            Assert.Contains(TestLayouts.Gen1Key("1").Code, lighting.TopLayer.KeyColors.Keys);
        }

        [AvaloniaFact]
        public void AShiftClickRun_PaintsTheKeysItAdded()
        {
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(4, 5, 6);
            tab.SelectKeyCommand.Execute(boards[0].Keys[3]);
            tab.ExtendSelectionCommand.Execute(boards[0].Keys[6]);

            Assert.Equal(4, tab.Selection.Count);
            Assert.Equal(4, lighting.TopLayer.KeyColors.Count);
            Assert.All(lighting.TopLayer.KeyColors.Values, color => Assert.Equal(new LedColor(4, 5, 6), color));
        }

        [AvaloniaFact]
        public void ClearSelectionCommand_EmptiesTheSelection_AndLeavesTheColoursOnTheLayer()
        {
            // ISSUE #131's second half. `Clear` sits beside `Select all`, so it undoes `Select all`
            // rather than painting: it was bound to a command that turned the selected keys black,
            // which is invisible with nothing selected, invisible over unpainted keys, and
            // invisible under a mode whose paint layer is drawn at 0 % — "the Clear button does
            // nothing", as reported.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            var key = boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            tab.SelectKeyCommand.Execute(key);
            tab.Picker.Color = new LedColor(255, 0, 0);

            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            var changes = 0;

            tab.ModelChanged += (_, _) => changes++;
            tab.ClearSelectionCommand.Execute(null);

            Assert.Equal(0, tab.Selection.Count);
            Assert.False(key.IsLightingSelected);

            // It moves no colour and so writes nothing into the profile: a selection is not file
            // state, and Save must not go amber for letting go of one.
            Assert.Equal(0, changes);
            Assert.Single(lighting.TopLayer.KeyColors);
            Assert.True(key.HasPaintColor);
        }

        [AvaloniaFact]
        public void PaintingAKeyBlack_StillErasesIt_WhichIsTheEraseClearUsedToOwn()
        {
            // SetKeyColor's contract (specs/07-lighting.md §2.1): black is "no colour", so the map
            // never holds it and the cap goes hatched rather than dark. That contract is what made
            // `Clear` an eraser before issue #131 and it is untouched by the rebinding — picking
            // black in the picker is the same erase, on the same selection, through the same call.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();

            tab.Attach(lighting, boards);
            SelectMode(tab, LightingMode.Freestyle);

            var key = boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            tab.SelectKeyCommand.Execute(key);
            tab.Picker.Color = new LedColor(255, 0, 0);

            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            tab.Picker.Color = LedColor.Black;

            Assert.Empty(lighting.TopLayer.KeyColors);
            Assert.False(key.HasPaintColor);
            Assert.Null(key.PaintColorHex);

            // ...and the keys stay selected, because painting is about colour.
            Assert.Equal(1, tab.Selection.Count);
        }

        /// <summary>
        /// A layer whose <b>stored</b> mode has no per-key colour opens locked (issue #135). The
        /// gate lives in <c>RefreshParameters</c> rather than in <c>SelectMode</c> precisely for
        /// this: <c>Attach</c> and a layer switch both reach it, so a profile read off the drive in
        /// Wave is unselectable from its first frame without anything having to pick a mode first.
        /// </summary>
        [AvaloniaFact]
        public void ALayerStoredInAModeWithNoPerKeyColour_OpensLocked()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.Mode = LightingMode.Wave;
            lighting.FnLayer.Mode = LightingMode.Freestyle;

            var tab = CreateAttachedTab(lighting: lighting);

            Assert.Equal(LightingMode.Wave, tab.SelectedMode);
            Assert.False(tab.CanSelectKeys);
            Assert.False(tab.SelectAllKeysCommand.CanExecute(null));

            tab.SelectKeyCommand.Execute(tab.Board!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(0, tab.Selection.Count);

            // The other layer is stored in a per-key mode, so moving to it unlocks the board — the
            // gate follows what is shown, and a layer switch runs the same RefreshParameters.
            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.True(tab.CanSelectKeys);

            tab.SelectKeyCommand.Execute(tab.Board!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            Assert.Equal(1, tab.Selection.Count);
        }

        /// <summary>
        /// A selection survives a move between two modes that both have per-key colours, and is
        /// emptied by a move to one that has none (issue #135) — and by a layer change, which it
        /// always was, because the keys it names belong to the layer that is leaving.
        /// <para>
        /// The first half is what keeps the rail usable: Freestyle and Breathe paint the same map,
        /// so comparing them must not cost the user their selection. The second is the new rule.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public void Selection_SurvivesABetweenPerKeyModes_AndIsEmptiedByOneWithout()
        {
            var tab = CreateAttachedTabInAPerKeyMode();
            var boards = tab.Board!;
            var key = boards.Keys[TestLayouts.RgbDigitOneKeyIndex];

            tab.SelectKeyCommand.Execute(key);

            SelectMode(tab, LightingMode.Breathe);

            Assert.True(tab.CanSelectKeys);
            Assert.Equal(1, tab.Selection.Count);
            Assert.True(key.IsLightingSelected);

            // Wave's file body is `[wave]>[spdN][dirX]` and nothing else (§2.2), so there is no
            // per-key colour for a selection to be about.
            SelectMode(tab, LightingMode.Wave);

            Assert.False(tab.CanSelectKeys);
            Assert.Equal(0, tab.Selection.Count);
            Assert.False(key.IsLightingSelected);

            // Back in a per-key mode the board is live again — the lock is a property of the mode
            // showing, never a latch that has to be undone.
            SelectMode(tab, LightingMode.Freestyle);

            Assert.True(tab.CanSelectKeys);

            tab.SelectKeyCommand.Execute(key);

            Assert.Equal(1, tab.Selection.Count);

            tab.SelectLayerCommand.Execute(tab.Layers[1]);

            Assert.Equal(0, tab.Selection.Count);
            Assert.False(key.IsLightingSelected);
        }

        /// <summary>
        /// THE REVERSAL OF MOCKUP 2f (issue #135). 2f draws "Paint · 2 keys selected", "Select all"
        /// and "Clear" on a board running WAVE, beside the sentence "the colors are still on file",
        /// and until now this app followed it: the paint belongs to the layer rather than to the
        /// effect over it, so the controls that manage it were reachable in every mode.
        /// <para>
        /// In use that offered a write the hardware never performs — §2.2 gives Wave no per-key
        /// colour line at all, so the firmware reads none. Selecting keys there is now refused: no
        /// selection can be taken, the two bulk buttons are disabled, and a cap click is inert.
        /// What survives is the <i>display</i> half of 2f's sentence, asserted below — colours
        /// already on file still show at 40% under the effect.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public void Painting_InAPaintIgnoringMode_IsRefused_ButTheColoursOnFileStillShow()
        {
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab();
            var key = boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            tab.Attach(lighting, boards);

            // Paint one key in a mode that can, then leave for one that cannot.
            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectKeyCommand.Execute(key);
            tab.Picker.Color = new LedColor(1, 2, 3);

            Assert.Equal(new LedColor(1, 2, 3), lighting.TopLayer.KeyColors[TestLayouts.Gen1Key("1").Code]);

            SelectMode(tab, LightingMode.Wave);

            Assert.False(tab.Parameters.HasPerKeyColors);
            Assert.False(tab.CanSelectKeys);
            Assert.Equal(0, tab.Selection.Count);

            // The board does not respond, and neither bulk button is available.
            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);
            tab.ExtendSelectionCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);

            Assert.Equal(0, tab.Selection.Count);
            Assert.False(tab.SelectAllKeysCommand.CanExecute(null));
            Assert.False(tab.ClearSelectionCommand.CanExecute(null));
            Assert.False(tab.PaintSelectionCommand.CanExecute(null));
            Assert.False(tab.SelectZoneCommand.CanExecute(tab.Zones[0]));

            // Nothing reached the model: the one painted key is still the only one on file.
            Assert.Equal(
                new LedColor(1, 2, 3),
                Assert.Single(lighting.TopLayer.KeyColors).Value);

            // ...and the mode still decides how that colour is DRAWN, which is the half of 2f that
            // survives: Wave shows it at 40% under the travelling effect.
            tab.AdvancePreview(0.1);

            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(1, 2, 3))),
                key.PaintColorHex);
            Assert.Equal(LightingEffectFrame.PaintOpacityDimmed, key.PaintOpacity);
        }

        [AvaloniaFact]
        public void ColoursPaintedUnderAPaintIgnoringMode_AreOnTheLayer_AndSurviveTheRoundTrip()
        {
            // "The colors are still on file", mechanically. The per-key map is the LAYER's state, so
            // a colour painted while Wave is running is written to led<n>.txt as soon as the layer's
            // mode is one whose grammar carries per-key lines (§2.2) — no repainting, no second
            // gesture. Under Wave itself the file has nowhere to put them: its grammar is
            // `[wave]>[spdN][dirX]` and nothing else, which is why this test crosses back.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting, boards);

            // The colours are laid down in a per-key mode — since issue #135 the only kind that has
            // a selection — and the layer is then carried INTO Wave, which is the crossing this
            // test is about. Before #135 they were painted under Wave directly; what is asserted
            // has not changed, only the route to it.
            SelectMode(tab, LightingMode.Freestyle);

            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitOneKeyIndex]);
            tab.SelectKeyCommand.Execute(boards[0].Keys[TestLayouts.RgbDigitTwoKeyIndex]);
            tab.Picker.Color = new LedColor(87, 196, 216);

            var painted = new Dictionary<int, LedColor>(lighting.TopLayer.KeyColors);

            SelectMode(tab, LightingMode.Wave);

            Assert.Equal(2, painted.Count);

            var underWave = LedFileParser.ParseRgb(LedFileSerializer.SerializeRgb(lighting));

            Assert.Equal(LightingMode.Wave, underWave.TopLayer.Mode);
            Assert.Empty(underWave.TopLayer.KeyColors);

            // Back to a mode whose file body IS the per-key lines, without touching a colour: the
            // two keys painted under Wave are what gets written.
            SelectMode(tab, LightingMode.Freestyle);

            var reloaded = LedFileParser.ParseRgb(LedFileSerializer.SerializeRgb(lighting));

            Assert.Equal(LightingMode.Freestyle, reloaded.TopLayer.Mode);
            Assert.Equal(painted.Count, reloaded.TopLayer.KeyColors.Count);
            Assert.All(painted, entry => Assert.Equal(entry.Value, reloaded.TopLayer.KeyColors[entry.Key]));
            Assert.Equal(
                new LedColor(87, 196, 216),
                reloaded.TopLayer.KeyColors[TestLayouts.Gen1Key("1").Code]);
        }

        [AvaloniaFact]
        public void Zones_ForTheFreestyleEdgeRgb_AreTheEightOfTheSpec()
        {
            var tab = CreateAttachedTab();

            Assert.Equal(
                new[] { "All", "Number", "Function", "WASD", "Game", "Arrow", "Left Module", "Right Module" },
                tab.Zones.Select(zone => zone.Caption));
        }

        [AvaloniaFact]
        public void SelectZoneCommand_SelectsEveryKeyOfTheZone_AndPaintsThem()
        {
            // A zone button is a user pointing at a named set of keys on the board, so it is one of
            // the three direct paint gestures (issue #128) and commits on the spot — the Apply that
            // #124 split it from is gone. `Select all` is deliberately NOT one of them; see
            // SelectAllKeysCommand_WithAColourAlreadyInThePicker_PaintsNothing.
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Breathe);
            tab.Picker.Color = new LedColor(12, 34, 56);

            var numbers = tab.Zones.Single(zone => zone.Caption == "Number");

            tab.SelectZoneCommand.Execute(numbers);

            Assert.Equal(numbers.KeyCodes.Count, tab.Selection.Count);
            Assert.All(
                numbers.KeyCodes,
                keyCode => Assert.True(
                    tab.Board!.Keys.Single(key => key.Key.OriginalKey.Code == keyCode).IsLightingSelected));

            Assert.Equal(numbers.KeyCodes.Count, lighting.TopLayer.KeyColors.Count);
            Assert.All(
                numbers.KeyCodes,
                keyCode => Assert.Equal(new LedColor(12, 34, 56), lighting.TopLayer.KeyColors[keyCode]));
        }

        [AvaloniaFact]
        public void SelectZoneCommand_TakingAZoneBackOut_PaintsNothing()
        {
            // The other half of the toggle. Only what a gesture ADDS is painted, so the click that
            // deselects a zone writes nothing at all — and the colours it left behind stay, because
            // erasing is `Clear`'s job and not a side effect of letting go of a selection.
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.Picker.Color = new LedColor(12, 34, 56);

            var wasd = tab.Zones.Single(zone => zone.Caption == "WASD");

            tab.SelectZoneCommand.Execute(wasd);

            var painted = new Dictionary<int, LedColor>(lighting.TopLayer.KeyColors);
            var changes = 0;

            tab.ModelChanged += (_, _) => changes++;
            tab.SelectZoneCommand.Execute(wasd);

            Assert.Equal(0, tab.Selection.Count);
            Assert.Equal(0, changes);
            Assert.Equal(painted.Count, lighting.TopLayer.KeyColors.Count);
            Assert.All(painted, entry => Assert.Equal(entry.Value, lighting.TopLayer.KeyColors[entry.Key]));
        }

        [AvaloniaFact]
        public void SelectZoneCommand_OnTheFnLayer_SelectsTheSamePhysicalPositions()
        {
            // Zone membership is authored against the top layer; on the Fn layer the same
            // positions carry different memory keys (specs/07-lighting.md §2.4 item 6). That
            // resolution survived the select/apply split — it now happens on the way into the
            // selection instead of on the way into the model.
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting, boards);
            tab.SelectLayerCommand.Execute(tab.Layers[1]);
            SelectMode(tab, LightingMode.Freestyle);

            tab.Picker.Color = new LedColor(7, 7, 7);

            // The Function zone is the sharp case: positions 2-7 carry F1-F6 on the top layer and
            // the media keys on the Fn layer, so a zone applied verbatim would colour keys the Fn
            // layer does not have.
            var function = tab.Zones.Single(zone => zone.Caption == "Function");

            tab.SelectZoneCommand.Execute(function);

            Assert.Equal(function.KeyCodes.Count, tab.Selection.Count);
            Assert.Equal(function.KeyCodes.Count, lighting.FnLayer.KeyColors.Count);
            Assert.Contains(TestLayouts.Gen1Key("mute").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.Contains(TestLayouts.Gen1Key("next").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.DoesNotContain(TestLayouts.Gen1Key("F1").Code, lighting.FnLayer.KeyColors.Keys);
            Assert.All(
                lighting.FnLayer.KeyColors.Keys,
                keyCode => Assert.NotNull(boards[1].Layer.FindByOriginalKeyCode(keyCode)));
            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [AvaloniaFact]
        public void SelectZoneCommand_OnAZoneThatIsAlreadySelected_TakesItBackOut()
        {
            // Plain subtraction: a zone whose keys are all selected comes back out on the next
            // press, which is what makes a mis-aimed zone recoverable without emptying everything.
            var tab = CreateAttachedTabInAPerKeyMode();
            var wasd = tab.Zones.Single(zone => zone.Caption == "WASD");

            tab.SelectZoneCommand.Execute(wasd);

            Assert.Equal(4, tab.Selection.Count);
            Assert.All(KeysOf(tab, wasd), key => Assert.True(key.IsLightingSelected));

            tab.SelectZoneCommand.Execute(wasd);

            Assert.Equal(0, tab.Selection.Count);
            Assert.All(KeysOf(tab, wasd), key => Assert.False(key.IsLightingSelected));
        }

        [AvaloniaFact]
        public void SelectZoneCommand_OverAnotherZone_AddsToTheSelectionRatherThanReplacingIt()
        {
            var tab = CreateAttachedTabInAPerKeyMode();
            var wasd = tab.Zones.Single(zone => zone.Caption == "WASD");
            var arrows = tab.Zones.Single(zone => zone.Caption == "Arrow");

            tab.SelectZoneCommand.Execute(wasd);
            tab.SelectZoneCommand.Execute(arrows);

            // The two are disjoint, so the counts simply add — and the board says so key by key,
            // which is where a selection is read now that no button carries a face for it.
            Assert.Equal(wasd.KeyCodes.Count + arrows.KeyCodes.Count, tab.Selection.Count);
            Assert.All(KeysOf(tab, wasd), key => Assert.True(key.IsLightingSelected));
            Assert.All(KeysOf(tab, arrows), key => Assert.True(key.IsLightingSelected));
        }

        [AvaloniaFact]
        public void AZoneButton_OverALargerZoneItSitsInside_TouchesOnlyItsOwnKeys()
        {
            // THE REPORTED SEQUENCE of issue #131: press `Game`, press `WASD`, press `WASD` again.
            // The zones are NESTED — WASD's four keys are all inside Game's twenty-nine — and the
            // button used to carry a derived "all my keys are selected" face, so the third press
            // removed four keys and un-lit `Game`, whose twenty-five other keys were still
            // selected. Two buttons disagreeing about one selection is what the user saw as them
            // interfering with each other.
            //
            // Each button now acts on its own keys and shows nothing, so the sequence is arithmetic:
            // +29, +0 (already in), -4.
            var tab = CreateAttachedTabInAPerKeyMode();
            var game = tab.Zones.Single(zone => zone.Caption == "Game");
            var wasd = tab.Zones.Single(zone => zone.Caption == "WASD");

            Assert.All(wasd.KeyCodes, keyCode => Assert.Contains(keyCode, game.KeyCodes));

            tab.SelectZoneCommand.Execute(game);

            Assert.Equal(game.KeyCodes.Count, tab.Selection.Count);

            // WASD is already wholly inside the selection, so this press subtracts it.
            tab.SelectZoneCommand.Execute(wasd);

            Assert.Equal(game.KeyCodes.Count - wasd.KeyCodes.Count, tab.Selection.Count);
            Assert.All(KeysOf(tab, wasd), key => Assert.False(key.IsLightingSelected));

            // ...and Game's OTHER keys are untouched: nothing about the smaller zone reaches them.
            var others = game.KeyCodes.Where(keyCode => !wasd.KeyCodes.Contains(keyCode)).ToArray();

            Assert.NotEmpty(others);
            Assert.All(
                others,
                keyCode => Assert.True(
                    tab.Board!.Keys.Single(key => key.Key.OriginalKey.Code == keyCode).IsLightingSelected));

            // Pressing Game again is the same plain addition it always was: four of its keys are
            // missing, so the whole zone goes back in rather than coming out.
            tab.SelectZoneCommand.Execute(game);

            Assert.Equal(game.KeyCodes.Count, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void AZoneButton_AfterSelectAll_TakesItsOwnKeysBackOut()
        {
            // The same defect inverted, and separately reported: after `Select all` every button was
            // lit, so un-pressing WASD left `Game` unlit while twenty-five of its keys were still
            // selected — and the NEXT press on Game took the select branch and put the four back
            // instead of removing twenty-five. "Un-toggling the buttons below sometimes doesn't
            // deselect", in the report's words.
            var tab = CreateAttachedTabInAPerKeyMode();
            var wasd = tab.Zones.Single(zone => zone.Caption == "WASD");
            var game = tab.Zones.Single(zone => zone.Caption == "Game");
            var total = tab.Board!.Keys.Count;

            tab.SelectAllKeysCommand.Execute(null);

            Assert.Equal(total, tab.Selection.Count);

            tab.SelectZoneCommand.Execute(wasd);

            Assert.Equal(total - wasd.KeyCodes.Count, tab.Selection.Count);
            Assert.All(KeysOf(tab, wasd), key => Assert.False(key.IsLightingSelected));

            // Game is no longer wholly selected, so it adds its four missing keys back — plain
            // addition, and nothing else in the selection moves.
            tab.SelectZoneCommand.Execute(game);

            Assert.Equal(total, tab.Selection.Count);

            // And a zone that IS wholly selected still subtracts, which is the half the latch broke.
            tab.SelectZoneCommand.Execute(game);

            Assert.Equal(total - game.KeyCodes.Count, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void TheAllZone_IsAPlainButtonToo_TakingTheWholeLayerInAndOut()
        {
            // `All` is a superset of every other zone, which is what made it the worst latch on the
            // row. As a plain button it is unremarkable: everything in, everything out.
            var tab = CreateAttachedTabInAPerKeyMode();
            var all = tab.Zones.Single(zone => zone.Caption == "All");

            tab.SelectZoneCommand.Execute(all);

            Assert.Equal(all.KeyCodes.Count, tab.Selection.Count);

            tab.SelectZoneCommand.Execute(all);

            Assert.Equal(0, tab.Selection.Count);
        }

        [AvaloniaFact]
        public void PaintSelectionCommand_WithNothingSelected_IsDisabled()
        {
            var tab = CreateAttachedTab();

            Assert.False(tab.PaintSelectionCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void PaintSelectionCommand_AnnouncesItsGate_WhenTheSelectionMoves()
        {
            // The view binds IsEnabled to the command, so the gate has to announce itself or Apply
            // stays grey over a board full of selected keys.
            var tab = CreateAttachedTabInAPerKeyMode();
            var announced = 0;

            tab.PaintSelectionCommand.CanExecuteChanged += (_, _) => announced++;

            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));

            Assert.True(announced > 0);
            Assert.True(tab.PaintSelectionCommand.CanExecute(null));

            announced = 0;

            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));

            Assert.True(announced > 0);
            Assert.False(tab.PaintSelectionCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public void PaintSelectionCommand_PaintsTheWholeSelectionWithThePickersColour()
        {
            // It has no button since issue #128, but it is still the panel's "put this colour on
            // everything selected" — the whole selection, not only what the last gesture added —
            // and Clear is built out of it.
            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));

            tab.Picker.Color = new LedColor(9, 90, 190);
            tab.SelectAllKeysCommand.Execute(null);

            // "Select all" paints nothing, so only WASD carries a colour at this point.
            Assert.Equal(4, lighting.TopLayer.KeyColors.Count);

            tab.PaintSelectionCommand.Execute(null);

            Assert.All(
                tab.Board!.Keys,
                key => Assert.Equal(
                    new LedColor(9, 90, 190),
                    lighting.TopLayer.KeyColors[key.Key.OriginalKey.Code]));
        }

        [AvaloniaFact]
        public async Task ResetAllCommand_WhenConfirmed_ErasesEveryKeyColour()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));
            tab.PaintSelectionCommand.Execute(null);

            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            await tab.ResetAllCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(LightingTabViewModel.ResetAllConfirmation, request.Message);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [AvaloniaFact]
        public async Task ResetAllCommand_WhenDeclined_ChangesNothing()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));
            tab.PaintSelectionCommand.Execute(null);

            await tab.ResetAllCommand.ExecuteAsync(null);

            Assert.Equal(4, lighting.TopLayer.KeyColors.Count);
        }

        [AvaloniaFact]
        public async Task ResetAllCommand_InAPaintIgnoringMode_StillErasesTheLayersColours()
        {
            // "Reset All" manages the same per-key map the paint row does, so it is reachable under
            // the same rule — and it has to be, because the colours it erases are exactly the ones
            // still showing at 40% under an effect that does not read them.
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            var lighting = new LightingModel();
            var tab = CreateAttachedTab(lighting: lighting);

            SelectMode(tab, LightingMode.Freestyle);
            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "WASD"));
            tab.PaintSelectionCommand.Execute(null);

            SelectMode(tab, LightingMode.Spectrum);

            Assert.True(tab.ResetAllCommand.CanExecute(null));
            Assert.NotEmpty(lighting.TopLayer.KeyColors);

            await tab.ResetAllCommand.ExecuteAsync(null);

            Assert.Single(_notifications.MessageBoxes);
            Assert.Empty(lighting.TopLayer.KeyColors);
        }

        [AvaloniaFact]
        public void AdvancePreview_PushesTheSampledFrameOntoTheCaps()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);
            tab.AdvancePreview(0.2);

            var board = tab.Board!;

            // Wave lights the board, so something is lit — and a key the effect does not reach
            // this frame carries no colour at all rather than a black one.
            Assert.Contains(board.Keys, key => key.HasEffectColor);
            Assert.All(
                board.Keys.Where(key => !key.HasEffectColor),
                key => Assert.Null(key.EffectColorHex));
        }

        [AvaloniaFact]
        public void AdvancePreview_ForTheSameElapsedTime_DrawsTheSameFrame()
        {
            // The sampler is deterministic: no clock, no Random, no state carried between calls.
            var first = CreateAttachedTab();
            var second = CreateAttachedTab();

            SelectMode(first, LightingMode.Starlight);
            SelectMode(second, LightingMode.Starlight);

            first.AdvancePreview(0.4);
            first.AdvancePreview(0.4);
            second.AdvancePreview(0.4);
            second.AdvancePreview(0.4);

            Assert.Equal(
                first.Board!.Keys.Select(key => key.EffectColorHex),
                second.Board!.Keys.Select(key => key.EffectColorHex));
            Assert.Equal(
                first.Board!.Keys.Select(key => key.EffectIntensity),
                second.Board!.Keys.Select(key => key.EffectIntensity));
        }

        [AvaloniaFact]
        public void AdvancePreview_UnderAPaintIgnoringMode_DimsThePaintLayerToFortyPercent()
        {
            // Mockup 2f, verbatim: "Wave ignores painted colors, so the paint layer is shown at 40%
            // under the effect — the colors are still on file."
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);
            tab.AdvancePreview(0.1);

            Assert.All(
                tab.Board!.Keys,
                key => Assert.Equal(LightingEffectFrame.PaintOpacityDimmed, key.PaintOpacity));
        }

        [AvaloniaFact]
        public void AdvancePreview_UnderAPaintDirectMode_DrawsThePaintLayerInFull()
        {
            var tab = CreateAttachedTab();

            SelectMode(tab, LightingMode.Freestyle);
            tab.AdvancePreview(0.1);

            Assert.True(tab.Parameters.RendersPaintDirectly);
            Assert.All(
                tab.Board!.Keys,
                key => Assert.Equal(LightingEffectFrame.PaintOpacityDirect, key.PaintOpacity));
        }

        [AvaloniaFact]
        public void AdvancePreview_UnderAPaintIgnoringMode_PutsThePaintOverTheEffectOnAPaintedKeyOnly()
        {
            // Which SIDE of the effect the paint is composited on is how the 40% survives a mode
            // that lights every key at intensity 1.0 — Wave, Solid and Spectrum all do, and the
            // paint drawn under them would be covered outright. It is a per-key answer: an
            // unpainted cap has nothing to reveal, so its effect stays at full strength.
            var tab = CreateAttachedTabInAPerKeyMode();
            var painted = tab.Board!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            // Painted where painting is possible, then carried into Wave (issue #135) — the cap's
            // colour is the layer's either way, and what is asserted is how Wave DRAWS it.
            tab.SelectKeyCommand.Execute(painted);
            tab.Picker.Color = new LedColor(0, 0, 255);

            SelectMode(tab, LightingMode.Wave);

            tab.AdvancePreview(0.1);

            Assert.True(painted.ShowsPaintOverEffect);
            Assert.False(painted.ShowsPaintUnderEffect);
            Assert.All(
                tab.Board!.Keys.Where(key => !key.HasPaintColor),
                key =>
                {
                    Assert.False(key.ShowsPaintOverEffect);
                    Assert.False(key.ShowsPaintUnderEffect);
                });
        }

        [AvaloniaFact]
        public void AdvancePreview_UnderBreathe_KeepsThePaintUnderTheEffect_AndStillVariesOverTime()
        {
            // The paint-direct half, unchanged: Breathe's own pulse IS the effect layer modulating
            // the painted colour, so the paint stays under it and the intensity keeps moving. A fix
            // that dimmed the effect on every painted key would flatten this into a constant wash.
            var tab = CreateAttachedTab();
            var painted = tab.Board!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            SelectMode(tab, LightingMode.Breathe);

            tab.SelectKeyCommand.Execute(painted);
            tab.Picker.Color = new LedColor(0, 0, 255);

            var intensities = new List<double>();

            for (var frame = 0; frame < 8; frame++)
            {
                tab.AdvancePreview(0.25);

                intensities.Add(painted.EffectIntensity);
            }

            Assert.True(painted.ShowsPaintUnderEffect);
            Assert.False(painted.ShowsPaintOverEffect);
            Assert.Equal(LightingEffectFrame.PaintOpacityDirect, painted.PaintOpacity);
            Assert.True(intensities.Distinct().Count() > 1, "Breathe stopped breathing on a painted key.");
        }

        [AvaloniaFact]
        public void AdvancePreview_UnderReduceMotion_HoldsTheBoardOnTheFirstFrame()
        {
            var motion = CreateMotionSettings(reduceMotion: true);
            var tab = CreateAttachedTab(motionSettings: motion);
            var atZero = CreateAttachedTab();

            SelectMode(tab, LightingMode.Wave);
            SelectMode(atZero, LightingMode.Wave);

            atZero.AdvancePreview(0.0);

            tab.AdvancePreview(5.0);
            tab.AdvancePreview(5.0);

            Assert.False(tab.IsPreviewAnimating);

            // Frozen is a held frame, not a dark board: the point of this screen is to show what
            // the mode looks like, so an all-unlit board would pass this comparison vacuously.
            Assert.Contains(tab.Board!.Keys, key => key.HasEffectColor);
            Assert.Equal(
                atZero.Board!.Keys.Select(key => key.EffectColorHex),
                tab.Board!.Keys.Select(key => key.EffectColorHex));
        }

        [AvaloniaFact]
        public void AdvancePreview_WhenReduceMotionIsTurnedOffMidRun_ResumesAnimating()
        {
            // Since issue #96 reduce-motion is a live preference with no change notification, so
            // the tab re-reads it every frame; this is the case that proves it.
            var motion = CreateMotionSettings(reduceMotion: true);
            var tab = CreateAttachedTab(motionSettings: motion);

            SelectMode(tab, LightingMode.Wave);
            tab.AdvancePreview(5.0);

            var frozen = tab.Board!.Keys.Select(key => key.EffectColorHex).ToList();

            motion.ReduceMotion = false;

            tab.AdvancePreview(0.5);

            Assert.True(tab.IsPreviewAnimating);
            Assert.NotEqual(frozen, tab.Board!.Keys.Select(key => key.EffectColorHex).ToList());
        }

        [AvaloniaFact]
        public void BoardHeader_NamesTheModeAndWhetherItIsLive()
        {
            var motion = CreateMotionSettings(reduceMotion: false);
            var tab = CreateAttachedTab(motionSettings: motion);

            SelectMode(tab, LightingMode.Wave);
            tab.AdvancePreview(0.1);

            Assert.Equal(tab.ModeCaption + LightingTabViewModel.LivePreviewSuffix, tab.BoardHeader);
            Assert.Equal(LightingModeCatalog.Find(LightingMode.Wave).DisplayName, tab.ModeCaption);

            motion.ReduceMotion = true;

            tab.AdvancePreview(0.1);

            Assert.Equal(tab.ModeCaption + LightingTabViewModel.FrozenPreviewSuffix, tab.BoardHeader);
        }

        [AvaloniaFact]
        public void TheAppLayer_HoldsNoSecondCopyOfThePerModeParameterTable()
        {
            // LightingPanelVisibility was that copy and is gone; LightingModeParameters subsumed
            // it. Nothing in the app assembly may restate a lighting rule.
            var appTypes = typeof(LightingTabViewModel).Assembly
                .GetTypes()
                .Select(type => type.Name)
                .ToArray();

            Assert.DoesNotContain("LightingPanelVisibility", appTypes);
            Assert.IsType<LightingModeParameters>(CreateAttachedTab().Parameters);
        }

        [AvaloniaFact]
        public void Attach_WithoutALightingModel_FallsBackToAnInMemoryOne()
        {
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting: null, BuildBoards(new LightingModel()));

            Assert.Equal(2, tab.Layers.Count);

            SelectMode(tab, LightingMode.Pulse);

            Assert.Equal(LightingMode.Pulse, tab.SelectedMode);
            Assert.Equal(LightingMode.Pulse, tab.Layers[0].State.Mode);
        }

        [AvaloniaFact]
        public void Attach_ForADeviceThePanelCannotEdit_DoesNothing()
        {
            var tab = new LightingTabViewModel(
                TestDevices.CreateSnapshot(DeviceId.Tko),
                _notifications,
                _preferences);

            tab.Attach(new TkoLightingModel(), []);

            Assert.False(tab.IsAvailable);
            Assert.Empty(tab.Layers);
            Assert.Null(tab.SelectedLayer);

            // And its preview is inert rather than throwing at whatever the view's timer does.
            tab.AdvancePreview(0.1);
        }

        [AvaloniaFact]
        public void EveryEdit_SerialisedAndReparsed_SurvivesTheRoundTrip()
        {
            // The acceptance criterion: what the panel writes into the model has to be expressible
            // in led<n>.txt. ProfileSession.Save is exactly SerializeRgb + a file write, so this is
            // the app-layer half of "a lighting edit survives Save → reload".
            var lighting = new LightingModel();
            var boards = BuildBoards(lighting);
            var tab = CreateTab(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            tab.Attach(lighting, boards);

            // Top layer: a two-line effect with an effect colour, a base colour, a speed and a
            // direction.
            SelectMode(tab, LightingMode.Loop);
            tab.Picker.Color = new LedColor(255, 128, 0);
            tab.SelectColorSlotCommand.Execute(tab.BaseColor);
            tab.Picker.Color = new LedColor(0, 0, 64);
            tab.Speed = 8;
            tab.SelectDirectionCommand.Execute(tab.Directions.Single(entry => entry.Direction == LightingDirection.Right));

            // Fn layer: a per-key mode with a hand-painted key and a zone fill.
            tab.SelectLayerCommand.Execute(tab.Layers[1]);
            SelectMode(tab, LightingMode.Breathe);
            tab.Speed = 2;
            tab.SelectKeyCommand.Execute(boards[1].Keys[TestLayouts.RgbDigitOneKeyIndex]);
            tab.Picker.Color = new LedColor(10, 200, 30);

            // The Function zone lands on the Fn layer's media keys, which are exactly the eight
            // save-token exceptions of specs/07-lighting.md §2.4 item 7 — the hardest thing in the
            // file to round-trip.
            tab.SelectZoneCommand.Execute(tab.Zones.Single(zone => zone.Caption == "Function"));
            tab.PaintSelectionCommand.Execute(null);

            var reloaded = LedFileParser.ParseRgb(LedFileSerializer.SerializeRgb(lighting));

            Assert.True(lighting.TopLayer.IsEquivalentTo(reloaded.TopLayer));
            Assert.Equal(LightingMode.Loop, reloaded.TopLayer.Mode);
            Assert.Equal(new LedColor(255, 128, 0), reloaded.TopLayer.EffectColor);
            Assert.Equal(new LedColor(0, 0, 64), reloaded.TopLayer.BaseColor);
            Assert.Equal(8, reloaded.TopLayer.Speed);
            Assert.Equal(LightingDirection.Right, reloaded.TopLayer.Direction);

            Assert.Equal(LightingMode.Breathe, reloaded.FnLayer.Mode);
            Assert.Equal(2, reloaded.FnLayer.Speed);
            Assert.Equal(13, reloaded.FnLayer.KeyColors.Count);
            Assert.Equal(lighting.FnLayer.KeyColors.Count, reloaded.FnLayer.KeyColors.Count);
            Assert.All(
                lighting.FnLayer.KeyColors,
                entry => Assert.Equal(entry.Value, reloaded.FnLayer.KeyColors[entry.Key]));

            // The media keys came back through the §2.4 item 7 exception table: mute was written
            // as [F1] and read back as mute.
            Assert.Contains(TestLayouts.Gen1Key("mute").Code, reloaded.FnLayer.KeyColors.Keys);

            // The one thing that is deliberately not file state: a per-key mode writes no effect
            // colour line (§2.2), so the swatch is back at its default after a reload.
            Assert.Equal(new LedColor(10, 200, 30), lighting.FnLayer.EffectColor);
            Assert.Equal(LedColor.DefaultEffectColor, reloaded.FnLayer.EffectColor);
        }

        private static bool IsSupported(DeviceId deviceId)
        {
            return LightingTabViewModel.IsSupported(DeviceCatalog.GetById(deviceId));
        }

        private static void SelectMode(LightingTabViewModel tab, LightingMode mode)
        {
            tab.SelectModeCommand.Execute(tab.Modes.Single(entry => entry.Mode == mode));
        }

        /// <summary>
        /// The shown layer's caps for one zone. Since issue #131 a zone button carries no state of
        /// its own, so "is this zone selected" is a question about the <b>board</b> — which is where
        /// the user reads it too, off the caps' selection rings.
        /// </summary>
        private static IEnumerable<KeyboardKeyViewModel> KeysOf(LightingTabViewModel tab, LightingZoneViewModel zone)
        {
            return zone.KeyCodes.Select(
                keyCode => tab.Board!.Keys.Single(key => key.Key.OriginalKey.Code == keyCode));
        }

        private static DeviceSnapshot CreateSnapshot(
            string keyboardFirmware = "1.0.121",
            string ledFirmware = "1.0.58")
        {
            return TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                versionFile: TestDevices.CreateVersionFile(
                    DeviceId.FreestyleEdgeRgb,
                    keyboardFirmware,
                    ledFirmware));
        }

        /// <summary>
        /// The app's real <see cref="IMotionSettings"/> over a fake OS detector — the live switch,
        /// so a test can flip <see cref="IMotionSettings.ReduceMotion"/> mid-run the way the
        /// Settings screen does.
        /// </summary>
        private static IMotionSettings CreateMotionSettings(bool reduceMotion)
        {
            return new MotionSettings(new FakeReduceMotionDetector(reduceMotion));
        }

        private static IReadOnlyList<KeyboardLayerViewModel> BuildBoards(LightingModel lighting)
        {
            return KeyboardLayerViewModel.BuildAll(
                KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb),
                VisualCatalog.FreestyleEdgeRgb,
                lighting);
        }

        private LightingTabViewModel CreateTab(
            DeviceSnapshot? snapshot = null,
            IMotionSettings? motionSettings = null)
        {
            return new LightingTabViewModel(
                snapshot ?? CreateSnapshot(),
                _notifications,
                _preferences,
                motionSettings);
        }

        private LightingTabViewModel CreateAttachedTab(
            DeviceSnapshot? snapshot = null,
            LightingModel? lighting = null,
            IMotionSettings? motionSettings = null)
        {
            var model = lighting ?? new LightingModel();
            var tab = CreateTab(snapshot, motionSettings);

            tab.Attach(model, BuildBoards(model));

            return tab;
        }

        /// <summary>
        /// An attached tab switched into <b>Freestyle</b> — a mode with per-key colours.
        /// <para>
        /// Since issue #135 a selection can exist only in such a mode, and a fresh
        /// <see cref="LightingModel"/> opens in <c>Disabled</c> (the enum's zero), where the board
        /// is locked. Every test about selecting, painting or zoning therefore starts here, and
        /// says so in its own first line rather than inheriting a paintable mode from a shared
        /// fixture — which is how a whole suite ends up asserting one arrangement of the world.
        /// </para>
        /// </summary>
        private LightingTabViewModel CreateAttachedTabInAPerKeyMode(
            DeviceSnapshot? snapshot = null,
            LightingModel? lighting = null,
            IMotionSettings? motionSettings = null)
        {
            var tab = CreateAttachedTab(snapshot, lighting, motionSettings);

            SelectMode(tab, LightingMode.Freestyle);

            return tab;
        }
    }
}
