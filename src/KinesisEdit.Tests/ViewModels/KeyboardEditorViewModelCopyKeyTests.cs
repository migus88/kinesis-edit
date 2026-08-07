using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// <c>Copy key…</c> (mockups 1e/2a), built to the editor's existing click-then-press grammar
    /// with the press replaced by a second click: arm from the selected key, then the next cap
    /// clicked is the target.
    /// <para>
    /// <b>One action, three entry points.</b> The legend row's <c>Copy key…</c>, the key
    /// inspector footer's <c>Copy to…</c> and the locked-key panel's all run this same command
    /// (issue #92) — an earlier note calling the footer's a different action is superseded.
    /// </para>
    /// </summary>
    public sealed class KeyboardEditorViewModelCopyKeyTests : IDisposable
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

        [AvaloniaFact]
        public async Task CopyKeyCommand_WithNothingSelected_CannotRun()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Null(editor.SelectedKey);
            Assert.False(editor.CopyKeyCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task CopyKeyCommand_WithAnEditableKeySelected_ArmsAndTheRowPrompts()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            editor.SelectKeyCommand.Execute(source);

            Assert.True(editor.CopyKeyCommand.CanExecute(null));

            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);
            Assert.Same(source, editor.CopySource);
            Assert.Equal(BoardLegendViewModel.CopyTargetPrompt, editor.CopyPrompt);
            Assert.Equal(BoardLegendViewModel.CopyTargetPrompt, editor.BoardLegend.CopyPrompt);
            Assert.True(editor.BoardLegend.IsPickingCopyTarget);
            Assert.True(editor.CancelCopyKeyCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task CopyKeyCommand_WhileTheProfileIsStillLoading_CannotRun()
        {
            var editor = CreateEditor();

            Assert.True(editor.IsLoading);
            Assert.False(editor.CopyKeyCommand.CanExecute(null));

            await editor.LoadAsync();
        }

        /// <summary>
        /// The predicate deliberately does <b>not</b> consult the open overlay any more: the key
        /// inspector footer runs this very command, and the rail is not modal, so nothing about it
        /// may be phrased in terms of <c>HasActiveOverlay</c> (issue #92). Nothing is lost — the
        /// panel disarms the pick on the way in, and the scrim is what stops a target being
        /// clicked.
        /// </summary>
        [AvaloniaFact]
        public async Task CopyKeyCommand_WhenAPanelOpens_IsDisarmedRatherThanDisabled()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex));
            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);

            editor.ShowOverlay(new ExportOverlayViewModel(null, _folderPicker, _files, _notifications));

            Assert.False(editor.IsCopyArmed);
            Assert.True(editor.CopyKeyCommand.CanExecute(null));
        }

        /// <summary>
        /// Acceptance criterion 5 of issue #92: a locked position (05 §5.3) is a legal copy
        /// <b>source</b>. It was refused outright before, which is what made the locked-key panel's
        /// <c>Copy from…</c>/<c>Copy to…</c> asymmetry cosmetic rather than real.
        /// </summary>
        [AvaloniaFact]
        public async Task CopyKeyCommand_WithALockedPositionSelected_StillArms()
        {
            var editor = await CreateLoadedEditorAsync(TestLayouts.CreateLockedKeyLayout());
            var locked = editor.SelectedLayer!.Keys[1];
            var target = editor.SelectedLayer.Keys[2];

            Assert.False(locked.CanEdit);

            // The target carries a remap, so the copy has something visible to overwrite: a locked
            // position can never be remapped, so what it copies across is "nothing assigned".
            Remap(editor, target, TestLayouts.Gen1Key("F5"));

            Assert.True(target.Key.IsModified);

            editor.SelectKeyCommand.Execute(locked);

            Assert.True(editor.CopyKeyCommand.CanExecute(null));

            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);
            Assert.Same(locked, editor.CopySource);

            // ...and the copy really runs: reading a locked position is exactly what is allowed.
            editor.SelectKeyCommand.Execute(target);

            Assert.False(editor.IsCopyArmed);
            Assert.False(target.Key.IsModified);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_TakesTheNextCapClicked_AndLandsTheAssignmentOnIt()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);
            var target = KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex);
            var assignment = TestLayouts.Gen1Key("F5");

            Remap(editor, source, assignment);

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(target);

            Assert.True(target.Key.IsModified);
            Assert.Equal(assignment.Code, target.Key.ModifiedKey!.Code);

            // Invariant 3: the cap re-read the model, so the picture is not showing the old legend.
            Assert.Equal(source.Caption, target.Caption);
            Assert.True(target.IsModified);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_Disarms_OnceItHasLanded()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex));

            Assert.False(editor.IsCopyArmed);
            Assert.Null(editor.CopySource);
            Assert.Equal(string.Empty, editor.BoardLegend.CopyPrompt);
            Assert.False(editor.CancelCopyKeyCommand.CanExecute(null));
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_MovesTheLegendsCounts_AndTheChromesOwn()
        {
            // Invariant 16: the write ends in RefreshCounters, so the row and the toolbar counters
            // both follow. Without it the cap would change and the row would keep yesterday's count.
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            Assert.Equal(1, editor.BoardLegend.RemappedCount);

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex));

            Assert.Equal(2, editor.BoardLegend.RemappedCount);
            Assert.Equal(2, editor.SelectedLayer!.RemappedCount);
            Assert.Equal(2, editor.ModifiedKeyCount);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_MarksTheSessionDirty_SoSaveTurnsAmber()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            // The fake session reports what it is told, exactly like the real one reports what its
            // serializer says; the point of the test is that the copy *re-asks*.
            _profiles.SessionToReturn!.IsDirty = false;

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);

            _profiles.SessionToReturn.IsDirty = true;

            Assert.False(editor.IsDirty);

            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex));

            Assert.True(editor.IsDirty);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_OntoItsOwnSource_WritesNothingAndDisarms()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(source);

            Assert.False(editor.IsCopyArmed);
            Assert.False(source.Key.IsModified);

            // And it is a cancel, not the click contract's "a second hit starts listening".
            Assert.False(editor.IsListening);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_OntoAPositionThatCannotBeEdited_RefusesCleanlyAndSaysWhy()
        {
            // The Freestyle Edge RGB's own geometry has no locked position, so the fixture pairs
            // its picture with a layout that does (TestLayouts.CreateLockedKeyLayout).
            var editor = await CreateLoadedEditorAsync(TestLayouts.CreateLockedKeyLayout());
            var source = editor.SelectedLayer!.Keys[0];
            var locked = editor.SelectedLayer.Keys[1];

            Assert.False(locked.CanEdit);

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            source.Key.SetMacro(1, editor.Layout!.CreateMacro());
            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(locked);

            // Nothing half-copied: neither the key data (Core refuses it) nor the macros (Core
            // would have taken them).
            Assert.False(locked.Key.IsModified);
            Assert.False(locked.Key.IsMacro);

            // And it says why, without blocking anything — the pick is still live.
            Assert.Equal(BoardLegendViewModel.CopyTargetLockedPrompt, editor.BoardLegend.CopyPrompt);
            Assert.True(editor.IsCopyArmed);
            Assert.Same(source, editor.CopySource);
        }

        [AvaloniaFact]
        public async Task AfterARefusal_TheNextEditableTargetStillTakesTheCopy()
        {
            var editor = await CreateLoadedEditorAsync(TestLayouts.CreateLockedKeyLayout());
            var source = editor.SelectedLayer!.Keys[0];
            var target = editor.SelectedLayer.Keys[2];

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(editor.SelectedLayer.Keys[1]);
            editor.SelectKeyCommand.Execute(target);

            Assert.True(target.Key.IsModified);
            Assert.False(editor.IsCopyArmed);
            Assert.Equal(string.Empty, editor.BoardLegend.CopyPrompt);
        }

        [AvaloniaFact]
        public async Task TheCopy_CarriesTheMacrosOntoAPositionThatMayHoldThem()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);
            var target = KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex);

            source.Key.SetMacro(1, editor.Layout!.CreateMacro());
            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(target);

            Assert.True(target.Key.IsMacro);
            Assert.True(target.IsMacro);

            // Both positions carry one now, and the row counted the second without the cap having
            // been refreshed by anything but RefreshCounters.
            Assert.Equal(2, editor.BoardLegend.MacroCount);
        }

        [AvaloniaFact]
        public async Task TheCopy_LeavesTheMacrosBehindOnAPositionThatMayNotHoldThem()
        {
            // KeyboardKey.CopyFrom copies macros onto a restricted position anyway, and
            // KeyboardLayout.Validate then reports MacroOnRestrictedKey — a violation that stops
            // the save outright (04 §5.3), not one of the four budget advisories. The widest scope
            // that is *correct* here is therefore KeyData.
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);
            var modifier = KeyAt(editor, TestLayouts.RgbLeftShiftKeyIndex);

            Assert.False(modifier.CanAssignMacro);
            Assert.True(modifier.CanEdit);

            source.Key.SetMacro(1, editor.Layout!.CreateMacro());
            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(modifier);

            Assert.True(modifier.Key.IsModified);
            Assert.False(modifier.Key.IsMacro);
            Assert.DoesNotContain(
                editor.Layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MacroOnRestrictedKey);
        }

        [AvaloniaFact]
        public async Task CancelCopyKeyCommand_LeavesTheArmedStateWithoutWriting()
        {
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);
            var target = KeyAt(editor, TestLayouts.RgbDigitTwoKeyIndex);

            Remap(editor, source, TestLayouts.Gen1Key("F5"));

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);
            editor.CancelCopyKeyCommand.Execute(null);

            Assert.False(editor.IsCopyArmed);
            Assert.Equal(string.Empty, editor.BoardLegend.CopyPrompt);

            // And the next click is an ordinary selection again, not a target.
            editor.SelectKeyCommand.Execute(target);

            Assert.False(target.Key.IsModified);
            Assert.Same(target, editor.SelectedKey);
        }

        [AvaloniaFact]
        public async Task ArmingACopy_EndsAListeningKey_AndStartingARemapEndsAnArmedCopy()
        {
            // The two are different answers to "what does the next input mean", so only one of
            // them is ever live. That exclusivity is what makes the view's Escape order — panel,
            // then capture, then the copy — unambiguous rather than lucky.
            var editor = await CreateLoadedEditorAsync();
            var source = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            editor.SelectKeyCommand.Execute(source);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);
            Assert.False(editor.IsListening);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);
            Assert.False(editor.IsCopyArmed);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_EndsOnALayerSwitchAndOnASectionSwitch()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex));
            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.False(editor.IsCopyArmed);

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);
            editor.CopyKeyCommand.Execute(null);

            Assert.True(editor.IsCopyArmed);

            editor.SelectedTab = EditorTab.Settings;

            Assert.False(editor.IsCopyArmed);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_EndsWhenAFeaturePanelOpensOverTheBoard()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex));
            editor.CopyKeyCommand.Execute(null);

            editor.ShowOverlay(new ExportOverlayViewModel(null, _folderPicker, _files, _notifications));

            Assert.False(editor.IsCopyArmed);
        }

        [AvaloniaFact]
        public async Task ResetLayer_StillWorksFromItsNewHomeInTheLegendRow()
        {
            // The command moved views, not owners: the row runs the editor's own instance.
            var editor = await CreateLoadedEditorAsync();
            var key = KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex);

            Remap(editor, key, TestLayouts.Gen1Key("F5"));

            Assert.Same(editor.ResetLayerCommand, editor.BoardLegend.ResetLayerCommand);
            Assert.Equal(1, editor.BoardLegend.RemappedCount);

            // Running the editor's own instance means running its confirmation too: the legend's
            // button asks before it erases, exactly as the toolbar's does.
            ConfirmTheNextReset();

            editor.BoardLegend.ResetLayerCommand.Execute(null);

            Assert.False(key.Key.IsModified);
            Assert.Equal(0, editor.BoardLegend.RemappedCount);
        }

        [AvaloniaFact]
        public async Task TheLegendRow_IsLayerScopedAndFollowsTheSwitch()
        {
            var editor = await CreateLoadedEditorAsync();

            Remap(editor, KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex), TestLayouts.Gen1Key("F5"));

            Assert.Equal(1, editor.BoardLegend.RemappedCount);

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.Equal(0, editor.BoardLegend.RemappedCount);

            editor.SelectLayerCommand.Execute(editor.Layers[0]);

            Assert.Equal(1, editor.BoardLegend.RemappedCount);
        }

        [AvaloniaFact]
        public async Task TheLegendRow_CountsTheLayersAdvisoryPositions()
        {
            // The count is an aggregate of what the strip already says, not a third place an
            // advisory is reported (invariant 21) — and it is pushed in from EditorAdvisories,
            // which is the only thing that knows.
            var editor = await CreateLoadedEditorAsync();
            var layer = editor.SelectedLayer!;

            Assert.Equal(0, editor.BoardLegend.AdvisoryCount);

            // The board already carries this token on its first position, so one remap puts it on
            // two — exactly what DuplicateKeyScan reports.
            Remap(editor, KeyAt(editor, TestLayouts.RgbDigitOneKeyIndex), layer.Keys[0].Key.OriginalKey);

            Assert.Equal(editor.Advisories.CountForLayer(layer.Index), editor.BoardLegend.AdvisoryCount);
            Assert.Equal(2, editor.BoardLegend.AdvisoryCount);

            // The other layer carries none, and the row is layer-scoped.
            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.Equal(0, editor.BoardLegend.AdvisoryCount);
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }
        }

        /// <summary>Answers Yes to the next reset confirmation, which erases nothing without one.</summary>
        private void ConfirmTheNextReset()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };
        }

        private static KeyboardKeyViewModel KeyAt(KeyboardEditorViewModel editor, int index)
        {
            return editor.SelectedLayer!.FindByIndex(index)
                ?? throw new InvalidOperationException($"The layer has no position {index}.");
        }

        /// <summary>Remaps one cap through the editor's own workflow: select, listen, receive.</summary>
        private void Remap(KeyboardEditorViewModel editor, KeyboardKeyViewModel key, KeyDefinition assignment)
        {
            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            _capture.RaiseKeystroke(assignment);
        }

        private KeyboardEditorViewModel CreateEditor()
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

            return editor;
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(KeyboardLayout? layout = null)
        {
            if (layout is not null)
            {
                _profiles.SessionToReturn = new FakeProfileSession(layout);
            }

            var editor = CreateEditor();

            await editor.LoadAsync();

            return editor;
        }
    }
}
