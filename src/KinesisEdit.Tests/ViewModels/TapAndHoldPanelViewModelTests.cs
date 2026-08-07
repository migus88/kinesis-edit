using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's Tap &amp; hold panel (mockup <c>2h</c>, specs/11-feature-dialogs.md
    /// §11.1): the mockup's own wording, the two independent captures, the bare modifier that
    /// records as a hold, the capability-supplied delay and its clamped steps, §11.1's three
    /// validation messages, the four pre-dialog checks and the firmware gate of 09 §2 — all of it
    /// ported from <c>TapAndHoldOverlayViewModelTests</c>, whose behaviour this panel keeps.
    /// <para>
    /// What the port changed, and why, is stated on each test that changed. The one behavioural
    /// difference is that the rail is <b>not modal</b>: an unarmed panel no longer swallows a
    /// keystroke, because the board beside it is live.
    /// </para>
    /// </summary>
    public sealed class TapAndHoldPanelViewModelTests
    {
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Strings_MatchTheSpecAndTheMockupVerbatim()
        {
            // §11.1's wording where the spec has one, mockup 2h's where the design replaced it.
            Assert.Equal("Assign Tap and Hold Action", TapAndHoldPanelViewModel.FeatureTitle);
            Assert.Equal("Tap — a quick press sends", TapAndHoldPanelViewModel.TapFieldLabel);
            Assert.Equal("Hold — past the delay it sends", TapAndHoldPanelViewModel.HoldFieldLabel);
            Assert.Equal(
                "A bare modifier is recordable as a hold — tap-alone and held-in-combo are captured as different things.",
                TapAndHoldPanelViewModel.CaptureRule);
            Assert.Equal("Record", TapAndHoldPanelViewModel.RecordCaption);
            Assert.Equal("Delay", TapAndHoldPanelViewModel.DelayLabel);
            Assert.Equal("default {0} · this device", TapAndHoldPanelViewModel.DelayDefaultFormat);
            Assert.Equal("Search for tokens", TapAndHoldPanelViewModel.SearchHint);
            Assert.Equal("Tap action is not sent until key is released.", TapAndHoldPanelViewModel.NoteText);
            Assert.Equal(
                "Designate the action sent when the key is tapped and released faster than the delay",
                TapAndHoldPanelViewModel.TapActionHint);
            Assert.Equal(
                "Designate the action sent when the key is held longer than the delay",
                TapAndHoldPanelViewModel.HoldActionHint);
            Assert.Equal(
                "Designate the time interval used to differentiate between the Tap and Hold actions",
                TapAndHoldPanelViewModel.DelayHint);
            Assert.Equal(
                "Please select a timing delay between 1ms and 999ms.",
                TapAndHoldPanelViewModel.InvalidDelayMessage);
            Assert.Equal("Please select a Tap Action", TapAndHoldPanelViewModel.MissingTapActionMessage);
            Assert.Equal("Please select a Hold Action", TapAndHoldPanelViewModel.MissingHoldActionMessage);
        }

        /// <summary>
        /// The record buttons read <c>● Record</c> in mockup <c>2h</c> and cannot: U+25CF is in
        /// neither embedded IBM Plex family, so a caption carrying it would draw as tofu. The dot is
        /// geometry in the view and the caption is the word alone — pinned here so nobody puts it
        /// back.
        /// </summary>
        [Fact]
        public void TheRecordCaption_CarriesNoBulletGlyph()
        {
            Assert.DoesNotContain('●', TapAndHoldPanelViewModel.RecordCaption);
        }

        [Fact]
        public void ThePanel_IsTheTapAndHoldSlotAndNamesItselfAsItsTabDoes()
        {
            var panel = CreatePanel();

            Assert.Equal(KeyInspectorMode.TapAndHold, panel.Mode);
            Assert.Equal(KeyInspectorTabViewModel.TapAndHoldCaption, panel.Title);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, 250)]
        [InlineData(DeviceId.Advantage360, 150)]
        public void Refresh_OnAnySupportingDevice_OpensAtTheCapabilitysDefaultDelay(DeviceId deviceId, int expected)
        {
            // Ported: the overlay read the capability in its constructor, the panel reads it from
            // the layout it is refreshed with — the panel outlives any one device's profile load.
            var scene = Scene.ForDevice(deviceId);
            var panel = CreatePanel(deviceId);

            scene.Refresh(panel);

            Assert.Equal(expected, panel.DelayMilliseconds);
            Assert.Equal(expected, panel.DefaultDelayMilliseconds);
            Assert.Equal(1, panel.MinimumDelayMilliseconds);
            Assert.Equal(999, panel.MaximumDelayMilliseconds);
        }

        /// <summary>The slider's caption names the device's own number, never a literal 250.</summary>
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "default 250 · this device")]
        [InlineData(DeviceId.Advantage360, "default 150 · this device")]
        public void DelayDefaultCaption_NamesTheDevicesOwnDefault(DeviceId deviceId, string expected)
        {
            var scene = Scene.ForDevice(deviceId);
            var panel = CreatePanel(deviceId);

            scene.Refresh(panel);

            Assert.Equal(expected, panel.DelayDefaultCaption);
            Assert.True(panel.HasDelayDefaultCaption);
        }

        [Fact]
        public void Refresh_OnAKeyThatAlreadyHasATapAndHold_OpensOnItsAssignment()
        {
            var scene = new Scene();

            scene.Key.SetTapAndHold(Gen1("a"), Gen1("lctrl"), 300);

            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.Same(scene.Key.TapAction, panel.TapAction);
            Assert.Same(scene.Key.HoldAction, panel.HoldAction);
            Assert.Equal(300, panel.DelayMilliseconds);
            Assert.Equal("300 ms", panel.DelayReadout);
        }

        /// <summary>
        /// Mockup <c>2h</c> draws the fields as <c>[j]</c> and <c>[lctrl]</c> — the bracketed file
        /// token, which is what makes them mono, and what the rail's assignment line above them
        /// already says. The overlay this panel replaces showed the cap's friendly caption, which on
        /// a stacked legend is two lines and wrapped the field to double height in a 268 px rail.
        /// </summary>
        [Fact]
        public void TheFields_SpellTheirActionAsTheFileDoes_NotAsTheCapDoes()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));
            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("lctrl"));

            Assert.Equal("[a]", panel.TapActionText);
            Assert.Equal("[lctrl]", panel.HoldActionText);
            Assert.DoesNotContain('\n', panel.HoldActionText);
        }

        [Fact]
        public void WantsKeystrokes_WithNoArmedField_IsFalse()
        {
            var panel = CreateOpenPanel();

            Assert.False(panel.WantsKeystrokes);
            Assert.False(panel.IsRecording);
            Assert.Equal(TapAndHoldField.None, panel.ArmedField);
        }

        [Fact]
        public void ReceiveKeystroke_WhileTheTapFieldIsArmed_FillsItAndDisarms()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);

            Assert.True(panel.WantsKeystrokes);
            Assert.True(panel.IsRecording);

            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.Same(Gen1("a"), panel.TapAction);
            Assert.Null(panel.HoldAction);
            Assert.False(panel.WantsKeystrokes);
            Assert.NotEmpty(panel.TapActionText);
        }

        [Fact]
        public void ReceiveKeystroke_WhileTheHoldFieldIsArmed_FillsOnlyThatField()
        {
            // "A bare modifier is recordable as a hold" — mockup 2h's own sentence, and the point
            // of two independent captures: lctrl alone is a legal hold action.
            var panel = CreateOpenPanel();

            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("lctrl"));

            Assert.Same(Gen1("lctrl"), panel.HoldAction);
            Assert.Null(panel.TapAction);
        }

        [Fact]
        public void ArmingOneField_DisarmsTheOther_SoAKeystrokeCanNeverLandInBoth()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.Null(panel.TapAction);
            Assert.Same(Gen1("a"), panel.HoldAction);
        }

        /// <summary>
        /// New with the rail: the record button is a toggle, because a recording started by accident
        /// beside a live board must be cancellable without pressing a key — the modal had Cancel for
        /// that and the rail has no Cancel.
        /// </summary>
        [Fact]
        public void ArmingTheSameFieldTwice_StandsTheCaptureDown()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.ArmTapActionCommand.Execute(null);

            Assert.False(panel.IsRecording);
            Assert.Equal(TapAndHoldField.None, panel.ArmedField);
        }

        /// <summary>
        /// <b>The rail is not modal, so an unarmed panel takes nothing.</b> The overlay swallowed
        /// every keystroke while it was merely open (spec 10's own wording for a modal); a rail sits
        /// beside a live board, and a panel that ate the keypress would steal the remap the user was
        /// recording on the cap next to it.
        /// </summary>
        [Fact]
        public void ReceiveKeystroke_WithNothingArmed_IsIgnored()
        {
            var panel = CreateOpenPanel();

            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.Null(panel.TapAction);
            Assert.Null(panel.HoldAction);
        }

        [Fact]
        public void Deactivate_AfterArmingAField_StopsTheCaptureAndWritesNothing()
        {
            // Replaces the overlay's "after the overlay closed, a keystroke is ignored": the rail
            // puts a panel down with Deactivate rather than closing it.
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.ArmTapActionCommand.Execute(null);
            panel.Deactivate();
            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.False(panel.WantsKeystrokes);
            Assert.False(panel.IsRecording);
            Assert.Null(panel.TapAction);
            Assert.False(scene.Key.IsTapAndHold);
        }

        /// <summary>
        /// <see cref="KeyInspectorPanelViewModel.IsRecording"/> is what the editor folds into
        /// <c>IsCaptureActive</c> — a panel that recorded without announcing it would have ⌘S fire
        /// while a hold action was being captured.
        /// </summary>
        [Fact]
        public void RecordingChanged_IsRaisedInBothDirections()
        {
            var panel = CreateOpenPanel();
            var raised = 0;

            panel.RecordingChanged += (_, _) => raised++;

            panel.ArmTapActionCommand.Execute(null);

            Assert.Equal(1, raised);

            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.Equal(2, raised);
            Assert.False(panel.IsRecording);
        }

        /// <summary>
        /// The panel raises no "I took that keystroke" signal of its own, and must not: the editor's
        /// router latches the Escape flag <em>before</em> it dispatches to any sink, so a second
        /// answer here would be a second thing to keep in step
        /// (docs/app/keyboard-editor.md, "Escape — the resolution"). What the panel owes is an
        /// honest <see cref="TapAndHoldPanelViewModel.WantsKeystrokes"/>, which is what the router
        /// reads.
        /// </summary>
        [Fact]
        public void ReceiveKeystroke_TakesTheKeyOnlyWhileAFieldIsArmed()
        {
            var panel = CreateOpenPanel();

            panel.ReceiveKeystroke(Keystroke("a"));

            Assert.Null(panel.TapAction);
            Assert.Null(panel.HoldAction);
            Assert.False(panel.WantsKeystrokes);

            panel.ArmHoldActionCommand.Execute(null);

            Assert.True(panel.WantsKeystrokes);

            panel.ReceiveKeystroke(Keystroke("lctrl"));

            Assert.Same(Gen1("lctrl"), panel.HoldAction);
            Assert.False(panel.WantsKeystrokes);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void Assign_WithADelayOutsideTheRange_ReportsTheSpecMessageAndWritesNothing(int delay)
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.DelayMilliseconds = delay;
            panel.AssignCommand.Execute(null);

            Assert.Equal(TapAndHoldPanelViewModel.InvalidDelayMessage, panel.ValidationMessage);
            Assert.True(panel.HasValidationMessage);
            Assert.False(scene.Key.IsTapAndHold);
        }

        /// <summary>
        /// Direct assignment is deliberately unclamped, which is the only way an out-of-range delay
        /// can survive long enough to produce §11.1's message. The <em>slider</em> is clamped for
        /// display, so a file written by older firmware is neither hidden nor silently rewritten.
        /// </summary>
        [Fact]
        public void DelayMilliseconds_TakesAnOutOfRangeValue_AndOnlyTheSliderClampsIt()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.DelayMilliseconds = 5000;

            Assert.Equal(5000, panel.DelayMilliseconds);
            Assert.Equal("5000 ms", panel.DelayReadout);
            Assert.Equal(999, panel.DelaySliderValue);
        }

        [Fact]
        public void Assign_WithNoTapAction_ReportsTheSpecMessage()
        {
            var panel = CreateOpenPanel();

            panel.AssignCommand.Execute(null);

            Assert.Equal(TapAndHoldPanelViewModel.MissingTapActionMessage, panel.ValidationMessage);
        }

        [Fact]
        public void Assign_WithNoHoldAction_ReportsTheSpecMessage()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));
            panel.AssignCommand.Execute(null);

            Assert.Equal(TapAndHoldPanelViewModel.MissingHoldActionMessage, panel.ValidationMessage);
        }

        [Fact]
        public void Assign_WithBothActions_WritesTheTapAndHoldToTheKeyAndAnnouncesIt()
        {
            var scene = new Scene();
            var panel = CreatePanel();
            var assigned = 0;

            scene.Refresh(panel);

            panel.Assigned += (_, _) => assigned++;

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));
            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("lctrl"));
            panel.DelayMilliseconds = 300;
            panel.AssignCommand.Execute(null);

            Assert.True(scene.Key.IsTapAndHold);
            Assert.Same(Gen1("a"), scene.Key.TapAction);
            Assert.Same(Gen1("lctrl"), scene.Key.HoldAction);
            Assert.Equal(300, scene.Key.TimingDelay);
            Assert.Equal(1, assigned);
            Assert.False(panel.HasValidationMessage);
        }

        /// <summary>
        /// Replaces the overlay's "Cancel leaves the key untouched": there is no Cancel in the rail,
        /// and the equivalent guarantee is that filling the fields writes nothing on its own.
        /// </summary>
        [Fact]
        public void FillingTheFields_WithoutAssigning_LeavesTheKeyUntouched()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));
            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("lctrl"));

            Assert.False(scene.Key.IsTapAndHold);
        }

        [Fact]
        public void IncreaseDelayCommand_AtTheMaximum_StaysClamped()
        {
            var panel = CreateOpenPanel();

            panel.DelayMilliseconds = 999;
            panel.IncreaseDelayCommand.Execute(null);

            Assert.Equal(999, panel.DelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_AtTheMinimum_StaysClamped()
        {
            var panel = CreateOpenPanel();

            panel.DelayMilliseconds = 1;
            panel.DecreaseDelayCommand.Execute(null);

            Assert.Equal(1, panel.DelayMilliseconds);
        }

        [Fact]
        public void DecreaseDelayCommand_FromAnOutOfRangeValue_ClampsBackIntoTheRange()
        {
            var panel = CreateOpenPanel();

            panel.DelayMilliseconds = 5000;
            panel.DecreaseDelayCommand.Execute(null);

            Assert.Equal(999, panel.DelayMilliseconds);
        }

        /// <summary>
        /// The rail nests nothing, so §11.1's two <c>Search</c> actions open the shared picker
        /// <em>inside</em> this panel — no second modal, and therefore no
        /// <c>EditorOverlayHost.ShowNested</c>. Arming stands down with it: the picker's query box
        /// is a real <c>TextBox</c>, which suspends capture the moment it takes focus.
        /// </summary>
        [Fact]
        public void SearchTapActionCommand_OpensThePickerInPlaceAndDisarmsCapture()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.SearchTapActionCommand.Execute(null);

            Assert.True(panel.IsPickerOpen);
            Assert.Equal(TapAndHoldPanelViewModel.TapFieldLabel, panel.PickerFieldLabel);
            Assert.False(panel.WantsKeystrokes);
        }

        [Fact]
        public void SearchHoldActionCommand_WhenARowIsTaken_WritesTheActionIntoTheHoldFieldAndCloses()
        {
            var panel = CreateOpenPanel();

            panel.SearchHoldActionCommand.Execute(null);

            Assert.True(panel.IsPickerOpen);
            Assert.Equal(TapAndHoldPanelViewModel.HoldFieldLabel, panel.PickerFieldLabel);

            Choose(panel, Gen1("lctrl"));

            Assert.False(panel.IsPickerOpen);
            Assert.Same(Gen1("lctrl"), panel.HoldAction);
            Assert.Null(panel.TapAction);

            // Nothing was written: a pick fills a field, and only Assign touches the model.
            Assert.True(panel.Picker.Recent.Contains(Gen1("lctrl")));
        }

        [Fact]
        public void ThePicker_WhenCancelled_LeavesBothFieldsAlone()
        {
            var panel = CreateOpenPanel();

            panel.AssignAction(TapAndHoldField.Hold, Gen1("lctrl"));
            panel.SearchHoldActionCommand.Execute(null);
            panel.CloseSearchCommand.Execute(null);

            Assert.False(panel.IsPickerOpen);
            Assert.Same(Gen1("lctrl"), panel.HoldAction);
        }

        [Fact]
        public void ThePicker_WhenThePanelIsStoodDown_ClosesWithIt()
        {
            var panel = CreateOpenPanel();

            panel.SearchTapActionCommand.Execute(null);

            Assert.True(panel.IsPickerOpen);

            panel.Deactivate();

            Assert.False(panel.IsPickerOpen);
        }

        /// <summary>Takes <paramref name="definition"/>'s row in the panel's own picker.</summary>
        private static void Choose(TapAndHoldPanelViewModel panel, KeyDefinition definition)
        {
            foreach (var row in panel.Picker.Rows)
            {
                if (row.Definition.Code == definition.Code)
                {
                    panel.Picker.ChooseCommand.Execute(row);

                    return;
                }
            }

            throw new InvalidOperationException($"The picker lists no row for key code {definition.Code}.");
        }

        [Fact]
        public void Refresh_OnAKeyThatPassesEveryCheck_IsAvailable()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.True(panel.IsAvailable);
            Assert.Empty(panel.UnavailableReason);
        }

        /// <summary>
        /// Ported from <c>TryCreate_OnAnAlphanumericTopLayerKey_ReturnsTheSpecRefusal</c>: the rail
        /// has no "try to open", so the fourth pre-dialog check of §11.1 lands on
        /// <see cref="KeyInspectorPanelViewModel.UnavailableReason"/> instead — the panel refuses
        /// politely rather than disappearing.
        /// </summary>
        [Fact]
        public void Refresh_OnAnAlphanumericTopLayerKey_RefusesWithTheSpecMessage()
        {
            var scene = new Scene(keyIndex: 1);
            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.False(panel.IsAvailable);
            Assert.Equal(
                "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.",
                panel.UnavailableReason);
        }

        /// <summary>
        /// A refusal that is <b>not</b> the firmware gate's offers no firmware update. The frame
        /// caught it: a profile at 11 of 10 refuses through §11.1's second pre-dialog check, and the
        /// panel was offering to update perfectly current firmware.
        /// </summary>
        [Fact]
        public void Refresh_OnARefusalThatIsNotTheFirmwareGates_OffersNoFirmwareUpdate()
        {
            var scene = new Scene();

            scene.FillTapAndHolds(11);

            var panel = CreatePanel();

            scene.Refresh(panel, EditorAdvisories.Build(scene.Layout));

            Assert.False(panel.IsAvailable);
            Assert.Equal(
                "You have reached the maximum number of Tap and Hold actions for this Profile.",
                panel.UnavailableReason);
            Assert.False(panel.CanUpdateFirmware);
        }

        [Fact]
        public void Refresh_OnAMacroTriggerKey_RefusesWithTheSpecMessage()
        {
            var scene = new Scene();

            scene.Key.SetMacro(1, scene.Layout.CreateMacro());

            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.False(panel.IsAvailable);
            Assert.Equal(
                "You cannot assign a Tap and Hold Action to a macro trigger key.",
                panel.UnavailableReason);
        }

        [Fact]
        public void Refresh_WithNothingSelected_RefusesAndArmsNothing()
        {
            var panel = CreateOpenPanel();

            panel.ArmTapActionCommand.Execute(null);
            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.False(panel.IsAvailable);
            Assert.Equal(TapAndHoldPanelViewModel.NoSelectionMessage, panel.UnavailableReason);
            Assert.False(panel.IsRecording);
        }

        [Fact]
        public void Refresh_OnADeviceWithoutTapAndHold_RefusesWithTheAppsOwnWording()
        {
            var scene = Scene.WithoutTapAndHold();
            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.False(panel.IsAvailable);
            Assert.Equal(TapAndHoldPanelViewModel.DeviceUnsupportedMessage, panel.UnavailableReason);
        }

        /// <summary>The rail refreshes every panel it holds, not only the showing one.</summary>
        [Fact]
        public void Refresh_RepeatedOnTheSameKey_KeepsAHalfFilledFieldTheUserIsStillWorkingOn()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));

            scene.Refresh(panel);

            Assert.Same(Gen1("a"), panel.TapAction);
        }

        /// <summary>
        /// …but a position somebody else rewrote is re-read: a remap written from the Remap panel
        /// clears the tap-and-hold ("one rule per position"), and stale fields would offer to
        /// re-assign something the key no longer carries.
        /// </summary>
        [Fact]
        public void Refresh_AfterTheModelMovedUnderneath_ReReadsTheFields()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Key.SetTapAndHold(Gen1("a"), Gen1("lctrl"), 300);
            scene.Refresh(panel);

            Assert.Same(Gen1("a"), panel.TapAction);

            scene.Key.Remap(Gen1("esc"));
            scene.Refresh(panel);

            Assert.Null(panel.TapAction);
            Assert.Null(panel.HoldAction);
            Assert.Equal(250, panel.DelayMilliseconds);
        }

        /// <summary>Refresh re-reads; it never writes (the panel contract's own words).</summary>
        [Fact]
        public void Refresh_WritesNothingToTheModel()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);
            scene.Refresh(panel);

            Assert.False(scene.Key.IsTapAndHold);
            Assert.False(scene.Key.IsModified);
        }

        /// <summary>
        /// The budget advisory is <b>read</b> off the set the rail hands over, never recomputed and
        /// never re-worded — two derivations of one finding are two things to disagree.
        /// </summary>
        [Fact]
        public void Refresh_OnAProfileOverTheTapAndHoldBudget_ShowsTheEditorsOwnAdvisory()
        {
            var scene = new Scene();

            scene.FillTapAndHolds(11);

            var advisories = EditorAdvisories.Build(scene.Layout);
            var panel = CreatePanel();

            scene.Refresh(panel, advisories);

            Assert.True(panel.HasBudgetAdvisory);
            Assert.Equal(AdvisoryText.TapAndHoldCount(11, 10), panel.BudgetAdvisory);
        }

        [Fact]
        public void Refresh_OnACleanProfile_ShowsNoBudgetAdvisory()
        {
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel, EditorAdvisories.Build(scene.Layout));

            Assert.False(panel.HasBudgetAdvisory);
            Assert.Empty(panel.BudgetAdvisory);
        }

        /// <summary>
        /// A duplicate-key advisory on the very position being edited must not be mistaken for the
        /// budget one: the budget advisory is the only Layout-tab advisory that names neither a
        /// layer nor a position.
        /// </summary>
        [Fact]
        public void Refresh_WithOnlyPerKeyAdvisories_ShowsNoBudgetAdvisory()
        {
            var scene = new Scene();

            scene.Layer.Keys[1].Remap(Gen1("esc"));

            var panel = CreatePanel();

            scene.Refresh(panel, EditorAdvisories.Build(scene.Layout));

            Assert.False(panel.HasBudgetAdvisory);
        }

        /// <summary>
        /// <c>KeyboardKey.SetTapAndHold</c> refuses a position that can never be remapped
        /// (specs/05-key-model.md §5.3), and the panel honours that answer rather than reporting a
        /// success with nothing written.
        /// </summary>
        [Fact]
        public void Assign_WhenTheKeyRefusesTheAssignment_ReportsItAndWritesNothing()
        {
            var scene = Scene.Locked();
            var panel = CreatePanel();

            scene.Refresh(panel);

            Assert.False(scene.Key.CanEdit);

            panel.ArmTapActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("a"));
            panel.ArmHoldActionCommand.Execute(null);
            panel.ReceiveKeystroke(Keystroke("lctrl"));
            panel.AssignCommand.Execute(null);

            Assert.Equal(TapAndHoldPanelViewModel.LockedKeyMessage, panel.ValidationMessage);
            Assert.False(scene.Key.IsTapAndHold);
        }

        [Fact]
        public void Refresh_BelowTheFirmwareGate_RefusesInlineWithTheGatesOwnWordingAndOffersTheUpdate()
        {
            var scene = new Scene();
            var panel = CreatePanel(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 0));

            scene.Refresh(panel);

            Assert.False(panel.IsAvailable);
            Assert.Equal(TapAndHoldPanelViewModel.FirmwareRefusalMessage, panel.UnavailableReason);
            Assert.True(panel.CanUpdateFirmware);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, panel.UpdateFirmwareCaption);

            panel.UpdateFirmwareCommand.Execute(null);

            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdgeRgb),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        [Fact]
        public void Refresh_AtTheFirmwareGate_IsAvailableAndOffersNoUpdate()
        {
            var scene = new Scene();
            var panel = CreatePanel(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 1));

            scene.Refresh(panel);

            Assert.True(panel.IsAvailable);
            Assert.False(panel.CanUpdateFirmware);
        }

        [Fact]
        public void Refresh_InDemoMode_BypassesTheGate()
        {
            var scene = new Scene();
            var panel = CreatePanel(DeviceId.FreestyleEdgeRgb, new FirmwareState { IsDemoMode = true });

            scene.Refresh(panel);

            Assert.True(panel.IsAvailable);
        }

        [Fact]
        public void Refresh_BelowTheFirmwareGate_StandsAnArmedCaptureDown()
        {
            // A gate that closes under an armed field must not leave the app capturing for a panel
            // that can no longer write anything.
            var scene = new Scene();
            var panel = CreatePanel();

            scene.Refresh(panel);

            panel.ArmTapActionCommand.Execute(null);

            var gated = CreatePanel(DeviceId.FreestyleEdgeRgb, Firmware(1, 0, 0));

            scene.Refresh(gated);

            Assert.False(gated.IsRecording);
            Assert.False(gated.AssignCommand.CanExecute(null));
        }

        [Fact]
        public void FirmwareRefusalFor_OnAFreestyleBoard_UsesTheGateRowsOwnMessage()
        {
            Assert.Equal(
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)!.Message,
                TapAndHoldPanelViewModel.FirmwareRefusalFor(DeviceId.FreestyleEdge));
        }

        /// <summary>
        /// Drift guard: the fallback used for the gate rows that carry no message (Advantage2, Edge
        /// RGB) must stay the wording the Freestyle row stores, since §11.1 quotes one refusal for
        /// every app.
        /// </summary>
        [Fact]
        public void FirmwareRefusalMessage_MatchesTheGateRowThatStoresIt()
        {
            Assert.Equal(
                TapAndHoldPanelViewModel.FirmwareRefusalMessage,
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)!.Message);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WithFirmwareAtTheGate_ShowsNothingAndAllows()
        {
            var allowed = await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 1),
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_InDemoMode_BypassesTheGate()
        {
            var allowed = await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                new FirmwareState { IsDemoMode = true },
                _notifications,
                _urlLauncher);

            Assert.True(allowed);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_BelowTheGate_RefusesWithTheSpecMessageAndTheUpdateButton()
        {
            var allowed = await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            var request = Assert.Single(_notifications.MessageBoxes);
            var button = Assert.Single(request.CustomButtons);

            Assert.False(allowed);
            Assert.Equal(TapAndHoldPanelViewModel.FeatureTitle, request.Title);
            Assert.Equal(TapAndHoldPanelViewModel.FirmwareRefusalMessage, request.Message);
            Assert.Equal(FirmwareFeatureGate.UpdateFirmwareButtonCaption, button.Caption);
            Assert.Empty(_urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WhenUpdateFirmwareIsPressed_OpensTheDevicesSupportPage()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome
            {
                Result = MessageBoxResult.Custom,
                CustomButtonId = FirmwareFeatureGate.UpdateFirmwareButtonId
            };

            var allowed = await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            Assert.False(allowed);
            Assert.Equal(
                FirmwareSupportUrls.FindUrl(DeviceId.FreestyleEdgeRgb),
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_WhenTheDialogIsDismissed_OpensNothing()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Ok };

            await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdgeRgb,
                Firmware(1, 0, 0),
                _notifications,
                _urlLauncher);

            Assert.Empty(_urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task EnsureFirmwareAvailableAsync_OnAFreestyleBoard_UsesTheGatesOwnMessage()
        {
            await TapAndHoldPanelViewModel.EnsureFirmwareAvailableAsync(
                DeviceId.FreestyleEdge,
                Firmware(1, 0, 479),
                _notifications,
                _urlLauncher);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(
                FirmwareGateCatalog.Find(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)!.Message,
                request.Message);
        }

        private TapAndHoldPanelViewModel CreatePanel(
            DeviceId deviceId = DeviceId.FreestyleEdgeRgb,
            FirmwareState? firmware = null)
        {
            return new TapAndHoldPanelViewModel(
                deviceId,
                firmware ?? new FirmwareState { IsDemoMode = true },
                _urlLauncher);
        }

        /// <summary>A panel already refreshed onto a key that passes every check.</summary>
        private TapAndHoldPanelViewModel CreateOpenPanel()
        {
            var panel = CreatePanel();

            new Scene().Refresh(panel);

            return panel;
        }

        private static KeyDefinition Gen1(string token)
        {
            return TestLayouts.Gen1Key(token);
        }

        private static CapturedKeystroke Keystroke(string token)
        {
            return new CapturedKeystroke
            {
                Key = Gen1(token),
                PhysicalKey = PhysicalKeyCode.None
            };
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }

        /// <summary>
        /// A layout, its one layer's view model and a selected cap — everything
        /// <see cref="TapAndHoldPanelViewModel.Refresh"/> is pushed, built without an editor
        /// anywhere near it.
        /// </summary>
        private sealed class Scene
        {
            /// <summary>Twelve free positions: one to select, and eleven to spend on the budget.</summary>
            private static readonly string[] DefaultTokens =
            [
                "esc", "a", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
            ];

            public KeyboardLayout Layout { get; }

            public KeyboardLayer Layer => Layout.Layers[0];

            public KeyboardKey Key => KeyViewModel.Key;

            public KeyboardKeyViewModel KeyViewModel { get; }

            private readonly KeyboardLayerViewModel _layerViewModel;

            public Scene(int keyIndex = 0)
                : this(TestLayouts.CreateLayout(DefaultTokens), keyIndex)
            {
            }

            private Scene(KeyboardLayout layout, int keyIndex)
            {
                Layout = layout;

                var indexes = new int[layout.Layers[0].Keys.Count];

                for (var offset = 0; offset < indexes.Length; offset++)
                {
                    indexes[offset] = layout.Layers[0].Keys[offset].Index;
                }

                _layerViewModel = KeyboardLayerViewModel.BuildAll(
                    layout,
                    TestLayouts.CreateVisual(indexes),
                    lighting: null)[0];

                KeyViewModel = _layerViewModel.FindByIndex(keyIndex)
                    ?? throw new InvalidOperationException($"No cap at index {keyIndex}.");
            }

            /// <summary>
            /// The real board of <paramref name="deviceId"/>, for the questions that are about the
            /// device's catalog data rather than about one position.
            /// </summary>
            public static Scene ForDevice(DeviceId deviceId)
            {
                var layout = KeyboardLayout.Create(deviceId);

                return new Scene(layout, layout.Layers[0].Keys[0].Index);
            }

            public static Scene Locked()
            {
                return new Scene(TestLayouts.CreateLockedKeyLayout(), 1);
            }

            public static Scene WithoutTapAndHold()
            {
                return new Scene(TestLayouts.CreateLayoutWithoutTapAndHold(), 0);
            }

            /// <summary>
            /// Drives the profile over the Edge RGB's budget of ten (§11.1), never touching the
            /// selected position — the panel's own state must not be what produces the advisory.
            /// </summary>
            public void FillTapAndHolds(int count)
            {
                var assigned = 0;

                foreach (var key in Layer.Keys)
                {
                    if (assigned >= count)
                    {
                        return;
                    }

                    if (key.Index != KeyViewModel.Index
                        && key.SetTapAndHold(TestLayouts.Gen1Key("a"), TestLayouts.Gen1Key("lctrl"), 250))
                    {
                        assigned++;
                    }
                }
            }

            public void Refresh(TapAndHoldPanelViewModel panel, EditorAdvisories? advisories = null)
            {
                panel.Refresh(KeyViewModel, _layerViewModel, Layout, advisories ?? EditorAdvisories.Empty);
            }
        }
    }
}
