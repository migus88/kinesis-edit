using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The key inspector's Remap panel: what it says the position sends, the two ways it changes
    /// that, what it refuses, and the advisory it reports afterwards without ever blocking anything.
    /// <para>
    /// It is built without an editor anywhere near it — the panel contract is that a panel is
    /// <em>pushed</em> state and knows nothing about who pushed it, and a test that could only reach
    /// it through <c>KeyboardEditorViewModel</c> would quietly be asserting the opposite.
    /// </para>
    /// </summary>
    public sealed class RemapPanelViewModelTests
    {
        [Fact]
        public void ThePanel_IsTheRemapSlot()
        {
            var scene = new Scene();

            Assert.Equal(KeyInspectorMode.Remap, scene.Panel.Mode);
            Assert.Equal(KeyInspectorTabViewModel.RemapCaption, scene.Panel.Title);
        }

        [Fact]
        public void WithNothingSelected_ItSaysNothingAndRefusesEverything()
        {
            // The rail calls Refresh on every panel it holds, including with a null key.
            var panel = new RemapPanelViewModel(TokenDialect.Gen1);

            panel.Refresh(null, null, null, EditorAdvisories.Empty);

            Assert.Equal(string.Empty, panel.CurrentToken);
            Assert.False(panel.RecordCommand.CanExecute(null));
            Assert.False(panel.AssignCommand.CanExecute(null));
            Assert.False(panel.HasDuplicateAdvisory);
        }

        [Fact]
        public void RepeatedRefreshesWithTheSameKey_ChangeNothing()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);

            scene.Refresh();
            scene.Refresh();

            // The arm survives somebody else's edit: a refresh is not a selection change.
            Assert.True(scene.Panel.IsRecording);
            Assert.Equal("[1]", scene.Panel.CurrentToken);
        }

        [Fact]
        public void ItNamesWhatThePositionSendsNow()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal("[1]", scene.Panel.CurrentToken);

            scene.Key.Key.Remap(TestLayouts.Gen1Key("esc"));
            scene.Key.RefreshFromModel();
            scene.Refresh();

            Assert.Equal("[esc]", scene.Panel.CurrentToken);
        }

        [Fact]
        public void Record_ArmsCaptureAndSaysSo()
        {
            var scene = new Scene();
            var raised = 0;

            scene.Panel.RecordingChanged += (_, _) => raised++;

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Panel.IsRecording);
            Assert.Equal(RemapPanelViewModel.RecordCaption, scene.Panel.RecordCommandCaption);

            scene.Panel.RecordCommand.Execute(null);

            Assert.True(scene.Panel.IsRecording);
            Assert.Equal(RemapPanelViewModel.RecordingCaption, scene.Panel.RecordCommandCaption);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void RecordIsAToggle_AndPressingItAgainStandsDown()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.RecordCommand.Execute(null);

            Assert.False(scene.Panel.IsRecording);
        }

        [Fact]
        public void AnArmedPanel_WantsTheNextKeystrokeAndAssignsIt()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);

            Assert.True(((IKeystrokeSink)scene.Panel).WantsKeystrokes);

            scene.Panel.ReceiveKeystroke(Keystroke("esc"));

            Assert.Equal("[esc]", scene.Panel.CurrentToken);
            Assert.True(scene.Key.Key.IsModified);
            Assert.Equal("esc", scene.Key.Key.ModifiedKey!.Gen1Token);
            Assert.False(scene.Panel.IsRecording);
        }

        [Fact]
        public void APanelThatIsNotArmed_TakesNoKeystroke()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(((IKeystrokeSink)scene.Panel).WantsKeystrokes);

            scene.Panel.ReceiveKeystroke(Keystroke("esc"));

            Assert.False(scene.Key.Key.IsModified);
        }

        [Fact]
        public void PressingThePositionsOwnFactoryAction_UndoesTheRemap()
        {
            // Remap(), never ApplyRemap(): assigning a position its own action clears the remap
            // (04 §2.1), exactly as the editor's own capture path does.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.ReceiveKeystroke(Keystroke("esc"));

            Assert.True(scene.Key.Key.IsModified);

            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.ReceiveKeystroke(Keystroke("1"));

            Assert.False(scene.Key.Key.IsModified);
            Assert.Equal("[1]", scene.Panel.CurrentToken);
        }

        [Fact]
        public void EveryWrite_AsksTheEditorToRefresh()
        {
            // Core announces nothing, so a write that did not end here would leave every counter,
            // every advisory and the amber Save stale.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.ReceiveKeystroke(Keystroke("esc"));

            Assert.Equal(1, scene.AssignedCount);

            scene.Panel.Picker.Query = "f1";
            scene.Panel.Picker.ChooseCommand.Execute(null);

            Assert.Equal(2, scene.AssignedCount);
        }

        [Fact]
        public void TakingARowFromThePicker_Assigns()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "esc";
            scene.Panel.Picker.ChooseCommand.Execute(null);

            Assert.Equal("[esc]", scene.Panel.CurrentToken);
            Assert.Equal("esc", scene.Key.Key.ModifiedKey!.Gen1Token);
        }

        [Fact]
        public void AssignCommand_NeedsAHighlightedRow()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(scene.Panel.AssignCommand.CanExecute(null));

            scene.Panel.Picker.Query = "esc";

            Assert.True(scene.Panel.AssignCommand.CanExecute(null));

            scene.Panel.AssignCommand.Execute(null);

            Assert.Equal("esc", scene.Key.Key.ModifiedKey!.Gen1Token);
        }

        [Fact]
        public void AnAssignment_GoesIntoTheRecentList()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "esc";
            scene.Panel.Picker.ChooseCommand.Execute(null);

            Assert.Contains(TestLayouts.Gen1Key("esc"), scene.Recent.Entries);
        }

        [Fact]
        public void TheRecentListIsTheSessions_AndIsNotPersistedAnywhere()
        {
            // Per-session and in memory, deliberately: persisting it is an app_settings.txt question
            // and belongs with issue #96. Nothing here reads or writes a store.
            var store = new RecentTokenStore();
            var scene = new Scene(store);

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "esc";
            scene.Panel.Picker.ChooseCommand.Execute(null);

            Assert.Same(store, scene.Panel.Picker.Recent);
            Assert.Single(store.Entries);
        }

        [Fact]
        public void SelectingAnotherKey_ClearsTheQueryAndAnyArm()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "esc";
            scene.Panel.RecordCommand.Execute(null);

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.Equal(string.Empty, scene.Panel.Picker.Query);
            Assert.False(scene.Panel.IsRecording);
            Assert.Equal("[2]", scene.Panel.CurrentToken);
        }

        [Fact]
        public void MovingToAnotherKey_ClearsThePickerWithoutRebuildingIt()
        {
            // The ~1 s key selection of issue #133, at its source. Refresh clears the picker on
            // every new position, and Clear used to reallocate one row view model per catalog entry
            // — 200 of them on this board — plus the group list and the flat list the view realizes
            // from. A picker that was never typed into has nothing to clear, and now costs nothing.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            var rows = scene.Panel.Picker.Rows;
            var items = scene.Panel.Picker.Items;

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.Same(rows, scene.Panel.Picker.Rows);
            Assert.Same(items, scene.Panel.Picker.Items);
            Assert.Equal(string.Empty, scene.Panel.Picker.Query);
        }

        [Fact]
        public void MovingToAnotherKeyAfterASearch_StillEmptiesTheQueryAndRebuilds()
        {
            // The other half of the same rule: a query really did narrow the list, so the list it
            // narrowed has to come back. The early-out must not swallow that.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "esc";

            var narrowed = scene.Panel.Picker.Rows;

            scene.Select(TestLayouts.RgbDigitTwoKeyIndex);

            Assert.Equal(string.Empty, scene.Panel.Picker.Query);
            Assert.NotSame(narrowed, scene.Panel.Picker.Rows);
            Assert.Equal(scene.Panel.Picker.TotalCount, scene.Panel.Picker.MatchCount);
            Assert.Null(scene.Panel.Picker.SelectedRow);
        }

        [Fact]
        public void Deactivate_StandsTheArmDown_AndWritesNothing()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.RecordCommand.Execute(null);
            scene.Panel.Deactivate();

            Assert.False(scene.Panel.IsRecording);
            Assert.False(scene.Key.Key.IsModified);
            Assert.Equal(0, scene.AssignedCount);
        }

        [Fact]
        public void ALockedPosition_IsRefusedPolitelyRatherThanSilently()
        {
            var scene = Scene.Locked();

            scene.Select(1);

            Assert.False(scene.Panel.IsAvailable);
            Assert.Equal(RemapPanelViewModel.LockedReason, scene.Panel.UnavailableReason);
            Assert.False(scene.Panel.RecordCommand.CanExecute(null));

            scene.Panel.Picker.Query = "esc";

            Assert.False(scene.Panel.AssignCommand.CanExecute(null));

            scene.Panel.AssignCommand.Execute(null);

            Assert.False(scene.Key.Key.IsModified);
            Assert.Equal(0, scene.AssignedCount);
        }

        [Fact]
        public void AnUnlockedPosition_CarriesNoRefusal()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            Assert.True(scene.Panel.IsAvailable);
            Assert.Equal(string.Empty, scene.Panel.UnavailableReason);
        }

        [Fact]
        public void TheDuplicateAdvisory_AppearsAfterAnAssignmentThatCreatedOne_AndBlocksNothing()
        {
            // Criterion 3. The assignment lands first and the advisory reports it afterwards: limits
            // are reported, never enforced.
            var scene = new Scene();

            // [2] already sends the digit 2; assigning it to the [1] position duplicates it.
            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "2";

            var row = scene.Panel.Picker.Rows.First(candidate => candidate.Token == "[2]");

            scene.Panel.Picker.ChooseCommand.Execute(row);

            Assert.Equal("[2]", scene.Panel.CurrentToken);
            Assert.True(scene.Key.Key.IsModified);
            Assert.True(scene.Panel.HasDuplicateAdvisory);
            Assert.Contains("[2] is on 2 positions", scene.Panel.DuplicateAdvisory, StringComparison.Ordinal);
            Assert.EndsWith(AdvisoryText.DuplicatesAreAllowed, scene.Panel.DuplicateAdvisory, StringComparison.Ordinal);
        }

        [Fact]
        public void TheAdvisoryIsReadOffTheEditorsSet_NeverRescanned()
        {
            // A panel that ran DuplicateKeyScan itself would be a second derivation of one fact. The
            // proof: hand the panel an empty set after a duplicate was created, and it says nothing.
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "2";

            var row = scene.Panel.Picker.Rows.First(candidate => candidate.Token == "[2]");

            scene.Panel.Picker.ChooseCommand.Execute(row);

            Assert.True(scene.Panel.HasDuplicateAdvisory);

            scene.Panel.Refresh(scene.Key, scene.Layer, scene.Layout, EditorAdvisories.Empty);

            Assert.False(scene.Panel.HasDuplicateAdvisory);
        }

        [Fact]
        public void AnAssignmentThatDuplicatesNothing_ReportsNothing()
        {
            // `vol+` is on no position of the stock board, so nothing is duplicated by putting it
            // on one. (`esc` would be: the board already has an Esc key.)
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "vol+";
            scene.Panel.Picker.ChooseCommand.Execute(null);

            Assert.Equal("[vol+]", scene.Panel.CurrentToken);
            Assert.False(scene.Panel.HasDuplicateAdvisory);
        }

        [Fact]
        public void TheAdvisoryLeavesWithTheSelection()
        {
            var scene = new Scene();

            scene.Select(TestLayouts.RgbDigitOneKeyIndex);

            scene.Panel.Picker.Query = "2";

            scene.Panel.Picker.ChooseCommand.Execute(
                scene.Panel.Picker.Rows.First(row => row.Token == "[2]"));

            Assert.True(scene.Panel.HasDuplicateAdvisory);

            scene.Select(TestLayouts.RgbDigitThreeKeyIndex);

            Assert.False(scene.Panel.HasDuplicateAdvisory);
        }

        [Fact]
        public void FocusSearch_ReachesThePickersOwnRequest()
        {
            var scene = new Scene();
            var raised = 0;

            scene.Panel.Picker.FocusRequested += (_, _) => raised++;

            scene.Panel.FocusSearch();

            Assert.Equal(1, raised);
            Assert.True(scene.Panel.Picker.IsFocusPending);
        }

        [Fact]
        public void ThePanel_NeverConstructsAPickerAtItsOwnDialectByAccident()
        {
            var panel = new RemapPanelViewModel(TokenDialect.Gen2);

            Assert.Equal(TokenDialect.Gen2, panel.Picker.Dialect);
            Assert.Equal(KeySearchCatalog.Build(TokenDialect.Gen2).Count, panel.Picker.TotalCount);
        }

        private static CapturedKeystroke Keystroke(string token)
        {
            return new CapturedKeystroke
            {
                Key = TestLayouts.Gen1Key(token),
                PhysicalKey = PhysicalKeyCode.None
            };
        }

        /// <summary>A panel over a real board, refreshed the way the rail refreshes it.</summary>
        private sealed class Scene
        {
            public KeyboardLayout Layout { get; }

            public KeyboardLayerViewModel Layer { get; }

            public RemapPanelViewModel Panel { get; }

            public RecentTokenStore Recent { get; }

            public KeyboardKeyViewModel Key => _key ?? throw new InvalidOperationException("Nothing selected.");

            public int AssignedCount { get; private set; }

            private KeyboardKeyViewModel? _key;

            public Scene(RecentTokenStore? recent = null)
                : this(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb), VisualCatalog.FreestyleEdgeRgb, recent)
            {
            }

            private Scene(KeyboardLayout layout, KeyboardVisual visual, RecentTokenStore? recent)
            {
                Layout = layout;
                Layer = KeyboardLayerViewModel.BuildAll(layout, visual, lighting: null)[0];
                Recent = recent ?? new RecentTokenStore();
                Panel = new RemapPanelViewModel(layout.Dialect, Recent);

                // Exactly what the editor wires: a write here ends in its own refresh funnel, which
                // rebuilds the advisories and pushes the whole state back down.
                Panel.Assigned += (_, _) =>
                {
                    AssignedCount++;

                    Refresh();
                };
            }

            public static Scene Locked()
            {
                return new Scene(TestLayouts.CreateLockedKeyLayout(), TestLayouts.CreateVisual(0, 1, 2), recent: null);
            }

            public void Select(int keyIndex)
            {
                _key = Layer.FindByIndex(keyIndex)
                       ?? throw new InvalidOperationException($"No position {keyIndex} on this board.");

                Refresh();
            }

            public void Refresh()
            {
                Panel.Refresh(_key, Layer, Layout, EditorAdvisories.Build(Layout));
            }
        }
    }
}
