using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Savant Elite2 editor: it loads <c>active/pedals.txt</c>, shows the seven inputs of
    /// specs/12-savant-elite.md §5, and programs them — click <b>Edit</b> on a row, press keys (or
    /// pick a Special Action of §6), then Done, and Save writes the file back.
    /// <para>
    /// It coordinates and owns no rules of its own: the entry buffer, its Single/Macro modes and
    /// the Special Actions catalog are Core's <see cref="PedalEditSession"/> /
    /// <see cref="PedalSpecialActions"/> (docs/app/savant-elite.md), keystrokes arrive from
    /// <see cref="IKeystrokeCaptureService"/> (docs/app/keystroke-capture.md), and the file is read
    /// and written by <see cref="PedalFileService"/>. There is no eject: the SE2 reloads on its
    /// physical mode switch (§5 step 7).
    /// </para>
    /// </summary>
    public sealed class SavantElitePedalViewModel : DeviceEditorViewModel, IDisposable
    {
        /// <summary>Subtitle describing what the view does.</summary>
        public const string EditorNotice = "Click Edit on an input, then press the keys you want it to send.";

        /// <summary>Note above the rows (12 §1, §5: the file describes all seven possible inputs).</summary>
        public const string AllInputsNote = "All seven possible inputs are listed — your product has only some of these pedals and jacks.";

        /// <summary>Note shown instead of a file's contents while the session is in demo mode.</summary>
        public const string DemoModeMessage = "Demo mode: no pedal v-Drive is connected, so nothing was read from a device. Every input is shown unassigned.";

        /// <summary>Note shown when the drive carries no pedal file yet (12 §5 step 8).</summary>
        public const string MissingFileMessage = "This v-Drive has no active/pedals.txt yet, so every input is shown unassigned.";

        /// <summary>Note shown when the file was read but programs nothing.</summary>
        public const string EmptyFileMessage = "The pedal configuration file has no assignments — every input is unassigned.";

        /// <summary>Prefix of the note shown when the file could not be read at all.</summary>
        public const string LoadFailureMessagePrefix = "Could not read the pedal configuration file: ";

        /// <summary>Heading of the unparseable-lines section.</summary>
        public const string InvalidLinesHeading = "Lines that could not be read";

        /// <summary>Explanation under <see cref="InvalidLinesHeading"/>.</summary>
        public const string InvalidLinesNote = "These lines name a pedal input but could not be understood. They are listed rather than dropped silently; nothing on the device has been changed.";

        /// <summary>Warning under <see cref="InvalidLinesNote"/>: a save regenerates those lines (12 §4.6).</summary>
        public const string InvalidLinesSaveNote = "Saving rewrites each of these lines from the input shown above it, so the text below is replaced.";

        /// <summary>Note next to Save while the drive has no pedal file yet (12 §5 step 8).</summary>
        public const string MissingFileSaveNote = "Saving creates active/pedals.txt on the v-Drive.";

        /// <summary>Note next to Save while the session is a demo one (03 §3.5).</summary>
        public const string DemoModeSaveNote = "Saving is unavailable in demo mode — connect a writable pedal v-Drive to keep changes.";

        /// <summary>Hint under the entry box (12 §5 step 3, §6).</summary>
        public const string EntryHint = "Press keys to record them. Use Special Actions for mouse, media, delays and shortcuts.";

        /// <summary>Prefix of the entry box's heading; the input's name follows it (12 §5 step 1).</summary>
        public const string EditingCaptionPrefix = "EDITING: ";

        /// <summary>Title of the dialogs the editor raises about one input.</summary>
        public const string EditTitle = "Pedal Configuration";

        /// <summary>The mid-edit question of 12 §5 step 1, asked when another input is picked.</summary>
        public const string PendingEditMessage = "Key modification in progress, apply changes?";

        /// <summary>
        /// Its affirmative, named after what it does rather than "Yes" (docs/design/mockups.md,
        /// mockup 1k: "the two affirmative choices are labelled by outcome, not Yes/No"). It still
        /// answers <c>Yes</c>.
        /// </summary>
        public const string ApplyEditCaption = "Apply edit";

        /// <summary>Its other affirmative — the edit goes, the navigation proceeds. Still <c>No</c>.</summary>
        public const string DiscardEditCaption = "Discard edit";

        /// <summary>The way out of every question this editor asks. Still <c>Cancel</c>, or <c>No</c> where there is none.</summary>
        public const string StayHereCaption = "Cancel";

        /// <summary>Title of everything a save raises — its failure dialog and its post-save toast.</summary>
        public const string SaveTitle = "Save Pedal Configuration";

        /// <summary>
        /// Caption of the loading indicator during a save. Not a spec string; the ellipsis is the
        /// one character mockup 1k uses, as in <see cref="LoadingCaptions.Default"/>.
        /// </summary>
        public const string SavingCaption = "Saving…";

        /// <summary>
        /// Title of the toast raised when a navigation is refused because a save is running. Both
        /// editors refuse in the same words, so the wording lives with the shared prompt; this is
        /// an alias, not a second string.
        /// </summary>
        public const string SaveInProgressTitle = UnsavedChangesPrompt.SaveInProgressTitle;

        /// <summary>
        /// Message of that toast. The loading indicator is now <b>blocking</b> — it sits on the
        /// same scrim the message box uses (mockup 1k, "Loading · blocking") — so the pointer can
        /// no longer reach Home while a save is running, and this refusal is the second line of
        /// defence rather than the first. It is kept, and it still speaks: the guard is reachable
        /// from any path that is not a click, and a navigation that silently did nothing would be
        /// worse than one that says why.
        /// </summary>
        public const string SaveInProgressMessage = UnsavedChangesPrompt.SaveInProgressMessage;

        /// <summary>Message prefix when the save threw; the exception's message follows it.</summary>
        public const string SaveErrorMessagePrefix = "The pedal configuration could not be saved: ";

        /// <summary>Title of the post-save toast (12 §5 step 7's "SAVE DONE.", modern casing).</summary>
        public const string SavedToastTitle = "Configuration saved";

        /// <summary>
        /// The post-save instruction of 12 §5 step 7 ("NOW CHANGE YOUR SE2 TO 'PLAY MODE' TO
        /// IMPLEMENT CHANGES") in this app's casing. There is no eject — the firmware reloads on
        /// the physical mode switch.
        /// </summary>
        public const string SavedToastMessage = "Switch your Savant Elite2 back to play mode to apply the changes.";

        /// <summary>Why a Special Action was refused for want of anything to apply it to (§6).</summary>
        public const string EmptyEntryMessage = "Add a keystroke first.";

        /// <summary>
        /// Why a Special Action was refused in the mode the entry is in (§6). Switching for the user
        /// would clear the entry (§5 step 2), so the switch is left to them.
        /// </summary>
        public const string ModeNotSupportedMessage = "Switch to Macro first — this action needs a macro entry.";

        /// <summary>Why "Different Press &amp; Release" cannot flag a modifier key (§6; §4.4's <c>{ }</c> rule).</summary>
        public const string ModifierKeystrokeMessage = "Different Press & Release cannot be applied to a modifier key.";

        /// <summary>Builds the entry box's heading for <paramref name="inputName"/>.</summary>
        public static string BuildEditingCaption(string inputName)
        {
            return EditingCaptionPrefix + inputName;
        }

        /// <summary>The seven inputs in <see cref="PedalInputs.All"/> order, always all of them.</summary>
        public IReadOnlyList<PedalInputRowViewModel> Inputs
        {
            get => _inputs;
            private set => SetProperty(ref _inputs, value);
        }

        /// <summary>Every line that named an input but could not be applied (12 §4.2).</summary>
        public IReadOnlyList<PedalInvalidLineViewModel> InvalidLines
        {
            get => _invalidLines;
            private set
            {
                if (SetProperty(ref _invalidLines, value))
                {
                    OnPropertyChanged(nameof(HasInvalidLines));
                }
            }
        }

        /// <summary>Whether the unparseable-lines section has anything to show.</summary>
        public bool HasInvalidLines => InvalidLines.Count > 0;

        /// <summary>The Special Actions menu of 12 §6, five sections in menu order.</summary>
        public IReadOnlyList<PedalSpecialActionGroupViewModel> SpecialActionGroups { get; }

        /// <summary>Where the seven rows came from; the view colors <see cref="StatusMessage"/> from it.</summary>
        public PedalLoadState LoadState
        {
            get => _loadState;
            private set
            {
                if (SetProperty(ref _loadState, value))
                {
                    OnPropertyChanged(nameof(CanEdit));
                    OnPropertyChanged(nameof(SaveNote));
                    OnPropertyChanged(nameof(HasSaveNote));

                    NotifyCommands();
                }
            }
        }

        /// <summary>The demo / missing-file / empty-file / failure note; empty when there is nothing to say.</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                if (SetProperty(ref _statusMessage, value))
                {
                    OnPropertyChanged(nameof(HasStatusMessage));
                }
            }
        }

        /// <summary>Whether <see cref="StatusMessage"/> has anything to show.</summary>
        public bool HasStatusMessage => StatusMessage.Length > 0;

        /// <summary>The file this view was loaded from; empty in demo mode (12 §5 "File name:").</summary>
        public string FilePath
        {
            get => _filePath;
            private set
            {
                if (SetProperty(ref _filePath, value))
                {
                    OnPropertyChanged(nameof(HasFilePath));
                }
            }
        }

        /// <summary>Whether there is a file path to show.</summary>
        public bool HasFilePath => FilePath.Length > 0;

        /// <summary>The subtitle under the device name.</summary>
        public string Notice => EditorNotice;

        /// <summary>The "not every input exists on your product" note.</summary>
        public string InputsNote => AllInputsNote;

        /// <summary>What the file cannot say about saving: demo mode, or a file that does not exist yet.</summary>
        public string SaveNote => LoadState switch
        {
            PedalLoadState.DemoMode => DemoModeSaveNote,
            PedalLoadState.FileMissing => MissingFileSaveNote,
            _ => string.Empty
        };

        /// <summary>Whether <see cref="SaveNote"/> has anything to show.</summary>
        public bool HasSaveNote => SaveNote.Length > 0;

        /// <summary>Whether the file is still being read; the rows are the empty seven until it is not.</summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>Whether a save is in flight.</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>
        /// Whether the inputs may be programmed at all. A file that could not be read is shown
        /// read-only: saving would rewrite lines nobody could see.
        /// </summary>
        public bool CanEdit => LoadState is PedalLoadState.Loaded or PedalLoadState.FileMissing or PedalLoadState.DemoMode;

        /// <summary>The row the entry box is editing, or null when no edit is in progress.</summary>
        public PedalInputRowViewModel? EditingRow
        {
            get => _editingRow;
            private set
            {
                if (SetProperty(ref _editingRow, value))
                {
                    OnPropertyChanged(nameof(IsEditing));
                    OnPropertyChanged(nameof(EditingInputName));
                    OnPropertyChanged(nameof(EditingCaption));

                    NotifyCommands();
                }
            }
        }

        /// <summary>Whether an input is being programmed right now.</summary>
        public bool IsEditing => EditingRow is not null;

        /// <summary>Name of the input being programmed; empty when none is.</summary>
        public string EditingInputName => EditingRow?.Name ?? string.Empty;

        /// <summary>The entry box's heading (12 §5 step 1).</summary>
        public string EditingCaption => BuildEditingCaption(EditingInputName);

        /// <summary>Single Action or Multiple Actions (12 §5 step 2); never <see cref="PedalInputMode.None"/>.</summary>
        public PedalInputMode EditMode
        {
            get => _editMode;
            private set
            {
                if (SetProperty(ref _editMode, value))
                {
                    OnPropertyChanged(nameof(IsSingleMode));
                    OnPropertyChanged(nameof(IsMacroMode));
                }
            }
        }

        /// <summary>Whether the entry is a single key or mouse action.</summary>
        public bool IsSingleMode => EditMode == PedalInputMode.Single;

        /// <summary>Whether the entry is a macro.</summary>
        public bool IsMacroMode => EditMode == PedalInputMode.Macro;

        /// <summary>The entry as the box shows it, one item per display item (12 §5).</summary>
        public IReadOnlyList<string> EntryItems
        {
            get => _entryItems;
            private set
            {
                if (SetProperty(ref _entryItems, value))
                {
                    OnPropertyChanged(nameof(HasEntry));

                    NotifyCommands();
                }
            }
        }

        /// <summary>The whole entry as one string — the items concatenated, spaces made visible.</summary>
        public string EntryText
        {
            get => _entryText;
            private set => SetProperty(ref _entryText, value);
        }

        /// <summary>Whether the entry holds anything.</summary>
        public bool HasEntry => EntryItems.Count > 0;

        /// <summary>Whether the §6 "Windows Combination (Win + …)" modifier is latched.</summary>
        public bool HoldWinModifier
        {
            get => _holdWinModifier;
            private set
            {
                if (SetProperty(ref _holdWinModifier, value))
                {
                    // Clear is offered for a latched modifier over an empty entry, and latching it
                    // changes no other property: EntryItems keeps handing back the same empty list,
                    // which SetProperty sees as unchanged and does not report.
                    NotifyCommands();
                }
            }
        }

        /// <summary>Why the last Special Action was refused (§6); empty when there is nothing to say.</summary>
        public string EntryMessage
        {
            get => _entryMessage;
            private set
            {
                if (SetProperty(ref _entryMessage, value))
                {
                    OnPropertyChanged(nameof(HasEntryMessage));
                }
            }
        }

        /// <summary>Whether <see cref="EntryMessage"/> has anything to show.</summary>
        public bool HasEntryMessage => EntryMessage.Length > 0;

        /// <summary>Whether an input changed since the file was loaded.</summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set
            {
                if (SetProperty(ref _hasUnsavedChanges, value))
                {
                    NotifyCommands();
                }
            }
        }

        /// <summary>
        /// Opens the entry box on a row (12 §5 step 1). Asynchronous because an edit already in
        /// progress on another row is confirmed first.
        /// </summary>
        public IAsyncRelayCommand<PedalInputRowViewModel> BeginEditCommand { get; }

        /// <summary>Switches the entry to Single Action; a real switch clears it (12 §5 step 2).</summary>
        public IRelayCommand SetSingleModeCommand { get; }

        /// <summary>Switches the entry to Multiple Actions; a real switch clears it (12 §5 step 2).</summary>
        public IRelayCommand SetMacroModeCommand { get; }

        /// <summary>Removes the last display item of the entry (12 §5 step 5).</summary>
        public IRelayCommand BackspaceCommand { get; }

        /// <summary>Empties the entry (12 §5 step 5).</summary>
        public IRelayCommand ClearEntryCommand { get; }

        /// <summary>Closes the entry box without committing (12 §5 step 6, Cancel).</summary>
        public IRelayCommand CancelEditCommand { get; }

        /// <summary>Commits the entry to its input (12 §5 step 6, Done).</summary>
        public IRelayCommand DoneCommand { get; }

        /// <summary>Applies one Special Actions item to the entry (12 §6).</summary>
        public IRelayCommand<PedalSpecialActionViewModel> ApplySpecialActionCommand { get; }

        /// <summary>Writes <c>pedals.txt</c> back; never available in demo mode (03 §3.5).</summary>
        public IAsyncRelayCommand SaveCommand { get; }

        private readonly PedalFileService _pedalFiles;
        private readonly IKeystrokeCaptureService _capture;
        private readonly INotificationService _notifications;
        private readonly Action<CapturedKeystroke> _keystrokeCapturedHandler;
        private PedalConfiguration _configuration = new();
        private PedalEditSession? _session;
        private IReadOnlyList<PedalInputRowViewModel> _inputs;
        private IReadOnlyList<PedalInvalidLineViewModel> _invalidLines = [];
        private IReadOnlyList<string> _entryItems = [];
        private PedalInputRowViewModel? _editingRow;
        private PedalLoadState _loadState = PedalLoadState.None;
        private PedalInputMode _editMode = PedalInputMode.Single;
        private string _statusMessage = string.Empty;
        private string _filePath = string.Empty;
        private string _entryText = string.Empty;
        private string _entryMessage = string.Empty;
        private bool _holdWinModifier;
        private bool _hasUnsavedChanges;
        private bool _isLoading = true;
        private bool _isBusy;
        private bool _hasLoadStarted;
        private bool _isDisposed;

        /// <summary>
        /// Creates the editor for <paramref name="device"/>. Construction is deliberately cheap —
        /// no file is touched here — so the shell can swap the view in immediately and let
        /// <see cref="LoadAsync"/> do the reading; the seven rows already exist, unassigned, so the
        /// view is never blank.
        /// </summary>
        public SavantElitePedalViewModel(
            DeviceSnapshot device,
            PedalFileService pedalFiles,
            IKeystrokeCaptureService capture,
            INotificationService notifications) : base(device)
        {
            _pedalFiles = pedalFiles ?? throw new ArgumentNullException(nameof(pedalFiles));
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            _inputs = BuildRows(_configuration);

            BeginEditCommand = new AsyncRelayCommand<PedalInputRowViewModel>(BeginEditAsync, CanBeginEdit);
            SetSingleModeCommand = new RelayCommand(() => SetEditMode(PedalInputMode.Single), CanChangeEntry);
            SetMacroModeCommand = new RelayCommand(() => SetEditMode(PedalInputMode.Macro), CanChangeEntry);
            BackspaceCommand = new RelayCommand(Backspace, () => CanChangeEntry() && HasEntry);
            ClearEntryCommand = new RelayCommand(ClearEntry, () => CanChangeEntry() && (HasEntry || HoldWinModifier));
            CancelEditCommand = new RelayCommand(CancelEdit, () => IsEditing);
            DoneCommand = new RelayCommand(CommitEdit, CanChangeEntry);
            ApplySpecialActionCommand = new RelayCommand<PedalSpecialActionViewModel>(ApplySpecialAction, CanApplySpecialAction);
            SaveCommand = new AsyncRelayCommand(() => SaveAsync(), CanSave);

            SpecialActionGroups = PedalSpecialActionGroupViewModel.BuildAll(ApplySpecialActionCommand);

            _keystrokeCapturedHandler = OnKeystrokeCaptured;
            _capture.KeystrokeCaptured += _keystrokeCapturedHandler;
        }

        /// <summary>
        /// Reads <c>active/pedals.txt</c> off the UI thread and fills the rows in. Total: demo
        /// mode, a drive without the file, and an unreadable file are all reported as
        /// <see cref="LoadState"/> plus a note rather than thrown, because the shell fires this and
        /// forgets it. A second call is a no-op.
        /// </summary>
        public override async Task LoadAsync()
        {
            if (_isDisposed || _hasLoadStarted)
            {
                return;
            }

            _hasLoadStarted = true;
            IsLoading = true;

            LoadOutcome outcome;

            try
            {
                outcome = await Task.Run(Load).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                outcome = new LoadOutcome { State = PedalLoadState.LoadFailed, FailureMessage = exception.Message };
            }

            try
            {
                Apply(outcome);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// The unsaved-changes gate of 12 §5 step 9, asked by the shell before it closes the
        /// editor: Save writes and refuses to leave when the write failed, Discard leaves, Cancel
        /// stays. An edit still in the entry box is discarded first — it was never committed to an
        /// input. The question is <see cref="UnsavedChangesPrompt"/>'s, shared verbatim with the
        /// keyboard editor; only the sentence about what the save writes is this board's.
        /// <para>
        /// A save in flight refuses outright, without a question — but with a toast. The blocking
        /// loading card now covers Home with its scrim, so the *pointer* can no longer get here at
        /// all; this stays because the scrim is a UI fact and this is the invariant. Leaving would
        /// dispose this editor while <c>WriteAllLines</c> is still running; the write is short, and
        /// the navigation works the moment it finishes. The toast rides along so that any path
        /// which is not a click — a shortcut, a caller of this method — says why rather than
        /// appearing to do nothing.
        /// </para>
        /// </summary>
        public override async Task<bool> ConfirmCloseAsync()
        {
            if (IsBusy)
            {
                _notifications.ShowToast(new ToastRequest
                {
                    Title = SaveInProgressTitle,
                    Message = SaveInProgressMessage
                });

                return false;
            }

            CancelEdit();

            if (!HasUnsavedChanges)
            {
                return true;
            }

            // Demo mode and an unreadable file can hold edits but can never write them, so the
            // question degrades to "discard?" — a Save that cannot succeed would leave the user
            // with no way out of the editor.
            var canSave = CanEverSave();

            // canSuppress is false, and it is a property of this board rather than a preference.
            // The opt-out promises "always save on leaving", and the only place to take that
            // promise back is the "App & notifications" section of the Settings tab — which the
            // Savant Elite2 does not render, because it is SettingsCapability.None. A pedal user
            // who ticked the box could never untick it. It gets the checkbox when it gets a
            // preferences surface of its own (issues #53 / #96).
            var outcome = await ShowMessageBoxAsync(
                UnsavedChangesPrompt.Build(UnsavedChangesPrompt.PedalMessage, canSave, canSuppress: false))
                .ConfigureAwait(true);

            return UnsavedChangesPrompt.Interpret(outcome, canSave) switch
            {
                UnsavedChangesAnswer.Save => await SaveAsync().ConfigureAwait(true),
                UnsavedChangesAnswer.Discard => true,
                // Cancel, Escape, and a box that could not be shown all leave the editor open:
                // navigating away would discard the changes the question is about.
                _ => false
            };
        }

        private static IReadOnlyList<PedalInputRowViewModel> BuildRows(PedalConfiguration configuration)
        {
            var rows = new PedalInputRowViewModel[configuration.Inputs.Count];

            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = new PedalInputRowViewModel(configuration.Inputs[index]);
            }

            return new ReadOnlyCollection<PedalInputRowViewModel>(rows);
        }

        private static IReadOnlyList<PedalInvalidLineViewModel> BuildInvalidLines(IReadOnlyList<PedalInvalidLine> lines)
        {
            var rows = new PedalInvalidLineViewModel[lines.Count];

            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = new PedalInvalidLineViewModel(lines[index]);
            }

            return new ReadOnlyCollection<PedalInvalidLineViewModel>(rows);
        }

        private static string BuildStatusMessage(PedalLoadState state, bool isEmpty, string failureMessage)
        {
            return state switch
            {
                PedalLoadState.DemoMode => DemoModeMessage,
                PedalLoadState.FileMissing => MissingFileMessage,
                PedalLoadState.LoadFailed => LoadFailureMessagePrefix + failureMessage,
                PedalLoadState.Loaded when isEmpty => EmptyFileMessage,
                _ => string.Empty
            };
        }

        /// <summary>Folds the modifiers held with a captured keystroke into the macro model's flags.</summary>
        private static MacroModifiers ToModifiers(IReadOnlyList<KeyDefinition> heldModifiers)
        {
            var modifiers = MacroModifiers.None;

            foreach (var held in heldModifiers)
            {
                if (MacroModifierCodes.TryFromKeyCode(held.Code, out var modifier))
                {
                    modifiers |= modifier;
                }
            }

            return modifiers;
        }

        private static string DescribeFailure(PedalSpecialActionFailure failure)
        {
            return failure switch
            {
                PedalSpecialActionFailure.ModifierKeystroke => ModifierKeystrokeMessage,
                PedalSpecialActionFailure.ModeNotSupported => ModeNotSupportedMessage,
                _ => EmptyEntryMessage
            };
        }

        private LoadOutcome Load()
        {
            if (IsDemoMode || Device.Location is null)
            {
                // Demo mode never touches the drive (03 §3.5): even a present but unwritable
                // v-Drive is opened as a demo session, and the seven rows stand in for it.
                return new LoadOutcome { State = PedalLoadState.DemoMode };
            }

            var filePath = string.Empty;

            try
            {
                filePath = PedalFileService.GetPedalFilePath(Device.Location);

                var result = _pedalFiles.Load(Device.Location);

                return new LoadOutcome
                {
                    State = PedalLoadState.Loaded,
                    Configuration = result.Configuration,
                    InvalidLines = result.InvalidLines,
                    FilePath = filePath
                };
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                // A pedal that has never been programmed simply has no file; 12 §5 step 8 turns
                // that into the "Create a new file?" prompt, which here is a plain note plus a
                // Save that creates the file.
                return new LoadOutcome { State = PedalLoadState.FileMissing, FilePath = filePath };
            }
            catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException
                                                  or NotSupportedException
                                                  or ArgumentException
                                                  or InvalidOperationException)
            {
                return new LoadOutcome
                {
                    State = PedalLoadState.LoadFailed,
                    FailureMessage = exception.Message,
                    FilePath = filePath
                };
            }
        }

        private void Apply(LoadOutcome outcome)
        {
            _configuration = outcome.Configuration ?? new PedalConfiguration();

            Inputs = BuildRows(_configuration);
            InvalidLines = BuildInvalidLines(outcome.InvalidLines);
            FilePath = outcome.FilePath;
            LoadState = outcome.State;
            StatusMessage = BuildStatusMessage(outcome.State, _configuration.IsEmpty, outcome.FailureMessage);
        }

        private bool CanBeginEdit(PedalInputRowViewModel? row)
        {
            return row is not null && CanEdit && !IsLoading && !IsBusy;
        }

        /// <summary>
        /// 12 §5 step 1: opens the entry box on a row. An edit already open on <b>another</b> row
        /// is confirmed first — Yes commits it, No discards it, Cancel leaves everything as it is.
        /// Re-clicking the row already being edited does nothing; Cancel and Done are its exits.
        /// </summary>
        private async Task BeginEditAsync(PedalInputRowViewModel? row)
        {
            if (!CanBeginEdit(row) || ReferenceEquals(row, EditingRow))
            {
                return;
            }

            if (_session is not null && !await ConfirmPendingEditAsync().ConfigureAwait(true))
            {
                return;
            }

            // That question is modal, but the editor can still go away under it — the shell window
            // closing disposes it — and starting capture then would leave it running with nothing
            // subscribed to it.
            if (_isDisposed || !CanBeginEdit(row))
            {
                return;
            }

            StartEdit(row!);
        }

        /// <summary>
        /// Asks the mid-edit question of 12 §5 step 1 and applies the answer. Returns whether the
        /// caller may go on to open another row.
        /// </summary>
        private async Task<bool> ConfirmPendingEditAsync()
        {
            var outcome = await ShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = EditTitle,
                Message = PendingEditMessage,
                Icon = MessageBoxIcon.Confirmation,
                Buttons = MessageBoxButtons.YesNoCancel,
                YesCaption = ApplyEditCaption,
                NoCaption = DiscardEditCaption,
                CancelCaption = StayHereCaption
            }).ConfigureAwait(true);

            switch (outcome?.Result)
            {
                case MessageBoxResult.Yes:
                    CommitEdit();
                    return true;

                case MessageBoxResult.No:
                    CancelEdit();
                    return true;

                default:
                    // Cancel, Escape, and a box that could not be shown all keep the entry open.
                    return false;
            }
        }

        private void StartEdit(PedalInputRowViewModel row)
        {
            _session = PedalEditSession.BeginFor(row.Input);

            row.IsEditing = true;
            EditingRow = row;
            EntryMessage = string.Empty;

            RefreshEntry();

            _capture.Start();
        }

        /// <summary>12 §5 step 6 (Done): commits the entry and closes the box.</summary>
        private void CommitEdit()
        {
            var session = _session;
            var row = EditingRow;

            if (session is null || row is null)
            {
                return;
            }

            var changed = session.DiffersFrom(row.Input);

            session.ApplyTo(row.Input);

            EndEdit();

            row.RefreshFromModel();

            if (changed)
            {
                row.IsModified = true;
                HasUnsavedChanges = true;
            }
        }

        /// <summary>12 §5 step 6 (Cancel): drops the entry. The input was never touched.</summary>
        private void CancelEdit()
        {
            if (_session is null)
            {
                return;
            }

            EndEdit();
        }

        private void EndEdit()
        {
            _session = null;

            if (EditingRow is not null)
            {
                EditingRow.IsEditing = false;
            }

            EditingRow = null;
            EditMode = PedalInputMode.Single;
            HoldWinModifier = false;
            EntryMessage = string.Empty;

            EntryItems = [];
            EntryText = string.Empty;

            RefreshSpecialActions();

            // Capture is never left running: it swallows keystrokes from the rest of the app while
            // it is on (docs/app/keystroke-capture.md).
            _capture.Stop();
        }

        private bool CanChangeEntry()
        {
            return IsEditing && !IsBusy;
        }

        private void SetEditMode(PedalInputMode mode)
        {
            if (_session is null)
            {
                return;
            }

            _session.SetMode(mode);

            EntryMessage = string.Empty;

            RefreshEntry();
        }

        private void Backspace()
        {
            if (_session is null)
            {
                return;
            }

            _session.Backspace();

            EntryMessage = string.Empty;

            RefreshEntry();
        }

        private void ClearEntry()
        {
            if (_session is null)
            {
                return;
            }

            _session.Clear();

            EntryMessage = string.Empty;

            RefreshEntry();
        }

        private bool CanApplySpecialAction(PedalSpecialActionViewModel? action)
        {
            return action is not null && CanChangeEntry() && _session?.CanApply(action.Action) == true;
        }

        /// <summary>
        /// 12 §6: applies a menu item. A refusal changes nothing — it only says why, next to the
        /// entry box, because a modal dialog for "add a keystroke first" would be noise.
        /// </summary>
        private void ApplySpecialAction(PedalSpecialActionViewModel? action)
        {
            if (action is null || _session is null)
            {
                return;
            }

            var result = _session.Apply(action.Action);

            if (!result.Applied)
            {
                EntryMessage = DescribeFailure(result.Failure);

                return;
            }

            EntryMessage = string.Empty;

            RefreshEntry();
        }

        private void OnKeystrokeCaptured(CapturedKeystroke keystroke)
        {
            var session = _session;

            if (session is null || keystroke is null)
            {
                return;
            }

            session.AddKeystroke(keystroke.Key, ToModifiers(keystroke.HeldModifiers));

            EntryMessage = string.Empty;

            RefreshEntry();
        }

        /// <summary>Re-reads the session into the properties the entry box binds to.</summary>
        private void RefreshEntry()
        {
            var session = _session;

            if (session is null)
            {
                return;
            }

            // The mode is read back rather than assumed: a Special Action may have switched it
            // (12 §6), which also cleared the entry.
            EditMode = session.Mode;
            HoldWinModifier = session.HoldWinModifier;
            EntryItems = PedalEntryText.Format(session.DisplayItems);
            EntryText = string.Concat(EntryItems);

            RefreshSpecialActions();
        }

        private void RefreshSpecialActions()
        {
            foreach (var group in SpecialActionGroups)
            {
                group.Refresh(_session);
            }

            ApplySpecialActionCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Whether this editor could ever write its file — a property of the device and the load,
        /// not of the moment. Demo mode never writes (03 §3.5) and a file that could not be read is
        /// not rewritten from a model nobody could check; a missing file is saveable, because the
        /// save creates it. Kept apart from <see cref="CanSave"/> so that "busy right now" is never
        /// mistaken for "cannot be saved at all", which is a different question to the user.
        /// </summary>
        private bool CanEverSave()
        {
            return !IsDemoMode
                   && Device.Location is not null
                   && LoadState is PedalLoadState.Loaded or PedalLoadState.FileMissing;
        }

        /// <summary>Whether Save may run right now: something to write, and nothing in the way.</summary>
        private bool CanSave()
        {
            return CanEverSave() && HasUnsavedChanges && !IsBusy && !IsLoading;
        }

        /// <summary>
        /// Writes the file back (12 §4.6) between the loading indicator's show and hide, then
        /// reports the outcome. There is no eject (§5 step 7, invariant 8): the firmware reloads
        /// when the user flips the SE2 back to play mode, which is what the toast asks for.
        /// </summary>
        private async Task<bool> SaveAsync()
        {
            if (!CanSave())
            {
                return false;
            }

            // An entry still in the box was never committed to an input, so it is not part of what
            // is being written; dropping it keeps the file and the screen in agreement.
            CancelEdit();

            var location = Device.Location!;
            var configuration = _configuration;
            Exception? error = null;

            IsBusy = true;

            try
            {
                // Shown inside the try, and hidden after IsBusy is cleared below: both calls fan
                // out to the overlay, and a failure in either must not leave the flag stuck. The
                // indicator is blocking, so a ShowLoading that threw would leave the app dimmed and
                // untouchable; IsBusy on its own would only disable Save, every entry command and —
                // through ConfirmCloseAsync's refusal — Home.
                _notifications.ShowLoading(SavingCaption);

                await Task.Run(() => _pedalFiles.Save(location, configuration)).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                IsBusy = false;

                _notifications.HideLoading();
            }

            if (error is not null)
            {
                await ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = SaveTitle,
                    Message = SaveErrorMessagePrefix + error.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);

                return false;
            }

            foreach (var row in Inputs)
            {
                row.IsModified = false;
            }

            HasUnsavedChanges = false;

            _notifications.ShowToast(new ToastRequest
            {
                Title = SavedToastTitle,
                Message = SavedToastMessage
            });

            return true;
        }

        /// <summary>
        /// Shows a box and returns its outcome, or null when it could not be put on screen (the
        /// window is already gone). Every caller treats null as "the user did not answer".
        /// <para>
        /// Capture is suspended for as long as the box is up: it swallows every key of the focused
        /// window while it runs, the dialog's own Enter and Escape included
        /// (docs/app/keystroke-capture.md). Suspending a stopped capture is harmless, so this holds
        /// for the boxes raised outside an edit too.
        /// </para>
        /// </summary>
        private async Task<MessageBoxOutcome?> ShowMessageBoxAsync(MessageBoxRequest request)
        {
            _capture.Suspend();

            try
            {
                return await _notifications.ShowMessageBoxAsync(request).ConfigureAwait(true);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                _capture.Resume();
            }
        }

        private void NotifyCommands()
        {
            BeginEditCommand.NotifyCanExecuteChanged();
            SetSingleModeCommand.NotifyCanExecuteChanged();
            SetMacroModeCommand.NotifyCanExecuteChanged();
            BackspaceCommand.NotifyCanExecuteChanged();
            ClearEntryCommand.NotifyCanExecuteChanged();
            CancelEditCommand.NotifyCanExecuteChanged();
            DoneCommand.NotifyCanExecuteChanged();
            ApplySpecialActionCommand.NotifyCanExecuteChanged();
            SaveCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Stops capture and detaches from it. The capture service is app-wide and outlives the
        /// editor, so leaving it started would swallow every keystroke of the dashboard behind us.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _capture.KeystrokeCaptured -= _keystrokeCapturedHandler;

            CancelEdit();
        }

        /// <summary>What one load attempt produced: where the rows came from, and what to say about it.</summary>
        private sealed record LoadOutcome
        {
            public required PedalLoadState State { get; init; }

            public PedalConfiguration? Configuration { get; init; }

            public IReadOnlyList<PedalInvalidLine> InvalidLines { get; init; } = [];

            public string FilePath { get; init; } = string.Empty;

            public string FailureMessage { get; init; } = string.Empty;
        }
    }
}
