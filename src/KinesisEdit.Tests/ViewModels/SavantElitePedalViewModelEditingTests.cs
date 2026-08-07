using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// Programming a pedal input (specs/12-savant-elite.md §5, §6): Edit opens the entry box on one
    /// row, keystrokes land in it, Single Action / Macro switch modes, Special Actions add what a
    /// keyboard cannot, Done commits and Cancel discards — plus the two gates around losing work,
    /// the mid-edit question of §5 step 1 and the unsaved-changes question of §5 step 9.
    /// </summary>
    public sealed class SavantElitePedalViewModelEditingTests : IDisposable
    {
        private const int LeftPedal = 0;

        private const int MiddlePedal = 1;

        private const int Jack1 = 3;

        /// <summary>Where 12 §4.6 puts the file on a discovered SE2 v-Drive.</summary>
        private static string PedalFilePath =>
            Path.Combine(TestDevices.CreateLocation(DeviceId.SavantElite2).RootPath, "active", "pedals.txt");

        private readonly FakeVDriveFileService _fileService = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly PedalFileService _pedalFiles;
        private readonly List<SavantElitePedalViewModel> _editors = [];

        public SavantElitePedalViewModelEditingTests()
        {
            _pedalFiles = new PedalFileService(_fileService);
        }

        [Fact]
        public async Task BeginEditCommand_OnARow_OpensTheEntryBoxAndStartsCapture()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            Assert.True(editor.IsEditing);
            Assert.Same(editor.Inputs[LeftPedal], editor.EditingRow);
            Assert.True(editor.Inputs[LeftPedal].IsEditing);
            Assert.Equal("EDITING: Left Pedal", editor.EditingCaption);
            Assert.Equal(PedalInputMode.Single, editor.EditMode);
            Assert.True(editor.IsSingleMode);
            Assert.False(editor.HasEntry);
            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);
        }

        [Fact]
        public async Task BeginEditCommand_OnAProgrammedRow_LoadsWhatTheInputAlreadyHolds()
        {
            // 12 §5 step 1 in this app: Configure loads the current entry instead of defaulting to
            // an empty single action, so opening an input never throws its macro away.
            var editor = await LoadAsync("{lpedal}>{-a}{+a}{-b}{+b}");

            await BeginEditAsync(editor, LeftPedal);

            Assert.Equal(PedalInputMode.Macro, editor.EditMode);
            Assert.True(editor.IsMacroMode);
            Assert.Equal(new[] { "a", "b" }, editor.EntryItems);
            Assert.Equal("ab", editor.EntryText);
        }

        [Fact]
        public async Task BeginEditCommand_OnTheRowAlreadyBeingEdited_ChangesNothing()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            await BeginEditAsync(editor, LeftPedal);

            Assert.Same(editor.Inputs[LeftPedal], editor.EditingRow);
            Assert.Equal(new[] { "[a]" }, editor.EntryItems);
            Assert.Equal(1, _capture.StartCount);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task BeginEditCommand_WhenTheFileCouldNotBeRead_IsUnavailable()
        {
            _fileService.SetFile(PedalFilePath, "[lpedal]>[lmouse]");
            _fileService.SetUnreadable(PedalFilePath);

            var editor = await CreateLoadedAsync(VDriveConnectionStatus.Connected);

            Assert.Equal(PedalLoadState.LoadFailed, editor.LoadState);
            Assert.False(editor.CanEdit);
            Assert.False(editor.BeginEditCommand.CanExecute(editor.Inputs[LeftPedal]));

            await BeginEditAsync(editor, LeftPedal);

            Assert.False(editor.IsEditing);
            Assert.Equal(0, _capture.StartCount);
        }

        [Fact]
        public async Task KeystrokeCaptured_InSingleMode_ReplacesTheEntry()
        {
            // 12 §4.3: a single-action line holds exactly one token, so the last key wins.
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));
            _capture.RaiseKeystroke(Key("b"));

            Assert.Equal(new[] { "[b]" }, editor.EntryItems);
            Assert.Equal("[b]", editor.EntryText);
        }

        [Fact]
        public async Task KeystrokeCaptured_InMacroMode_AppendsWithTheModifiersHeldWithIt()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            _capture.RaiseKeystroke(Key("a"), Key("lshift"));
            _capture.RaiseKeystroke(Key("b"));

            Assert.Equal(new[] { "{lshift+a}", "b" }, editor.EntryItems);
            Assert.Equal("{lshift+a}b", editor.EntryText);
        }

        [Fact]
        public async Task KeystrokeCaptured_WhenNoEntryIsOpen_ChangesNothing()
        {
            var editor = await LoadAsync("[lpedal]>");

            _capture.RaiseKeystroke(Key("a"));

            Assert.False(editor.HasEntry);
            Assert.False(editor.HasUnsavedChanges);
            Assert.False(editor.Inputs[LeftPedal].IsAssigned);
        }

        [Fact]
        public async Task EntryText_ForASpace_ShowsTheOpenBoxSymbol()
        {
            // 12 §5: the entry box makes a recorded space visible; the row memo keeps the raw
            // token, which is what the file holds.
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            _capture.RaiseKeystroke(Key("space"));

            Assert.Equal(new[] { "␣" }, editor.EntryItems);

            editor.DoneCommand.Execute(null);

            Assert.Equal(" ", editor.Inputs[LeftPedal].AssignmentText);
        }

        [Fact]
        public async Task SetMacroModeCommand_AfterTyping_ClearsTheEntry()
        {
            // 12 §5 step 2: "switching modes clears the current entry".
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            editor.SetMacroModeCommand.Execute(null);

            Assert.True(editor.IsMacroMode);
            Assert.False(editor.HasEntry);
            Assert.Equal(string.Empty, editor.EntryText);
        }

        [Fact]
        public async Task SetSingleModeCommand_WhenTheEntryIsAlreadySingle_KeepsIt()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            editor.SetSingleModeCommand.Execute(null);

            Assert.Equal(new[] { "[a]" }, editor.EntryItems);
        }

        [Fact]
        public async Task BackspaceCommand_RemovesTheLastDisplayItem()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            _capture.RaiseKeystroke(Key("a"));
            _capture.RaiseKeystroke(Key("b"));

            Assert.True(editor.BackspaceCommand.CanExecute(null));

            editor.BackspaceCommand.Execute(null);

            Assert.Equal(new[] { "a" }, editor.EntryItems);

            editor.ClearEntryCommand.Execute(null);

            Assert.False(editor.HasEntry);
            Assert.False(editor.BackspaceCommand.CanExecute(null));
        }

        [Fact]
        public async Task DoneCommand_AfterTyping_CommitsTheEntryAndMarksTheRowModified()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            editor.DoneCommand.Execute(null);

            var row = editor.Inputs[LeftPedal];

            Assert.False(editor.IsEditing);
            Assert.False(row.IsEditing);
            Assert.True(row.IsModified);
            Assert.True(row.IsAssigned);
            Assert.Equal("[a]", row.AssignmentText);
            Assert.Equal(PedalInputRowViewModel.SingleModeCaption, row.ModeCaption);
            Assert.True(editor.HasUnsavedChanges);
            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task DoneCommand_WithAnUnchangedEntry_LeavesTheRowUnmodified()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            await BeginEditAsync(editor, LeftPedal);

            editor.DoneCommand.Execute(null);

            Assert.False(editor.Inputs[LeftPedal].IsModified);
            Assert.False(editor.HasUnsavedChanges);
            Assert.Equal("[lmouse]", editor.Inputs[LeftPedal].AssignmentText);
        }

        [Fact]
        public async Task DoneCommand_WithAnEmptyEntry_ClearsTheInput()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            await BeginEditAsync(editor, LeftPedal);

            editor.ClearEntryCommand.Execute(null);
            editor.DoneCommand.Execute(null);

            Assert.False(editor.Inputs[LeftPedal].IsAssigned);
            Assert.True(editor.Inputs[LeftPedal].IsModified);
            Assert.True(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task DoneCommand_OnAnUntouchedEmptyMacroRow_LeavesTheLineExactlyAsItWas()
        {
            // "{jack1}>" is a legitimate line: macro mode with an empty macro. Opening it and
            // pressing Done touches nothing — the row must not be badged Modified, and a later save
            // must not rewrite the line in the other bracket form.
            var editor = await LoadAsync("[lpedal]>", "{jack1}>");

            await BeginEditAsync(editor, Jack1);

            Assert.Equal(PedalInputMode.Macro, editor.EditMode);
            Assert.False(editor.HasEntry);

            editor.DoneCommand.Execute(null);

            Assert.False(editor.Inputs[Jack1].IsModified);
            Assert.False(editor.HasUnsavedChanges);
            Assert.False(editor.SaveCommand.CanExecute(null));

            // Something else has to change for a save to be possible at all; the untouched line
            // still has to survive it.
            await EditAsync(editor, LeftPedal, Key("a"));
            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(new[] { "[lpedal]>[a]", "{jack1}>" }, _fileService.ReadAllLines(PedalFilePath));
        }

        [Fact]
        public async Task CancelEditCommand_AfterTyping_LeavesTheInputAlone()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            editor.CancelEditCommand.Execute(null);

            Assert.False(editor.IsEditing);
            Assert.False(editor.Inputs[LeftPedal].IsModified);
            Assert.False(editor.HasUnsavedChanges);
            Assert.Equal("[lmouse]", editor.Inputs[LeftPedal].AssignmentText);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task BeginEditCommand_OnAnotherRowMidEdit_AppliesTheEntryWhenTheUserAnswersYes()
        {
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            Answer(MessageBoxResult.Yes);

            await BeginEditAsync(editor, MiddlePedal);

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(SavantElitePedalViewModel.PendingEditMessage, box.Message);
            Assert.Equal(MessageBoxButtons.YesNoCancel, box.Buttons);
            Assert.Equal("[a]", editor.Inputs[LeftPedal].AssignmentText);
            Assert.True(editor.Inputs[LeftPedal].IsModified);
            Assert.Same(editor.Inputs[MiddlePedal], editor.EditingRow);
            Assert.Equal(2, _capture.StartCount);
        }

        [Fact]
        public async Task BeginEditCommand_OnAnotherRowMidEdit_DropsTheEntryWhenTheUserAnswersNo()
        {
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            Answer(MessageBoxResult.No);

            await BeginEditAsync(editor, MiddlePedal);

            Assert.False(editor.Inputs[LeftPedal].IsAssigned);
            Assert.False(editor.Inputs[LeftPedal].IsModified);
            Assert.False(editor.HasUnsavedChanges);
            Assert.Same(editor.Inputs[MiddlePedal], editor.EditingRow);
        }

        [Fact]
        public async Task BeginEditCommand_OnAnotherRowMidEdit_ChangesNothingWhenTheUserCancels()
        {
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            Answer(MessageBoxResult.Cancel);

            await BeginEditAsync(editor, MiddlePedal);

            Assert.Same(editor.Inputs[LeftPedal], editor.EditingRow);
            Assert.Equal(new[] { "[a]" }, editor.EntryItems);
            Assert.False(editor.Inputs[LeftPedal].IsAssigned);
            Assert.Equal(1, _capture.StartCount);
            Assert.Equal(0, _capture.StopCount);
        }

        [Fact]
        public async Task BeginEditCommand_MidEdit_SuspendsCaptureWhileTheQuestionIsUp()
        {
            // The box is a window of its own: capture swallows every key of the focused window
            // while it runs, so it has to let go for the dialog's own Enter/Escape.
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            var suspendedWhileShowing = false;

            _notifications.MessageBoxShowing = _ => suspendedWhileShowing = _capture.IsSuspended;

            Answer(MessageBoxResult.Cancel);

            await BeginEditAsync(editor, MiddlePedal);

            Assert.True(suspendedWhileShowing);
            Assert.Equal(1, _capture.SuspendCount);
            Assert.Equal(1, _capture.ResumeCount);
            Assert.False(_capture.IsSuspended);
            Assert.True(_capture.IsCapturing);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithTheLeftMouseDoubleClick_AppendsTheWholeTriple()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.ApplySpecialActionCommand.Execute(Action(editor, "left-mouse-double-click"));

            // The item is macro-only, so applying it switched the mode for the user (§6).
            Assert.True(editor.IsMacroMode);
            Assert.Equal(new[] { PedalActionDescriber.LeftMouseDoubleClickText }, editor.EntryItems);
            Assert.False(editor.HasEntryMessage);

            editor.DoneCommand.Execute(null);

            Assert.Equal(PedalActionDescriber.LeftMouseDoubleClickText, editor.Inputs[LeftPedal].AssignmentText);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithDifferentPressReleaseOnAnEmptyEntry_ExplainsTheRefusal()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);
            editor.ApplySpecialActionCommand.Execute(Action(editor, "different-press-release"));

            Assert.Equal(SavantElitePedalViewModel.EmptyEntryMessage, editor.EntryMessage);
            Assert.True(editor.HasEntryMessage);
            Assert.False(editor.HasEntry);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithDifferentPressReleaseOnAModifier_ExplainsTheRefusal()
        {
            // {-lshift}{ }{+lshift} reloads as a Shift-modified space (12 §4.4), so the flag is
            // refused rather than written and lost.
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            _capture.RaiseKeystroke(Key("lshift"));

            editor.ApplySpecialActionCommand.Execute(Action(editor, "different-press-release"));

            Assert.Equal(SavantElitePedalViewModel.ModifierKeystrokeMessage, editor.EntryMessage);
            Assert.Equal(new[] { "lshift" }, editor.EntryItems);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithDifferentPressReleaseInSingleMode_SaysToSwitchMode()
        {
            // The entry is not empty — it is in the wrong mode, and switching would clear it (§5
            // step 2), so the refusal names the real reason instead of "add a keystroke first".
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            editor.ApplySpecialActionCommand.Execute(Action(editor, "different-press-release"));

            Assert.Equal(SavantElitePedalViewModel.ModeNotSupportedMessage, editor.EntryMessage);
            Assert.Equal(new[] { "[a]" }, editor.EntryItems);
            Assert.True(editor.IsSingleMode);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithTheWinModifier_LatchesItOntoEveryLaterKeystroke()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);
            editor.ApplySpecialActionCommand.Execute(Action(editor, "hold-win-modifier"));

            Assert.True(editor.HoldWinModifier);
            Assert.True(Action(editor, "hold-win-modifier").IsChecked);

            _capture.RaiseKeystroke(Key("a"));

            Assert.Equal(new[] { "{win+a}" }, editor.EntryItems);
        }

        [Fact]
        public async Task ApplySpecialActionCommand_WithTheWinModifierOnAnEmptyEntry_EnablesClear()
        {
            // Clear is the only way to release the latch, so it has to become available the moment
            // the latch goes on — even though the entry is still empty, which changes no other
            // property the command could have been re-evaluated from.
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            Assert.False(editor.ClearEntryCommand.CanExecute(null));

            var notifications = 0;

            editor.ClearEntryCommand.CanExecuteChanged += (_, _) => notifications++;

            editor.ApplySpecialActionCommand.Execute(Action(editor, "hold-win-modifier"));

            Assert.True(editor.ClearEntryCommand.CanExecute(null));
            Assert.True(notifications > 0);

            editor.ClearEntryCommand.Execute(null);

            Assert.False(editor.HoldWinModifier);
            Assert.False(editor.ClearEntryCommand.CanExecute(null));
        }

        [Fact]
        public async Task SpecialActionGroups_MakeTheWinModifierTheirOnlyCheckableItem()
        {
            // The view binds each menu item's ToggleType from IsCheckable rather than declaring
            // them all check boxes: Avalonia's menu interaction handler check-marks every CheckBox
            // item it clicks and a one-way IsChecked binding never takes the mark back, so a
            // blanket CheckBox left the whole menu wearing marks for actions that latch nothing.
            var editor = await LoadAsync("[lpedal]>");

            var checkable = new List<string>();

            foreach (var group in editor.SpecialActionGroups)
            {
                foreach (var action in group.Actions)
                {
                    if (action.IsCheckable)
                    {
                        checkable.Add(action.Action.Id);
                    }
                }
            }

            Assert.Equal(new[] { "hold-win-modifier" }, checkable);

            await BeginEditAsync(editor, LeftPedal);

            editor.ApplySpecialActionCommand.Execute(Action(editor, "copy-ctrl"));

            Assert.Equal(new[] { "{ctrl+c}" }, editor.EntryItems);
            Assert.All(
                editor.SpecialActionGroups,
                group => Assert.All(group.Actions, action => Assert.False(action.IsChecked)));
        }

        [Fact]
        public async Task SpecialActionGroups_FollowTheModeTheEntryIsIn()
        {
            var editor = await LoadAsync("[lpedal]>");

            Assert.Equal(PedalSpecialActions.Groups.Count, editor.SpecialActionGroups.Count);
            Assert.Equal("MOUSE ACTIONS", editor.SpecialActionGroups[0].Name);
            Assert.All(editor.SpecialActionGroups, group => Assert.All(group.Actions, action => Assert.False(action.IsEnabled)));

            await BeginEditAsync(editor, LeftPedal);

            // Single mode: the media keys are offered, the macro-only double click is not (§6).
            Assert.True(Action(editor, "mute").IsAvailable);
            Assert.False(Action(editor, "left-mouse-double-click").IsAvailable);

            editor.SetMacroModeCommand.Execute(null);

            Assert.False(Action(editor, "mute").IsAvailable);
            Assert.True(Action(editor, "left-mouse-double-click").IsAvailable);
        }

        [Fact]
        public async Task SaveCommand_BeforeAnythingIsEdited_IsUnavailable()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            Assert.False(editor.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task SaveCommand_AfterAnEdit_WritesTheFileAndClearsTheModifiedFlags()
        {
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            Assert.True(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(PedalFilePath, Assert.Single(_fileService.WrittenPaths));
            Assert.Equal("[lpedal]>[a]", _fileService.ReadAllLines(PedalFilePath)[0]);
            Assert.False(editor.HasUnsavedChanges);
            Assert.All(editor.Inputs, row => Assert.False(row.IsModified));
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.IsBusy);

            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(SavantElitePedalViewModel.SavedToastTitle, toast.Title);
            Assert.Equal(SavantElitePedalViewModel.SavedToastMessage, toast.Message);

            // A successful save says nothing but the toast: no dialog, and no eject to report —
            // 12 §5 step 7 has the firmware reload on the physical mode switch, which is why this
            // editor is built without an eject dependency at all.
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task SaveCommand_WithAMacroOfCapturedKeystrokes_WritesTheHeldModifiersAroundThem()
        {
            // The acceptance criterion of the issue: Shift+a then b shows {lshift+a}, b and is
            // written as one modifier wrap around the a. Real capture distinguishes the two Shift
            // keys, so what lands in the file is the sided spelling; 12 §4.4 accepts both that and
            // the generic {-shift}, and the parser reads either back into the same macro.
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.SetMacroModeCommand.Execute(null);

            _capture.RaiseKeystroke(Key("a"), Key("lshift"));
            _capture.RaiseKeystroke(Key("b"));

            Assert.Equal(new[] { "{lshift+a}", "b" }, editor.EntryItems);

            editor.DoneCommand.Execute(null);

            Assert.Equal("{lshift+a}b", editor.Inputs[LeftPedal].AssignmentText);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal("{lpedal}>{-lshift}{a}{+lshift}{b}", _fileService.ReadAllLines(PedalFilePath)[0]);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhileASaveIsInFlight_RefusesWithAToastInsteadOfAQuestion()
        {
            // Leaving mid-save would dispose this editor and eject the volume while the write is
            // still running, and the question would be the wrong one anyway: the changes are being
            // saved, not unsaveable. The refusal is not silent, though — Home is a live button
            // during the save (the loading overlay takes no clicks), so it has to say something.
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            var gate = new TaskCompletionSource();

            _fileService.WriteGate = gate;

            var save = editor.SaveCommand.ExecuteAsync(null);

            Assert.True(editor.IsBusy);
            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);

            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(SavantElitePedalViewModel.SaveInProgressTitle, toast.Title);
            Assert.Equal(SavantElitePedalViewModel.SaveInProgressMessage, toast.Message);

            gate.SetResult();

            await save;

            // The moment the write finishes there is nothing left to ask about, and leaving works.
            Assert.False(editor.HasUnsavedChanges);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task SaveCommand_ShowsAndHidesTheSavingIndicator()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));
            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(new string?[] { SavantElitePedalViewModel.SavingCaption, null }, _notifications.LoadingHistory);
            Assert.Null(_notifications.LoadingCaption);
        }

        [Fact]
        public async Task SaveCommand_WhenTheSavingIndicatorCannotBeShown_ReportsItWithoutStayingBusy()
        {
            // ShowLoading fans out to the overlay, so it can fail like any other UI callback. It
            // runs inside the try: the save did not happen, and it is reported as the save failure
            // it is instead of escaping a command nobody awaits.
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            _notifications.ShowLoadingExceptionToThrow = new InvalidOperationException("no overlay");

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.StartsWith(
                SavantElitePedalViewModel.SaveErrorMessagePrefix,
                Assert.Single(_notifications.MessageBoxes).Message);
            Assert.False(editor.IsBusy);
            Assert.True(editor.HasUnsavedChanges);
            Assert.True(editor.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task SaveCommand_WhenTheSavingIndicatorCannotBeHidden_StillLeavesTheEditorUsable()
        {
            // IsBusy is cleared before HideLoading, so a failure there cannot strand the flag: a
            // stuck IsBusy would disable Save and every entry command *and* make ConfirmCloseAsync
            // refuse Home forever, trapping the user in the editor with their changes.
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            _notifications.HideLoadingExceptionToThrow = new InvalidOperationException("no overlay");

            await Assert.ThrowsAsync<InvalidOperationException>(() => editor.SaveCommand.ExecuteAsync(null));

            Assert.False(editor.IsBusy);
            Assert.Equal(PedalFilePath, Assert.Single(_fileService.WrittenPaths));

            _notifications.HideLoadingExceptionToThrow = null;

            // The way out is open again: Home asks its question instead of refusing outright.
            Answer(MessageBoxResult.No);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.True(editor.BeginEditCommand.CanExecute(editor.Inputs[LeftPedal]));
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_WhenTheWriteFails_ReportsItAndKeepsTheChangesPending()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            _fileService.SetUnreadable(PedalFilePath);

            await editor.SaveCommand.ExecuteAsync(null);

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(SavantElitePedalViewModel.SaveTitle, box.Title);
            Assert.StartsWith(SavantElitePedalViewModel.SaveErrorMessagePrefix, box.Message);
            Assert.Equal(MessageBoxIcon.Error, box.Icon);
            Assert.True(editor.HasUnsavedChanges);
            Assert.True(editor.Inputs[LeftPedal].IsModified);
            Assert.Empty(_notifications.Toasts);
            Assert.False(editor.IsBusy);
        }

        [Fact]
        public async Task SaveCommand_InDemoMode_IsUnavailableEvenWithChanges()
        {
            // Demo mode never writes (03 §3.5), but the inputs are still editable so the user can
            // see what the app would do.
            var editor = await CreateLoadedAsync(VDriveConnectionStatus.CannotAccess);

            Assert.True(editor.CanEdit);

            await EditAsync(editor, LeftPedal, Key("a"));

            Assert.True(editor.HasUnsavedChanges);
            Assert.False(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Empty(_fileService.WrittenPaths);
        }

        [Fact]
        public async Task SaveCommand_WithoutAPedalFileOnTheDrive_CreatesIt()
        {
            // 12 §5 step 8's "Create a new file?" — the file simply does not exist yet, and the
            // save writes all seven lines (12 §4.6).
            var editor = await CreateLoadedAsync(VDriveConnectionStatus.Connected);

            Assert.Equal(PedalLoadState.FileMissing, editor.LoadState);
            Assert.True(editor.CanEdit);

            await EditAsync(editor, LeftPedal, Key("a"));

            Assert.True(editor.SaveCommand.CanExecute(null));

            await editor.SaveCommand.ExecuteAsync(null);

            var written = _fileService.ReadAllLines(PedalFilePath);

            Assert.Equal(PedalInputs.Count, written.Count);
            Assert.Equal("[lpedal]>[a]", written[0]);
            Assert.False(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task SaveCommand_WithAnEntryStillOpen_DropsItRatherThanWritingIt()
        {
            var editor = await LoadAsync("[lpedal]>", "[mpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));
            await BeginEditAsync(editor, MiddlePedal);

            _capture.RaiseKeystroke(Key("b"));

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.IsEditing);
            Assert.False(editor.Inputs[MiddlePedal].IsAssigned);
            Assert.Equal("[a]", editor.Inputs[LeftPedal].AssignmentText);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WithoutChanges_LeavesWithoutAsking()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WithAnEntryStillOpen_DropsItAndLeaves()
        {
            var editor = await LoadAsync("[lpedal]>[lmouse]");

            await BeginEditAsync(editor, LeftPedal);

            _capture.RaiseKeystroke(Key("a"));

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.False(editor.IsEditing);
            Assert.Equal("[lmouse]", editor.Inputs[LeftPedal].AssignmentText);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserSaves_WritesTheFileAndLeaves()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            Answer(MessageBoxResult.Yes);

            Assert.True(await editor.ConfirmCloseAsync());

            var box = Assert.Single(_notifications.MessageBoxes);

            // The shared question of mockup 1f, in the board's own words: three answers, the wide
            // card, and Discard painted as the one that loses data.
            Assert.Equal(UnsavedChangesPrompt.Title, box.Title);
            Assert.Equal(UnsavedChangesPrompt.PedalMessage, box.Message);
            Assert.Equal(MessageBoxButtons.YesNoCancel, box.Buttons);
            Assert.Equal(UnsavedChangesPrompt.SaveCaption, box.YesCaption);
            Assert.Equal(UnsavedChangesPrompt.DiscardCaption, box.NoCaption);
            Assert.Equal(UnsavedChangesPrompt.CancelCaption, box.CancelCaption);
            Assert.Equal(MessageBoxResult.No, box.DestructiveResult);
            Assert.True(box.IsWide);
            Assert.Equal(PedalFilePath, Assert.Single(_fileService.WrittenPaths));
            Assert.False(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task ConfirmCloseAsync_OnThePedal_OffersNoDontAskAgainOptOut()
        {
            // Deliberate, and the reason is structural rather than editorial: the opt-out promises
            // "always save on leaving", and the only surface that can take the promise back is the
            // Settings tab's "App & notifications" section. The Savant Elite2 is
            // SettingsCapability.None, so it renders no Settings tab at all — a user who ticked the
            // box here could never untick it. It gets the checkbox with issues #53 / #96.
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            Answer(MessageBoxResult.Cancel);

            await editor.ConfirmCloseAsync();

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Null(box.SuppressionKey);
            Assert.False(box.HasSuppressionOption);
            Assert.Null(box.SuppressionResult);
            Assert.Null(box.SuppressionCaption);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserDiscards_LeavesWithoutWriting()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            Answer(MessageBoxResult.No);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_fileService.WrittenPaths);
            Assert.True(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserCancels_StaysInTheEditor()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            Answer(MessageBoxResult.Cancel);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Empty(_fileService.WrittenPaths);
            Assert.True(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheSaveFails_StaysInTheEditor()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            _fileService.SetUnreadable(PedalFilePath);
            Answer(MessageBoxResult.Yes);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.True(editor.HasUnsavedChanges);
            Assert.Equal(2, _notifications.MessageBoxes.Count);
            Assert.StartsWith(SavantElitePedalViewModel.SaveErrorMessagePrefix, _notifications.MessageBoxes[1].Message);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheChangesCannotBeSaved_AsksToDiscardThemInstead()
        {
            // A demo session can edit but can never write (03 §3.5): offering "save" there would
            // be a question with no working answer.
            var editor = await CreateLoadedAsync(VDriveConnectionStatus.CannotAccess);

            await EditAsync(editor, LeftPedal, Key("a"));

            Answer(MessageBoxResult.No);

            Assert.False(await editor.ConfirmCloseAsync());

            var box = Assert.Single(_notifications.MessageBoxes);

            // Its own wording, not the pedal's: every clause of PedalMessage is about a save, and
            // this card has no Save on it. Two answers, of which Discard is the affirmative because
            // it is the only one that moves — which is exactly why Interpret has to be told whether
            // saving was on offer.
            Assert.Equal(UnsavedChangesPrompt.CannotSaveTitle, box.Title);
            Assert.Equal(UnsavedChangesPrompt.CannotSaveMessage, box.Message);
            Assert.DoesNotContain("Saving writes", box.Message, StringComparison.Ordinal);
            Assert.Equal(MessageBoxButtons.YesNo, box.Buttons);
            Assert.Equal(UnsavedChangesPrompt.DiscardCaption, box.YesCaption);
            Assert.Equal(UnsavedChangesPrompt.CancelCaption, box.NoCaption);
            Assert.Equal(MessageBoxResult.Yes, box.DestructiveResult);
            Assert.Null(box.SuppressionKey);

            Answer(MessageBoxResult.Yes);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_fileService.WrittenPaths);
        }

        [Fact]
        public async Task ConfirmCloseAsync_SuspendsCaptureWhileTheQuestionIsUp()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            var suspendedWhileShowing = false;

            _notifications.MessageBoxShowing = _ => suspendedWhileShowing = _capture.IsSuspended;
            Answer(MessageBoxResult.No);

            await editor.ConfirmCloseAsync();

            Assert.True(suspendedWhileShowing);
            Assert.Equal(1, _capture.SuspendCount);
            Assert.Equal(1, _capture.ResumeCount);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheQuestionCannotBeShown_StaysInTheEditor()
        {
            var editor = await LoadAsync("[lpedal]>");

            await EditAsync(editor, LeftPedal, Key("a"));

            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no window");

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.True(editor.HasUnsavedChanges);
        }

        [Fact]
        public async Task Dispose_WhileAnEntryIsOpen_StopsCaptureAndDetaches()
        {
            var editor = await LoadAsync("[lpedal]>");

            await BeginEditAsync(editor, LeftPedal);

            editor.Dispose();
            editor.Dispose();

            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.HasSubscribers);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task LoadAsync_AfterDispose_ReadsNothing()
        {
            _fileService.SetFile(PedalFilePath, "[lpedal]>[lmouse]");

            var editor = Create(VDriveConnectionStatus.Connected);

            editor.Dispose();

            await editor.LoadAsync();

            Assert.Equal(0, _fileService.ReadCount);
            Assert.Equal(PedalLoadState.None, editor.LoadState);
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }

            _capture.Dispose();
        }

        private static KeyDefinition Key(string token)
        {
            return PedalTokenMap.Resolve(token)
                   ?? throw new InvalidOperationException($"No pedal token \"{token}\".");
        }

        private static PedalSpecialActionViewModel Action(SavantElitePedalViewModel editor, string id)
        {
            foreach (var group in editor.SpecialActionGroups)
            {
                foreach (var action in group.Actions)
                {
                    if (action.Action.Id == id)
                    {
                        return action;
                    }
                }
            }

            throw new InvalidOperationException($"No special action \"{id}\".");
        }

        private static Task BeginEditAsync(SavantElitePedalViewModel editor, int rowIndex)
        {
            return editor.BeginEditCommand.ExecuteAsync(editor.Inputs[rowIndex]);
        }

        /// <summary>Programs one input end to end: Edit, one keypress, Done.</summary>
        private async Task EditAsync(SavantElitePedalViewModel editor, int rowIndex, KeyDefinition key)
        {
            await BeginEditAsync(editor, rowIndex);

            _capture.RaiseKeystroke(key);

            editor.DoneCommand.Execute(null);
        }

        private void Answer(MessageBoxResult result)
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = result };
        }

        private async Task<SavantElitePedalViewModel> LoadAsync(params string[] lines)
        {
            _fileService.SetFile(PedalFilePath, lines);

            return await CreateLoadedAsync(VDriveConnectionStatus.Connected);
        }

        private async Task<SavantElitePedalViewModel> CreateLoadedAsync(VDriveConnectionStatus status)
        {
            var editor = Create(status);

            await editor.LoadAsync();

            return editor;
        }

        private SavantElitePedalViewModel Create(VDriveConnectionStatus status)
        {
            var editor = new SavantElitePedalViewModel(
                TestDevices.CreateSnapshot(DeviceId.SavantElite2, status),
                _pedalFiles,
                _capture,
                _notifications);

            _editors.Add(editor);

            return editor;
        }
    }
}
