using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// <c>Copy macro to…</c> — the Macro panel's armed pick (issue #141): arm from the rail, then
    /// the next cap clicked receives an independent <b>copy</b> of that one macro (06 §1: there is
    /// no shared macro anywhere on disk).
    /// <para>
    /// It is the <em>same</em> armed state as the legend row's whole-key <c>Copy key…</c>, scoped by
    /// the slot the panel was showing — one <c>CopySource</c>, one prompt, one Escape route — so the
    /// two cases that matter most here are the ones where that scope has to be dropped again.
    /// </para>
    /// </summary>
    public sealed class KeyboardEditorViewModelMacroCopyTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeSettingsService _settings = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly List<KeyboardEditorViewModel> _editors = [];

        [Fact]
        public async Task CopyMacroCommand_OnAPositionCarryingNoMacro_IsRefusedAndArmsNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            OpenMacroPanelFor(editor, TestLayouts.RgbDigitOneKeyIndex);

            Assert.False(editor.CopyMacroCommand.CanExecute(null));

            editor.CopyMacroCommand.Execute(null);

            Assert.False(editor.IsCopyArmed);
            Assert.Equal(string.Empty, editor.CopyPrompt);
        }

        [Fact]
        public async Task CopyMacroCommand_ArmsThePickWithItsOwnPrompt()
        {
            var editor = await CreateLoadedEditorAsync();

            ArmAMacroCopyFromDigitOne(editor);

            Assert.True(editor.IsCopyArmed);

            // Its own sentence, not the whole-key one: the two picks look identical on the board and
            // this line is what says which of them is in flight.
            Assert.Equal(BoardLegendViewModel.CopyMacroTargetPrompt, editor.CopyPrompt);
            Assert.Equal(BoardLegendViewModel.CopyMacroTargetPrompt, editor.BoardLegend.CopyPrompt);
        }

        [Fact]
        public async Task TheNextCapClicked_ReceivesAnIndependentCopyInItsFirstEmptySlot()
        {
            var editor = await CreateLoadedEditorAsync();

            var source = ArmAMacroCopyFromDigitOne(editor);
            var target = FindCap(editor, TestLayouts.RgbDigitTwoKeyIndex);

            editor.SelectKeyCommand.Execute(target);

            var original = source.Key.GetMacro(1)!;
            var copy = target.Key.GetMacro(1);

            Assert.NotNull(copy);
            Assert.NotSame(original, copy);
            Assert.Equal(original.Keystrokes.Count, copy!.Keystrokes.Count);

            // The copy sits where it landed: its trigger identity is the target's (05 §1.3), which
            // is what a stored name and the next load are both keyed by.
            Assert.Equal(target.Key.TriggerKey.Code, copy.TriggerKey);
            Assert.Equal(0, copy.LayerIndex);

            // ...and the original stayed exactly where it was.
            Assert.Same(original, source.Key.GetMacro(1));

            Assert.False(editor.IsCopyArmed);
            Assert.Equal(string.Empty, editor.CopyPrompt);

            // Invariant 3 and 16: the cap that grew a macro dot was re-read, and the funnel ran.
            Assert.True(target.IsMacro);
            Assert.Equal(2, editor.MacroCount);
        }

        [Fact]
        public async Task TheSourceKeyItself_DuplicatesTheMacroIntoItsOwnNextFreeSlot()
        {
            // Deliberate, and the opposite of the whole-key copy's "never mind": a whole-key
            // self-copy writes nothing, while this writes something real — and it is the one
            // surviving replacement for the deleted library's Duplicate.
            var editor = await CreateLoadedEditorAsync();

            var source = ArmAMacroCopyFromDigitOne(editor);
            var original = source.Key.GetMacro(1)!;

            editor.SelectKeyCommand.Execute(source);

            Assert.Same(original, source.Key.GetMacro(1));
            Assert.NotNull(source.Key.GetMacro(2));
            Assert.NotSame(original, source.Key.GetMacro(2));

            Assert.False(editor.IsCopyArmed);
            Assert.Equal(2, editor.MacroCount);
        }

        [Fact]
        public async Task ATargetWhoseSlotsAreAllTaken_RefusesAndStaysArmed()
        {
            var editor = await CreateLoadedEditorAsync();

            var source = ArmAMacroCopyFromDigitOne(editor);
            var target = FindCap(editor, TestLayouts.RgbDigitTwoKeyIndex);

            FillEverySlotOf(editor, target);

            var before = editor.MacroCount;

            editor.SelectKeyCommand.Execute(target);

            Assert.Equal(BoardLegendViewModel.CopyMacroTargetFullPrompt, editor.CopyPrompt);
            Assert.Equal(before, editor.MacroCount);

            // The refusal is about that one cap, so the user is still picking — and the source is
            // still the one they armed from.
            Assert.True(editor.IsCopyArmed);
            Assert.Same(source, editor.CopySource);
        }

        [Fact]
        public async Task APositionThatCannotCarryAMacro_RefusesAndStaysArmed()
        {
            var editor = await CreateLoadedEditorAsync();

            ArmAMacroCopyFromDigitOne(editor);

            var target = FindCap(editor, TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(target.CanAssignMacro);

            editor.SelectKeyCommand.Execute(target);

            // Its own sentence: a modifier position is perfectly remappable (05 §5.3) and still
            // refuses macros, so the whole-key copy's "cannot be edited" would be a lie.
            Assert.Equal(BoardLegendViewModel.CopyMacroTargetLockedPrompt, editor.CopyPrompt);
            Assert.Equal(1, editor.MacroCount);
            Assert.True(editor.IsCopyArmed);
        }

        [Fact]
        public async Task AProfileAlreadyAtItsMacroCount_RefusesTheCopyAndStaysArmed()
        {
            // Invariant 11's third input-time refusal: a copy that pushed the profile past 06 §6's
            // count would leave Validate() stopping every save of it — quietly unsavable, which is
            // exactly what CopyScopeFor exists to prevent on the other copy path.
            var editor = await CreateLoadedEditorAsync();
            var limit = editor.Layout!.Device.Macros.MaxMacroCount!.Value;

            TestLayouts.FillMacroSlots(editor.Layout, limit);

            var source = FindFirstCapCarryingAMacro(editor);
            var target = FindLastEmptyMacroCap(editor);

            OpenMacroPanel(editor, source);

            editor.CopyMacroCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);

            editor.SelectKeyCommand.Execute(target);

            Assert.Equal(
                MacroInspectorPanelViewModel.BuildMacroCountLimitMessage(limit),
                editor.CopyPrompt);

            // Off the model: the staging above wrote to it directly, so the editor's own counter is
            // one refresh behind and would report a nothing that looks like a pass.
            Assert.Equal(limit, editor.Layout.MacroCount);
            Assert.True(editor.IsCopyArmed);
        }

        [Fact]
        public async Task CancelCopyKeyCommand_DropsTheMacroScopeAndNotOnlyTheSource()
        {
            // The leak this guards: a _copyMacroSlot left behind by a cancelled macro copy would
            // turn the NEXT whole-key copy into a macro copy, silently. Every disarm path in the
            // editor goes through CancelCopyKey, so proving it here proves it for all of them.
            var editor = await CreateLoadedEditorAsync();

            var source = ArmAMacroCopyFromDigitOne(editor);

            editor.CancelCopyKeyCommand.Execute(null);

            Assert.False(editor.IsCopyArmed);

            RemapTheCap(source, "z");

            editor.CopyKeyCommand.Execute(null);

            var target = FindCap(editor, TestLayouts.RgbDigitTwoKeyIndex);

            editor.SelectKeyCommand.Execute(target);

            // A whole-key copy carries the assignment; a macro copy carries nothing but the macro.
            Assert.True(target.IsModified);
        }

        [Fact]
        public async Task ArmingAWholeKeyCopy_OverAnArmedMacroCopy_CopiesTheWholeKey()
        {
            var editor = await CreateLoadedEditorAsync();

            var source = ArmAMacroCopyFromDigitOne(editor);

            RemapTheCap(source, "z");

            editor.CopyKeyCommand.Execute(null);

            Assert.Equal(BoardLegendViewModel.CopyTargetPrompt, editor.CopyPrompt);

            var target = FindCap(editor, TestLayouts.RgbDigitTwoKeyIndex);

            editor.SelectKeyCommand.Execute(target);

            Assert.True(target.IsModified);
        }

        [Fact]
        public async Task AModalOpeningOverTheBoard_DisarmsTheMacroCopy()
        {
            // The scrim swallows every click aimed at the board, so a pick that stayed armed under
            // one could never be finished.
            var editor = await CreateLoadedEditorAsync();

            ArmAMacroCopyFromDigitOne(editor);

            editor.ExportCommand.Execute(null);

            Assert.NotNull(editor.ActiveOverlay);
            Assert.False(editor.IsCopyArmed);
            Assert.Equal(string.Empty, editor.CopyPrompt);
        }

        /// <summary>
        /// Records a macro on the digit-1 position and arms a copy of it, handing back that cap.
        /// </summary>
        private KeyboardKeyViewModel ArmAMacroCopyFromDigitOne(KeyboardEditorViewModel editor)
        {
            var source = FindCap(editor, TestLayouts.RgbDigitOneKeyIndex);
            var panel = OpenMacroPanel(editor, source);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("a"));

            panel.Deactivate();

            Assert.True(editor.CopyMacroCommand.CanExecute(null));

            editor.CopyMacroCommand.Execute(null);

            return source;
        }

        private static void RemapTheCap(KeyboardKeyViewModel cap, string token)
        {
            cap.Key.Remap(TestLayouts.Gen1Key(token));
            cap.RefreshFromModel();
        }

        private static void FillEverySlotOf(KeyboardEditorViewModel editor, KeyboardKeyViewModel cap)
        {
            for (var slot = Macro.MinMacroIndex; slot <= Macro.MaxMacroIndex; slot++)
            {
                cap.Key.SetMacro(slot, editor.Layout!.CreateMacro());
            }
        }

        private static KeyboardKeyViewModel FindFirstCapCarryingAMacro(KeyboardEditorViewModel editor)
        {
            foreach (var cap in editor.SelectedLayer!.Keys)
            {
                if (cap.Key.GetMacro(Macro.MinMacroIndex) is not null)
                {
                    return cap;
                }
            }

            throw new InvalidOperationException("The staged layout carries no macro on any drawn position.");
        }

        private static KeyboardKeyViewModel FindLastEmptyMacroCap(KeyboardEditorViewModel editor)
        {
            for (var index = editor.SelectedLayer!.Keys.Count - 1; index >= 0; index--)
            {
                var cap = editor.SelectedLayer.Keys[index];

                if (cap.Key.CanAssignMacro && cap.Key.MacroCount == 0)
                {
                    return cap;
                }
            }

            throw new InvalidOperationException("Every drawn position that accepts macros is full.");
        }

        private static KeyboardKeyViewModel FindCap(KeyboardEditorViewModel editor, int keyIndex)
        {
            return editor.SelectedLayer!.FindByIndex(keyIndex)
                   ?? throw new InvalidOperationException($"The layer has no position {keyIndex}.");
        }

        private static MacroInspectorPanelViewModel OpenMacroPanelFor(KeyboardEditorViewModel editor, int keyIndex)
        {
            return OpenMacroPanel(editor, FindCap(editor, keyIndex));
        }

        /// <summary>Selects <paramref name="cap"/> and puts the rail on its Macro mode.</summary>
        private static MacroInspectorPanelViewModel OpenMacroPanel(KeyboardEditorViewModel editor, KeyboardKeyViewModel cap)
        {
            editor.SelectKeyCommand.Execute(cap);

            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            return Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync()
        {
            var editor = new KeyboardEditorViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

            _editors.Add(editor);

            await editor.LoadAsync();

            return editor;
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }
        }
    }
}
