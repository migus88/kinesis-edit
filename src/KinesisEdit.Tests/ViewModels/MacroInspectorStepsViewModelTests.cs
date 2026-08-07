using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's step editor, and <b>specs/11-feature-dialogs.md §11.3 inside it</b>: the
    /// two captions, the exclusivity of the random radio and the custom field, the 1-999 clamp and
    /// validation, the resulting <c>dran</c>/<c>d001</c>..<c>d999</c> keys and the firmware gate of
    /// 09 §2 — every case migrated from <c>MacroDelayOverlayViewModelTests</c> when the modal was
    /// absorbed, plus the reorder, insert and delete the modal never had. The round-trip cases pin
    /// what §11.3's result does once it is written to a layout file (specs/06-macros.md §2.2).
    /// </summary>
    public sealed class MacroInspectorStepsViewModelTests
    {
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheSpecVerbatim()
        {
            Assert.Equal("Random Delay (1-150ms)", MacroInspectorStepsViewModel.RandomDelayCaption);
            Assert.Equal("Custom Delay (1-999ms)", MacroInspectorStepsViewModel.CustomDelayCaption);
            Assert.Equal(
                "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.",
                MacroInspectorStepsViewModel.InvalidDelayMessage);

            // Mockup 2i's own heading and reorder affordance.
            Assert.Equal("Steps", MacroInspectorStepsViewModel.SectionTitle);
            Assert.Equal("drag", MacroInspectorStepsViewModel.ReorderHintPrefix);
            Assert.Equal("insert step", MacroInspectorStepsViewModel.InsertStepCaption);
        }

        [Fact]
        public void ReorderShortcut_SpellsTheSameModifierOnBothPlatforms()
        {
            // ⌥ is Alt everywhere — only the spelling changes, exactly as the layer chips do it.
            Assert.Equal("· ⌥↑↓", MacroInspectorStepsViewModel.BuildReorderShortcut(isMacOs: true));
            Assert.Equal("· Alt+↑↓", MacroInspectorStepsViewModel.BuildReorderShortcut(isMacOs: false));
        }

        [Fact]
        public void Load_WithNoMacro_ShowsNothingAndNumbersTheFirstStepOne()
        {
            var steps = Create();

            steps.Load(null);

            Assert.Empty(steps.Items);
            Assert.False(steps.HasItems);
            Assert.Equal("01", steps.NextStepNumberText);
        }

        [Fact]
        public void Load_RendersEachStepAsTheMockDrawsIt()
        {
            var steps = Create();
            var macro = new Macro();

            // Mockup 2i writes the tokens as `[lshift]`/`[enter]` — the *legacy* spelling. The panel
            // spells them in the open device's own dialect (Gen1 here: `lshft`, `ent`), because mono
            // means "this is literally a value in THIS board's config file".
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("lshft")) { UpDown = KeyDirection.Down });
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b"), MacroModifiers.LeftShift));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("lshft")) { UpDown = KeyDirection.Up });
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("e")));

            steps.Load(macro);

            Assert.Equal(["01", "02", "03", "04"], steps.Items.Select(step => step.NumberText));
            Assert.Equal("[lshft]", steps.Items[0].TokenText);
            Assert.Equal(MacroInspectorStepViewModel.PressAction, steps.Items[0].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.HeldAction, steps.Items[1].ActionText);
            Assert.Equal("LS", steps.Items[1].ModifiersText);
            Assert.Equal(MacroInspectorStepViewModel.ReleaseAction, steps.Items[2].ActionText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, steps.Items[3].ActionText);
            Assert.Equal("05", steps.NextStepNumberText);
        }

        /// <summary>
        /// Mockup 2i draws <c>07 [enter] tap · 80 ms</c>: the delay is a keystroke of its own in the
        /// file, and a <b>qualifier of the step it follows</b> on screen.
        /// </summary>
        [Fact]
        public void Load_FoldsADelayIntoTheStepItFollows()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("ent")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));

            steps.Load(macro);

            var step = Assert.Single(steps.Items);

            Assert.Equal("[ent]", step.TokenText);
            Assert.Equal(MacroInspectorStepViewModel.TapAction, step.ActionText);
            Assert.Equal("80 ms", step.DelayText);
            Assert.Equal(80, step.DelayMilliseconds);
            Assert.False(step.IsRandomDelay);
        }

        [Fact]
        public void Load_WithALeadingDelay_GivesItARowOfItsOwn()
        {
            // The mock never draws this, but the file can hold it, and dropping it would be editing
            // the macro behind the user's back.
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));

            steps.Load(macro);

            Assert.Equal(2, steps.Items.Count);
            Assert.True(steps.Items[0].IsDelayOnly);
            Assert.Equal(MacroInspectorStepViewModel.DelayAction, steps.Items[0].ActionText);
            Assert.Equal("random", steps.Items[0].DelayText);
            Assert.Equal("[a]", steps.Items[1].TokenText);
        }

        [Fact]
        public void MoveStep_ReordersTheMacroAndReportsIt()
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");
            var changed = 0;

            steps.Changed += (_, _) => changed++;
            steps.Load(macro);

            Assert.True(steps.MoveStep(0, 2));

            Assert.Equal(["b", "c", "a"], macro.Keystrokes.Select(TokenOf));
            Assert.Equal(1, changed);
        }

        /// <summary>
        /// The reason a row is a step <em>plus its delay</em>: moving one without the other would
        /// silently retime the macro.
        /// </summary>
        [Fact]
        public void MoveStep_CarriesTheStepsOwnDelayWithIt()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            steps.Load(macro);

            Assert.True(steps.MoveStep(0, 1));

            Assert.Equal(["b", "a", "d080"], macro.Keystrokes.Select(TokenOf));
        }

        [Theory]
        [InlineData(-1, 0)]
        [InlineData(0, 3)]
        [InlineData(1, 1)]
        public void MoveStep_OutsideTheListOrOntoItself_DoesNothing(int from, int to)
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");

            steps.Load(macro);

            Assert.False(steps.MoveStep(from, to));
            Assert.Equal(["a", "b", "c"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void MoveStepUpCommand_MovesTheSelectedStepAndFollowsIt()
        {
            var steps = Create();
            var macro = MacroOf("a", "b", "c");

            steps.Load(macro);
            steps.SelectStepCommand.Execute(steps.Items[2]);

            Assert.True(steps.MoveStepUpCommand.CanExecute(null));

            steps.MoveStepUpCommand.Execute(null);

            Assert.Equal(["a", "c", "b"], macro.Keystrokes.Select(TokenOf));

            // The selection follows the step, or a second ⌥↑ would move a different row.
            Assert.Equal(2, steps.SelectedStep!.Position);
            Assert.Equal("[c]", steps.SelectedStep.TokenText);
        }

        [Fact]
        public void MoveStepCommands_AtTheEndsOfTheList_CannotRun()
        {
            var steps = Create();

            steps.Load(MacroOf("a", "b"));

            steps.SelectStepCommand.Execute(steps.Items[0]);
            Assert.False(steps.MoveStepUpCommand.CanExecute(null));
            Assert.True(steps.MoveStepDownCommand.CanExecute(null));

            steps.SelectStepCommand.Execute(steps.Items[1]);
            Assert.True(steps.MoveStepUpCommand.CanExecute(null));
            Assert.False(steps.MoveStepDownCommand.CanExecute(null));
        }

        [Fact]
        public void MoveStepCommands_WithNothingSelected_CannotRun()
        {
            var steps = Create();

            steps.Load(MacroOf("a", "b"));

            Assert.False(steps.MoveStepUpCommand.CanExecute(null));
            Assert.False(steps.MoveStepDownCommand.CanExecute(null));
        }

        [Fact]
        public void RemoveStepCommand_DropsTheRowAndItsDelayTogether()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(80, TokenDialect.Gen1)!));
            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("b")));

            steps.Load(macro);
            steps.RemoveStepCommand.Execute(steps.Items[0]);

            Assert.Equal(["b"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void EditDelayCommand_OpensTheEditorSeededFromTheRow()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveCustom(120, TokenDialect.Gen1)!));

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);

            Assert.True(steps.IsDelayEditorOpen);
            Assert.Equal(120, steps.CustomDelayMilliseconds);
            Assert.False(steps.IsRandomDelay);
        }

        [Fact]
        public void EditDelayCommand_OnARowCarryingTheRandomDelay_ChecksTheRandomRadio()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key("a")));
            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);

            Assert.True(steps.IsRandomDelay);
        }

        [Fact]
        public void CustomDelayMilliseconds_WhenTyped_UnchecksTheRandomRadio()
        {
            var steps = OpenDelayEditor();

            steps.IsRandomDelay = true;
            steps.CustomDelayMilliseconds = 250;

            Assert.False(steps.IsRandomDelay);
        }

        [Fact]
        public void ApplyDelay_WithNothingChosen_ReportsTheSpecMessageAndWritesNothing()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, steps.DelayError);
            Assert.True(steps.IsDelayEditorOpen);
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void ApplyDelay_WithACustomDelayOutsideTheRange_ReportsTheSpecMessage(int delay)
        {
            var steps = OpenDelayEditor(out var macro);

            steps.CustomDelayMilliseconds = delay;
            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(MacroInspectorStepsViewModel.InvalidDelayMessage, steps.DelayError);
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void ApplyDelay_WithTheRandomRadio_WritesTheRandomDelayKey()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.IsRandomDelay = true;
            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(["a", MacroDelayTokens.RandomToken], macro.Keystrokes.Select(TokenOf));
            Assert.Equal("random", steps.Items[0].DelayText);
        }

        [Theory]
        [InlineData(1, "d001")]
        [InlineData(50, "d050")]
        [InlineData(250, "d250")]
        [InlineData(999, "d999")]
        public void ApplyDelay_WithACustomDelay_WritesTheZeroPaddedDelayKey(int delay, string token)
        {
            var steps = OpenDelayEditor(out var macro);

            steps.CustomDelayMilliseconds = delay;
            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(["a", token], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void ApplyDelay_Twice_ReplacesTheDelayRatherThanAddingASecond()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.CustomDelayMilliseconds = 50;
            steps.ApplyDelayCommand.Execute(null);

            steps.CustomDelayMilliseconds = 120;
            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(["a", "d120"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void ApplyDelay_WithTheRandomRadioOverAStaleCustomValue_StillWritesTheRandomKey()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.CustomDelayMilliseconds = 250;
            steps.IsRandomDelay = true;
            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(["a", MacroDelayTokens.RandomToken], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void ClearDelay_TakesTheDelayOffTheStep()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.CustomDelayMilliseconds = 50;
            steps.ApplyDelayCommand.Execute(null);
            steps.ClearDelayCommand.Execute(null);

            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
            Assert.False(steps.Items[0].HasDelay);
        }

        [Fact]
        public void ClearDelay_OnADelayOnlyRow_DropsTheRow()
        {
            var steps = Create();
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);
            steps.ClearDelayCommand.Execute(null);

            Assert.Empty(macro.Keystrokes);
            Assert.False(steps.IsDelayEditorOpen);
        }

        [Fact]
        public void IncreaseDelayCommand_AtTheMaximum_StaysClamped()
        {
            var steps = OpenDelayEditor();

            steps.CustomDelayMilliseconds = 999;
            steps.IncreaseDelayCommand.Execute(null);

            Assert.Equal(999, steps.CustomDelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_FromAnEmptyField_LandsOnTheMinimum()
        {
            var steps = OpenDelayEditor();

            steps.DecreaseDelayCommand.Execute(null);

            Assert.Equal(1, steps.CustomDelayMilliseconds);
            Assert.Equal(1, steps.MinimumDelayMilliseconds);
            Assert.Equal(999, steps.MaximumDelayMilliseconds);
        }

        [Fact]
        public void CloseDelayCommand_AfterPickingADelay_WritesNothing()
        {
            var steps = OpenDelayEditor(out var macro);

            steps.IsRandomDelay = true;
            steps.CloseDelayCommand.Execute(null);

            Assert.False(steps.IsDelayEditorOpen);
            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        /// <summary>
        /// §11.3's own contract: a delay the panel produced must survive being written to the layout
        /// file and read back (06 §2.2), both as text and as model content.
        /// </summary>
        [Fact]
        public void AppliedDelays_RoundTrippedThroughTheLayoutFile_SurviveUnchanged()
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();
            var steps = Create();

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);
            steps.CustomDelayMilliseconds = 250;
            steps.ApplyDelayCommand.Execute(null);

            macro.AddKeystroke(new Keystroke(MacroDelayTokens.ResolveRandom(TokenDialect.Gen1)!));

            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal(["{q}>{s5}{x1}{a}{d250}{dran}"], lines);
            Assert.True(macro.IsEquivalentTo(roundTripped));
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
        }

        /// <summary>
        /// The known 125 ms / 500 ms asymmetry of 05 §3.12 and 06 §2.2, pinned rather than hidden:
        /// <see cref="MacroDelayTokens.ResolveCustom"/> resolves those two by token and the legacy
        /// rows 10007/10008 shadow their generated twins, while the RGB/TKO parser reads the same
        /// text back as the generated codes 10085 + N. The <b>file text</b> is identical in both
        /// directions — which is the interoperability contract — but the key-table identity is not.
        /// </summary>
        [Theory]
        [InlineData(125)]
        [InlineData(500)]
        public void AppliedDelay_AtALegacyFixedValueOnTheRgb_KeepsItsTextButNotItsKeyIdentity(int delay)
        {
            var parsed = ParseRgb("{q}>{a}");
            var macro = parsed.Layout.EnumerateMacros().Single();
            var steps = Create();

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);
            steps.CustomDelayMilliseconds = delay;
            steps.ApplyDelayCommand.Execute(null);

            var picked = macro.Keystrokes[^1].Key;
            var lines = LayoutFileSerializer.Serialize(parsed.Layout, parsed.InvalidLines);
            var reparsed = new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
            var roundTripped = reparsed.Layout.EnumerateMacros().Single();

            Assert.Equal([$"{{q}}>{{s5}}{{x1}}{{a}}{{d{delay}}}"], lines);
            Assert.Equal(lines, LayoutFileSerializer.Serialize(reparsed.Layout, reparsed.InvalidLines));
            Assert.NotEqual(picked.Code, roundTripped.Keystrokes[^1].Key.Code);
            Assert.Equal(10085 + delay, roundTripped.Keystrokes[^1].Key.Code);
        }

        [Fact]
        public void Delays_OnAnUngatedBoard_AreAvailable()
        {
            var steps = Create(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 0));

            Assert.True(steps.AreDelaysAvailable);
            Assert.Equal(string.Empty, steps.DelayUnavailableReason);
            Assert.False(steps.CanUpdateFirmware);
        }

        [Fact]
        public void Delays_OnAFreestyleAtTheGate_AreAvailable()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 340));

            Assert.True(steps.AreDelaysAvailable);
        }

        [Fact]
        public void Delays_InDemoMode_BypassTheGate()
        {
            var steps = Create(DeviceId.FreestyleEdge, new FirmwareState { IsDemoMode = true });

            Assert.True(steps.AreDelaysAvailable);
        }

        /// <summary>
        /// The gate is answered <b>in place</b> now, not as a message box: a modal refusal for a
        /// surface already on screen would interrupt nothing.
        /// </summary>
        [Fact]
        public void Delays_OnAFreestyleBelowTheGate_RefuseInPlaceWithTheSpecMessage()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            Assert.False(steps.AreDelaysAvailable);
            Assert.Equal(MacroInspectorStepsViewModel.FirmwareRefusalMessage, steps.DelayUnavailableReason);
            Assert.True(steps.CanUpdateFirmware);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, steps.UpdateFirmwareCaption);
        }

        [Fact]
        public void ApplyDelay_BelowTheFirmwareGate_WritesNothing()
        {
            var macro = MacroOf("a");
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);

            steps.CustomDelayMilliseconds = 250;

            Assert.False(steps.ApplyDelayCommand.CanExecute(null));

            steps.ApplyDelayCommand.Execute(null);

            Assert.Equal(["a"], macro.Keystrokes.Select(TokenOf));
        }

        [Fact]
        public void UpdateFirmwareCommand_WhenTheGateRefuses_OpensTheDevicesSupportPage()
        {
            var steps = Create(DeviceId.FreestyleEdge, Firmware(1, 0, 339));

            steps.UpdateFirmwareCommand.Execute(null);

            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdge),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        /// <summary>Drift guard against the gate row that stores the same refusal (09 §2).</summary>
        [Fact]
        public void FirmwareRefusalMessage_MatchesTheGateRowThatStoresIt()
        {
            Assert.Equal(
                MacroInspectorStepsViewModel.FirmwareRefusalMessage,
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays)!.Message);
        }

        private MacroInspectorStepsViewModel Create()
        {
            return Create(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 100));
        }

        private MacroInspectorStepsViewModel Create(DeviceId deviceId, FirmwareState firmware)
        {
            return new MacroInspectorStepsViewModel(deviceId, firmware, _urlLauncher);
        }

        private MacroInspectorStepsViewModel OpenDelayEditor()
        {
            return OpenDelayEditor(out _);
        }

        private MacroInspectorStepsViewModel OpenDelayEditor(out Macro macro)
        {
            var steps = Create();

            macro = MacroOf("a");

            steps.Load(macro);
            steps.EditDelayCommand.Execute(steps.Items[0]);

            return steps;
        }

        private static Macro MacroOf(params string[] tokens)
        {
            var macro = new Macro();

            foreach (var token in tokens)
            {
                macro.AddKeystroke(new Keystroke(TestLayouts.Gen1Key(token)));
            }

            return macro;
        }

        private static string TokenOf(Keystroke keystroke)
        {
            return keystroke.Key.GetToken(TokenDialect.Gen1);
        }

        private static LayoutParseResult ParseRgb(params string[] lines)
        {
            return new LayoutFileParser(DeviceId.FreestyleEdgeRgb).Parse(lines);
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }
    }
}
