using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector rail: what its header says, that the modes are one exclusive slot and warn
    /// before replacing, that its footer runs the editor's own commands rather than second copies of
    /// them, and that it is not modal.
    /// </summary>
    public class KeyInspectorViewModelTests
    {
        [Fact]
        public void TheExclusivitySentence_IsMockup2AsTightenedWording()
        {
            // Pinned verbatim: `1e` writes the shorter "This key does one thing" and `2a` supersedes
            // it. Copy is final (docs/design/README.md), so a paraphrase here is a defect.
            Assert.Equal(
                "This key does one thing — picking another replaces it.",
                KeyInspectorViewModel.ExclusivitySentence);
        }

        [Fact]
        public void SelectingAKey_OpensTheRail_AndNamesThePositionByItsFactoryToken()
        {
            var scene = new Scene();

            // Position 20 is the "1" of the left typing half (specs/05-key-model.md §4.2).
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.True(scene.Inspector.IsOpen);
            Assert.Equal(KeyboardKeyViewModel.LeftHalfDescription, scene.Inspector.PositionDescription);
            Assert.True(scene.Inspector.HasPositionDescription);
            Assert.Equal("[1]", scene.Inspector.PositionToken);
            Assert.Equal("[1]", scene.Inspector.FactoryAssignmentText);
            Assert.Equal("[1]", scene.Inspector.CurrentAssignmentText);
        }

        [Fact]
        public void TheHeaderKeepsNamingTheFactoryToken_WhileTheAssignmentLineFollowsTheRemap()
        {
            // The whole point of showing both: the header identifies the *position* on the board,
            // which does not move when the key is remapped, and the line under it says what the
            // position does now.
            var scene = new Scene();
            var key = scene.Key(TestLayouts.RgbDigitOneKeyIndex);

            key.Key.Remap(TestLayouts.Gen1Key("esc"));
            key.RefreshFromModel();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal("[1]", scene.Inspector.PositionToken);
            Assert.Equal("[1]", scene.Inspector.FactoryAssignmentText);
            Assert.Equal("[esc]", scene.Inspector.CurrentAssignmentText);
        }

        [Fact]
        public void AKeyInTheRightHalf_IsNamedAsSuch()
        {
            var scene = new Scene();
            var right = scene.Layer.Keys.First(key => key.Section == 1);

            scene.Select(right.Index);

            Assert.Equal(KeyboardKeyViewModel.RightHalfDescription, scene.Inspector.PositionDescription);
        }

        [Fact]
        public void ABoardDrawnInOnePiece_NamesNoHalf()
        {
            // "Left half" on a board with one panel would be an invention; the header then reads
            // "[esc] position", which is still true.
            var layout = TestLayouts.CreateLayout("esc", "F1", "F2");
            var layer = KeyboardLayerViewModel.BuildAll(layout, TestLayouts.CreateVisual(0, 1, 2), lighting: null)[0];
            var inspector = new Scene().Inspector;

            inspector.Refresh(layer.Keys[0], layer, layout, EditorAdvisories.Empty);

            Assert.Equal(string.Empty, inspector.PositionDescription);
            Assert.False(inspector.HasPositionDescription);
        }

        [Fact]
        public void TheModeTabs_AreRemapTapAndHoldAndMacro_AndMultiModIsNotDrawnOnThisBoard()
        {
            // handoff.md:137 and issue #92: where the firmware lacks multi-modifiers the tab is not
            // rendered at all. Mockup `1e` draws it beside an "Advantage 360 only" note; the handoff
            // wins, and the deviation is recorded.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(
                new[] { KeyInspectorMode.Remap, KeyInspectorMode.TapAndHold, KeyInspectorMode.Macro },
                scene.Inspector.Tabs.Select(tab => tab.Mode));
            Assert.DoesNotContain(scene.Inspector.Tabs, tab => tab.Mode == KeyInspectorMode.MultiModifier);
        }

        [Fact]
        public void OnABoardWhoseFirmwareHasMultiModifiers_TheFourthTabIsDrawn()
        {
            var scene = new Scene();
            var key = new KeyboardKey(
                new KeyPosition(0, "d"),
                TestLayouts.Gen1Key("d"),
                supportsMultiModifiers: true);

            scene.Inspector.Refresh(TestLayouts.CreateKeyViewModel(key), scene.Layer, scene.Layout, EditorAdvisories.Empty);

            Assert.Contains(scene.Inspector.Tabs, tab => tab.Mode == KeyInspectorMode.MultiModifier);
        }

        [Fact]
        public void EveryModeTab_IsLiveOnAnOrdinaryPosition()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.All(scene.Inspector.Tabs, tab => Assert.True(tab.IsEnabled));
            Assert.All(scene.Inspector.Tabs, tab => Assert.Equal(tab.Caption, tab.DisplayCaption));
        }

        [Fact]
        public void SelectingAKey_OpensTheModeItAlreadyCarries()
        {
            var scene = new Scene();
            var key = scene.Key(TestLayouts.RgbDigitOneKeyIndex);

            key.Key.SetTapAndHold(TestLayouts.Gen1Key("1"), TestLayouts.Gen1Key("esc"), 250);
            key.RefreshFromModel();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(KeyInspectorMode.TapAndHold, scene.Inspector.SelectedMode);
            Assert.Equal(string.Empty, scene.Inspector.ModeSwitchWarning);
        }

        [Fact]
        public void SwitchingModeOnARemappedKey_WarnsBeforeItReplaces()
        {
            // Mockup 2h: "the panel says so at the point of switching rather than after". Core's
            // SetTapAndHold really does clear the remap, so the warning is a statement of fact.
            var scene = new Scene();
            var key = scene.Key(TestLayouts.RgbDigitOneKeyIndex);

            key.Key.Remap(TestLayouts.Gen1Key("esc"));
            key.RefreshFromModel();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(KeyInspectorMode.Remap, scene.Inspector.SelectedMode);
            Assert.False(scene.Inspector.HasModeSwitchWarning);

            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.TapAndHold));

            Assert.True(scene.Inspector.HasModeSwitchWarning);
            Assert.Contains(KeyInspectorViewModel.RemapNoun, scene.Inspector.ModeSwitchWarning, StringComparison.Ordinal);
            Assert.Equal(
                KeyInspectorViewModel.BuildModeSwitchWarning(KeyInspectorMode.Remap),
                scene.Inspector.ModeSwitchWarning);
        }

        [Fact]
        public void TheWarningNamesWhatIsActuallyThere_NotAlwaysTheRemap()
        {
            var scene = new Scene();
            var key = scene.Key(TestLayouts.RgbDigitOneKeyIndex);

            key.Key.SetTapAndHold(TestLayouts.Gen1Key("1"), TestLayouts.Gen1Key("esc"), 250);
            key.RefreshFromModel();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.Remap));

            Assert.Contains(KeyInspectorViewModel.TapAndHoldNoun, scene.Inspector.ModeSwitchWarning, StringComparison.Ordinal);
        }

        [Fact]
        public void SwitchingModeOnAnUntouchedKey_WarnsAboutNothing()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.TapAndHold));

            Assert.Equal(KeyInspectorMode.TapAndHold, scene.Inspector.SelectedMode);
            Assert.False(scene.Inspector.HasModeSwitchWarning);
        }

        [Fact]
        public void TheWarningNeverBlocks_ItOnlySays()
        {
            // Invariant: nothing in the rail refuses an edit. The warning is text; both footer
            // commands stay runnable while it is up.
            var scene = new Scene();
            var key = scene.Key(TestLayouts.RgbDigitOneKeyIndex);

            key.Key.Remap(TestLayouts.Gen1Key("esc"));
            key.RefreshFromModel();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.TapAndHold));

            Assert.True(scene.Inspector.HasModeSwitchWarning);
            Assert.True(scene.Inspector.ResetKeyCommand.CanExecute(null));
            Assert.True(scene.Inspector.CopyKeyCommand.CanExecute(null));
        }

        /// <summary>
        /// The Macro tab hosts a panel like every other mode since issue #93 — it used to bridge out
        /// to the Macros tab, and navigating away from the board when a mode tab is pressed was the
        /// placeholder, not the design.
        /// </summary>
        [Fact]
        public void TheMacroTab_HostsItsPanelAndWidensTheRail()
        {
            var panel = new WidePanel();
            var scene = new Scene([panel]);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Inspector.IsWide);

            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.Macro));

            Assert.Same(panel, scene.Inspector.ActivePanel);
            Assert.True(scene.Inspector.IsWide);
        }

        /// <summary>
        /// The rail is 268 px wide for every mode but the macro one, and the panel is what says so
        /// (docs/design/handoff.md § Geometry).
        /// </summary>
        [Fact]
        public void TheRail_IsNarrowForEveryPanelThatDoesNotAskForTheWideOne()
        {
            var scene = new Scene([new NarrowPanel()]);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.Macro));

            Assert.False(scene.Inspector.IsWide);
        }

        [Fact]
        public void ADeadTab_IsRefusedSilently()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var dead = KeyInspectorTabViewModel.Disabled(
                KeyInspectorMode.TapAndHold,
                KeyInspectorTabViewModel.NotWritableReason);

            scene.Inspector.SelectModeCommand.Execute(dead);

            Assert.Equal(KeyInspectorMode.Remap, scene.Inspector.SelectedMode);
        }

        [Fact]
        public void TheFooterRunsTheEditorsOwnCommands_NotSecondCopiesOfThem()
        {
            // `Revert key` is ResetKeyCommand and `Copy to…` is CopyKeyCommand — a second reset path
            // or a second copy path would be a second set of refusals to keep in step.
            var scene = new Scene();

            Assert.Same(scene.ResetKeyCommand, scene.Inspector.ResetKeyCommand);
            Assert.Same(scene.CopyKeyCommand, scene.Inspector.CopyKeyCommand);
            Assert.Same(scene.CancelCopyKeyCommand, scene.Inspector.CancelCopyKeyCommand);
        }

        [Fact]
        public void TheArmedCopyState_IsReadOffTheCancelCommandRatherThanMirrored()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Inspector.IsCopyArmed);

            scene.SetCopyArmed(true);

            Assert.True(scene.Inspector.IsCopyArmed);

            scene.SetCopyArmed(false);

            Assert.False(scene.Inspector.IsCopyArmed);
        }

        [Fact]
        public void TheAdvisoryNote_IsReadOffTheEditorsSet_AndNeverRescanned()
        {
            var scene = new Scene();
            var advisories = EditorAdvisories.Build(scene.Layout);

            // The layout is clean, so nothing is anchored anywhere yet.
            scene.Inspector.Refresh(scene.Key(TestLayouts.RgbDigitOneKeyIndex), scene.Layer, scene.Layout, advisories);

            Assert.False(scene.Inspector.HasAdvisory);

            // Two positions now carry the same token, which DuplicateKeyScan reports per position.
            scene.Key(TestLayouts.RgbDigitTwoKeyIndex).Key.Remap(TestLayouts.Gen1Key("1"));

            var rescanned = EditorAdvisories.Build(scene.Layout);

            scene.Inspector.Refresh(scene.Key(TestLayouts.RgbDigitOneKeyIndex), scene.Layer, scene.Layout, rescanned);

            Assert.True(scene.Inspector.HasAdvisory);
            Assert.Equal(
                rescanned.ForKey(scene.Layer.Index, TestLayouts.RgbDigitOneKeyIndex)[0].Message,
                scene.Inspector.AdvisoryNote);
        }

        [Fact]
        public void TheAdvisoryNoteIsNotDerivedFromTheModel_SoAnEmptySetSaysNothing()
        {
            // Proves the rail reads rather than scans: the layout carries a genuine duplicate and
            // the rail still says nothing, because the set it was handed says nothing.
            var scene = new Scene();

            scene.Key(TestLayouts.RgbDigitTwoKeyIndex).Key.Remap(TestLayouts.Gen1Key("1"));

            scene.Inspector.Refresh(
                scene.Key(TestLayouts.RgbDigitOneKeyIndex),
                scene.Layer,
                scene.Layout,
                EditorAdvisories.Empty);

            Assert.False(scene.Inspector.HasAdvisory);
        }

        [Fact]
        public void HasSelection_FollowsTheKeyAndNothingElse()
        {
            // The rail's own switch since issue #119: it is a permanent column, so what the view
            // asks is "is there a position to be about", never "is the rail open".
            var scene = new Scene();

            Assert.False(scene.Inspector.HasSelection);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.True(scene.Inspector.HasSelection);

            scene.Refresh(key: null);

            Assert.False(scene.Inspector.HasSelection);
        }

        [Fact]
        public void TheEmptyStateSentence_IsOneShortLineOfItsOwn()
        {
            // It is the rail's, not a panel's: the two panel NoSelectionMessages answer a different
            // question from inside a rail that is already explaining itself, and neither is
            // reachable now that the empty state replaces the panels wholesale.
            Assert.NotEmpty(KeyInspectorViewModel.NoSelectionMessage);
            Assert.NotEqual(KeyInspectorViewModel.NoSelectionMessage, TapAndHoldPanelViewModel.NoSelectionMessage);
            Assert.NotEqual(KeyInspectorViewModel.NoSelectionMessage, MacroInspectorPanelViewModel.NoSelectionMessage);
        }

        [Fact]
        public void Close_StandsTheRailDownWithoutMovingTheSelection_AndANewKeyOpensItAgain()
        {
            // No longer Escape's last stage — issue #119 gave that to the selection — but still the
            // editor's teardown path, and still about the rail rather than about what is selected.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.Close();

            Assert.False(scene.Inspector.IsOpen);

            // A refresh for the same key must not undo the dismissal: it fires on every edit
            // anywhere in the layout.
            scene.Refresh(scene.Key(TestLayouts.RgbDigitOneKeyIndex));

            Assert.False(scene.Inspector.IsOpen);

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.True(scene.Inspector.IsOpen);
        }

        [Fact]
        public void Open_ReopensADismissedRailForTheSameKey()
        {
            // Clicking the selected cap again is a request for the inspector, not a refresh.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.Close();
            scene.Inspector.Open();

            Assert.True(scene.Inspector.IsOpen);
        }

        [Fact]
        public void WithNothingSelected_TheRailHasNothingToSayAboutAPosition()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Refresh(key: null);

            Assert.False(scene.Inspector.HasSelection);
            Assert.False(scene.Inspector.IsOpen);
            Assert.Equal(string.Empty, scene.Inspector.PositionToken);
            Assert.Equal(string.Empty, scene.Inspector.CurrentAssignmentText);
            Assert.False(scene.Inspector.IsLocked);
        }

        [Fact]
        public void ALockedPosition_PutsTheRailOnTheLockedPanel()
        {
            var scene = Scene.Locked();

            scene.Select(1);

            Assert.True(scene.Inspector.IsLocked);
            Assert.NotEmpty(scene.Inspector.LockedPanel.Hotkeys);
        }

        [Fact]
        public void TheRailIsNotModal()
        {
            // The rule the whole issue turns on: the board stays clickable while the rail is open,
            // so nothing here may look like an overlay. A property named for modality appearing on
            // this type is the shape of the mistake, and this is the tripwire for it.
            var members = typeof(KeyInspectorViewModel)
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain("HasActiveOverlay", members);
            Assert.DoesNotContain("IsModal", members);
            Assert.DoesNotContain("ActiveOverlay", members);
        }

        [Fact]
        public void ThePanelsAreRefreshedTogether_NotOnlyTheShowingOne()
        {
            // A panel the user switches to has to be right already; the alternative is a second
            // refresh path down the mode switch.
            var remap = new RecordingPanel(KeyInspectorMode.Remap);
            var hold = new RecordingPanel(KeyInspectorMode.TapAndHold);
            var scene = new Scene([remap, hold]);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal(1, remap.Refreshes);
            Assert.Equal(1, hold.Refreshes);
        }

        [Fact]
        public void SwitchingMode_StandsTheOutgoingPanelDown()
        {
            var remap = new RecordingPanel(KeyInspectorMode.Remap);
            var hold = new RecordingPanel(KeyInspectorMode.TapAndHold);
            var scene = new Scene([remap, hold]);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Same(remap, scene.Inspector.ActivePanel);

            scene.Inspector.SelectModeCommand.Execute(scene.Tab(KeyInspectorMode.TapAndHold));

            Assert.Equal(1, remap.Deactivations);
            Assert.Same(hold, scene.Inspector.ActivePanel);
        }

        [Fact]
        public void ClosingTheRail_StandsTheShowingPanelDown()
        {
            // A panel left armed behind a closed rail would go on swallowing keystrokes.
            var remap = new RecordingPanel(KeyInspectorMode.Remap);
            var scene = new Scene([remap]);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);
            scene.Inspector.Close();

            Assert.Equal(1, remap.Deactivations);
        }

        [Fact]
        public void TheRailReportsThePanelsRecording_SoTheEditorCanSuppressTheGrammar()
        {
            // Without this ⌘S fires while a hold action is being recorded.
            var remap = new RecordingPanel(KeyInspectorMode.Remap);
            var scene = new Scene([remap]);
            var notifications = 0;

            scene.Inspector.RecordingChanged += (_, _) => notifications++;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Inspector.IsRecording);

            remap.StartRecording();

            Assert.True(scene.Inspector.IsRecording);
            Assert.True(notifications > 0);
        }

        /// <summary>A rail over a real Freestyle Edge RGB board and three fake editor commands.</summary>
        /// <summary>A stand-in Macro panel that asks for the handoff's 300 px rail.</summary>
        private sealed class WidePanel : KeyInspectorPanelViewModel
        {
            public override KeyInspectorMode Mode => KeyInspectorMode.Macro;

            public override string Title => KeyInspectorTabViewModel.MacroCaption;

            public override bool WantsWideRail => true;

            public override void Refresh(
                KeyboardKeyViewModel? key,
                KeyboardLayerViewModel? layer,
                KeyboardLayout? layout,
                EditorAdvisories advisories)
            {
            }
        }

        /// <summary>The same slot, by a panel that does not — the rail must stay at 268.</summary>
        private sealed class NarrowPanel : KeyInspectorPanelViewModel
        {
            public override KeyInspectorMode Mode => KeyInspectorMode.Macro;

            public override string Title => KeyInspectorTabViewModel.MacroCaption;

            public override void Refresh(
                KeyboardKeyViewModel? key,
                KeyboardLayerViewModel? layer,
                KeyboardLayout? layout,
                EditorAdvisories advisories)
            {
            }
        }

        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public KeyboardLayerViewModel Layer { get; }

            public KeyInspectorViewModel Inspector { get; }

            public IRelayCommand ResetKeyCommand { get; }

            public IRelayCommand CopyKeyCommand { get; }

            public IRelayCommand CancelCopyKeyCommand { get; }

            private bool _isCopyArmed;

            public Scene(IEnumerable<KeyInspectorPanelViewModel>? panels = null)
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb, panels)
            {
            }

            private Scene(
                KeyboardLayout layout,
                KeyboardVisual visual,
                IEnumerable<KeyInspectorPanelViewModel>? panels)
            {
                Layout = layout;
                Layer = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null)[0];

                ResetKeyCommand = new RelayCommand(() => { });
                CopyKeyCommand = new RelayCommand(() => { });
                CancelCopyKeyCommand = new RelayCommand(() => { }, () => _isCopyArmed);

                Inspector = new KeyInspectorViewModel(ResetKeyCommand, CopyKeyCommand, CancelCopyKeyCommand, panels);
            }

            /// <summary>A rail over the fixture whose second position can never be remapped.</summary>
            public static Scene Locked()
            {
                return new Scene(TestLayouts.CreateLockedKeyLayout(), TestLayouts.CreateVisual(0, 1, 2), panels: null);
            }

            public KeyboardKeyViewModel Key(int index)
            {
                return Layer.FindByIndex(index)!;
            }

            public KeyInspectorTabViewModel Tab(KeyInspectorMode mode)
            {
                return Inspector.Tabs.Single(tab => tab.Mode == mode);
            }

            public void Select(int index)
            {
                Refresh(Key(index));
            }

            public void Refresh(KeyboardKeyViewModel? key)
            {
                Inspector.Refresh(key, Layer, Layout, EditorAdvisories.Empty);
            }

            public void SetCopyArmed(bool isArmed)
            {
                _isCopyArmed = isArmed;

                CancelCopyKeyCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>A mode panel that records what the rail did to it and nothing else.</summary>
        private sealed class RecordingPanel : KeyInspectorPanelViewModel
        {
            public override KeyInspectorMode Mode { get; }

            public override string Title => Mode.ToString();

            public override bool IsRecording => _isRecording;

            public int Refreshes { get; private set; }

            public int Deactivations { get; private set; }

            private bool _isRecording;

            public RecordingPanel(KeyInspectorMode mode)
            {
                Mode = mode;
            }

            public override void Refresh(
                KeyboardKeyViewModel? key,
                KeyboardLayerViewModel? layer,
                KeyboardLayout? layout,
                EditorAdvisories advisories)
            {
                Refreshes++;
            }

            public override void Deactivate()
            {
                Deactivations++;

                _isRecording = false;

                OnRecordingChanged();
            }

            public void StartRecording()
            {
                _isRecording = true;

                OnRecordingChanged();
            }
        }
    }
}
