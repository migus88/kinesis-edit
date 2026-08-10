using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The key inspector's <b>Remap</b> panel (mockups <c>1e</c>/<c>2a</c>): what the position sends
    /// now, the two ways to change it, and what the app noticed about the change afterwards. It
    /// replaces the Search Keys overlay of specs/11-feature-dialogs.md §11.6 in the rail — the same
    /// catalog, no longer behind a scrim.
    ///
    /// <para><b>Two ways in, and the physical key is still the fast one.</b> <c>● Record</c> arms
    /// capture and the next keypress becomes the assignment, which is the only way this app has ever
    /// assigned from a real key and stays so. The picker beside it is for the actions no keyboard can
    /// press — a media key, a mouse button, a profile selector — and for the user who would rather
    /// type a name than find a key.</para>
    ///
    /// <para><b>It records through the editor, never around it.</b> The editor owns the single
    /// subscription to <c>IKeystrokeCaptureService</c> and routes one keystroke to one consumer
    /// (docs/app/keyboard-editor.md, "Keystroke routing"); this panel says it wants one through
    /// <see cref="IsRecording"/>/<see cref="IKeystrokeSink.WantsKeystrokes"/> and takes it through
    /// <see cref="ReceiveKeystroke"/>. Saying so honestly is load bearing: the editor folds
    /// <see cref="IsRecording"/> into <c>IsCaptureActive</c>, which is what stops ⌘S firing on the
    /// keypress the user meant as an assignment.</para>
    ///
    /// <para><b>The armed field is a <c>Border</c>, not a <c>TextBox</c>.</b> Focus inside a text box
    /// suspends capture, so a token field the user could click into would swallow the very keypress
    /// it exists to record — hence the <c>TokenField</c> control theme and its <c>.armed</c> state.
    /// The <em>search</em> field is a real <c>TextBox</c> and is supposed to suspend capture: the
    /// user is typing a name there, not pressing a key.</para>
    ///
    /// <para><b>It writes, and then asks to be refreshed.</b> Core raises no change notification, so
    /// every write ends in <see cref="Assigned"/> and the editor answers by running its one refresh
    /// funnel. <see cref="Refresh"/> itself never writes — it is called <em>after</em> somebody
    /// else's mutation, and a panel that wrote from it would write forever.</para>
    ///
    /// <para><b>The advisory reports, it does not refuse.</b> Assigning a token that is already on
    /// the layer is legal (docs/app/keyboard-model.md — limits are reported, never enforced), so the
    /// assignment lands and the panel then says what the editor noticed. The finding is <em>read</em>
    /// off <see cref="EditorAdvisories"/>, which is built from <c>DuplicateKeyScan</c>; nothing here
    /// rescans and nothing here re-words, because two derivations of one fact are two things to
    /// disagree.</para>
    /// </summary>
    public sealed class RemapPanelViewModel : KeyInspectorPanelViewModel, IKeystrokeSink
    {
        /// <summary>The section label over the current-assignment field.</summary>
        public const string CurrentTokenLabel = "THIS KEY SENDS";

        /// <summary>The record button at rest.</summary>
        public const string RecordCaption = "Record";

        /// <summary>The record button while the next keypress is being waited for.</summary>
        public const string RecordingCaption = "Listening…";

        /// <summary>What the panel says under the armed field, so the user knows what to do next.</summary>
        public const string RecordingHint = "Press a key to assign it to this position.";

        /// <summary>Why the panel refuses on a position that can never be remapped (05 §5.3).</summary>
        public const string LockedReason = "This position is locked and cannot be remapped.";

        /// <summary>The panel's own name, and its mode tab's caption.</summary>
        public const string PanelTitle = KeyInspectorTabViewModel.RemapCaption;

        /// <inheritdoc />
        public override KeyInspectorMode Mode => KeyInspectorMode.Remap;

        /// <inheritdoc />
        public override string Title => PanelTitle;

        /// <inheritdoc />
        public override bool IsAvailable => _key is null || _key.CanEdit;

        /// <inheritdoc />
        public override string UnavailableReason => IsAvailable ? string.Empty : LockedReason;

        /// <inheritdoc />
        public override bool IsRecording => _isRecording;

        /// <summary>The catalog, the chips and the result rows — shared with every other call site.</summary>
        public TokenPickerViewModel Picker { get; }

        /// <summary>
        /// What the position sends right now, as the file spells it: <c>esc</c>. Mono, because it
        /// is literally a value in <c>layoutN.txt</c>; empty when nothing is selected.
        /// </summary>
        public string CurrentToken
        {
            get => _currentToken;
            private set => SetProperty(ref _currentToken, value);
        }

        /// <summary>The record button's caption, which moves with <see cref="IsRecording"/>.</summary>
        public string RecordCommandCaption => _isRecording ? RecordingCaption : RecordCaption;

        /// <summary>
        /// What the editor noticed about this position after the panel assigned to it — today always
        /// the duplicate-key finding of <c>DuplicateKeyScan</c>, which is the only advisory anchored
        /// to a key position at all. Amber, non-blocking, and shown only after an assignment made
        /// here: the rail's footer carries the position's standing note, and a second permanent copy
        /// of it in the panel would be a third place one advisory is reported.
        /// </summary>
        public string DuplicateAdvisory
        {
            get => _duplicateAdvisory;
            private set
            {
                if (SetProperty(ref _duplicateAdvisory, value))
                {
                    OnPropertyChanged(nameof(HasDuplicateAdvisory));
                }
            }
        }

        /// <summary>Whether the last assignment left something worth saying.</summary>
        public bool HasDuplicateAdvisory => _duplicateAdvisory.Length > 0;

        /// <summary>Arms capture for the next physical keystroke, or stands it down again.</summary>
        public IRelayCommand RecordCommand { get; }

        /// <summary>
        /// Assigns the picker's highlighted row — <c>↵</c>, and the pointer's double click. It is a
        /// command of its own rather than a listener on the picker so the panel can refuse: a locked
        /// position, or nothing selected, and the picker never learns why.
        /// </summary>
        public IRelayCommand AssignCommand { get; }

        /// <inheritdoc />
        bool IKeystrokeSink.WantsKeystrokes => _isRecording && _key is not null;

        /// <summary>Raised after the panel wrote a remap, so the editor can run its refresh funnel.</summary>
        public event EventHandler? Assigned;

        private readonly Action<KeyDefinition> _chosenHandler;
        private KeyboardKeyViewModel? _key;
        private KeyboardLayerViewModel? _layer;
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private string _currentToken = string.Empty;
        private string _duplicateAdvisory = string.Empty;
        private bool _isRecording;

        /// <summary>Whether the last write into the model came from this panel.</summary>
        private bool _hasAssigned;

        /// <summary>
        /// Builds the panel over one dialect's catalog. <paramref name="recent"/> is the editor's own
        /// session history, so the <c>Recent</c> chip here and the one in the macro-insertion picker
        /// remember the same assignments.
        /// </summary>
        public RemapPanelViewModel(TokenDialect dialect, RecentTokenStore? recent = null)
        {
            Picker = new TokenPickerViewModel(dialect, recent);

            RecordCommand = new RelayCommand(ToggleRecording, CanRecord);
            AssignCommand = new RelayCommand(AssignSelected, CanAssign);

            // The picker outlives nothing this panel does not own, so there is nothing to detach.
            _chosenHandler = _ => AssignSelected();
            Picker.Chosen += _chosenHandler;
            Picker.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(TokenPickerViewModel.HasSelection))
                {
                    AssignCommand.NotifyCanExecuteChanged();
                }
            };
        }

        /// <summary>Puts the caret in the search field — ⌘F, wherever the user pressed it.</summary>
        public void FocusSearch()
        {
            Picker.RequestFocus();
        }

        /// <inheritdoc />
        public override void Refresh(
            KeyboardKeyViewModel? key,
            KeyboardLayerViewModel? layer,
            KeyboardLayout? layout,
            EditorAdvisories advisories)
        {
            var isNewKey = !ReferenceEquals(key, _key);

            _key = key;
            _layer = layer;
            _advisories = advisories ?? EditorAdvisories.Empty;

            if (isNewKey)
            {
                // A note about the position the user just left has nothing to say about this one,
                // and an assignment made here is what earns the note in the first place.
                _hasAssigned = false;

                StopRecording();
                Picker.Clear();
            }

            CurrentToken = key?.CurrentAssignmentText ?? string.Empty;

            RefreshAdvisory();

            OnPropertyChanged(nameof(IsAvailable));
            OnPropertyChanged(nameof(UnavailableReason));

            RecordCommand.NotifyCanExecuteChanged();
            AssignCommand.NotifyCanExecuteChanged();
        }

        /// <inheritdoc />
        public override void Deactivate()
        {
            StopRecording();
        }

        /// <summary>
        /// Takes the captured keypress and makes it the position's assignment. <c>Remap</c> rather
        /// than <c>ApplyRemap</c>, deliberately and exactly as the editor's own capture path does:
        /// pressing a position's own factory action un-does the remap (04 §2.1), which is how the
        /// legacy apps behaved and what a user pressing <c>d</c> on the <c>d</c> position expects.
        /// </summary>
        public void ReceiveKeystroke(CapturedKeystroke keystroke)
        {
            ArgumentNullException.ThrowIfNull(keystroke);

            if (!_isRecording)
            {
                return;
            }

            StopRecording();

            Assign(keystroke.Key);
        }

        private bool CanRecord()
        {
            return _key is not null && _key.CanEdit;
        }

        private bool CanAssign()
        {
            return CanRecord() && Picker.HasSelection;
        }

        private void ToggleRecording()
        {
            if (_isRecording)
            {
                StopRecording();

                return;
            }

            if (!CanRecord())
            {
                return;
            }

            SetRecording(true);
        }

        private void StopRecording()
        {
            SetRecording(false);
        }

        private void SetRecording(bool isRecording)
        {
            if (_isRecording == isRecording)
            {
                return;
            }

            _isRecording = isRecording;

            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(RecordCommandCaption));

            OnRecordingChanged();
        }

        private void AssignSelected()
        {
            if (!CanAssign() || Picker.SelectedKey is not { } definition)
            {
                return;
            }

            Assign(definition);
        }

        /// <summary>
        /// The one write path. It ends in <see cref="Assigned"/> rather than in a refresh of its own:
        /// the editor's funnel rebuilds the advisories, the counters and the legend, and calling only
        /// half of that here would leave the rest stale.
        /// </summary>
        private void Assign(KeyDefinition definition)
        {
            if (_key is not { } key || !key.CanEdit)
            {
                return;
            }

            key.Key.Remap(definition);
            key.RefreshFromModel();

            Picker.Remember(definition);

            _hasAssigned = true;

            CurrentToken = key.CurrentAssignmentText;

            Assigned?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reads the position's advisory off the set the editor handed over. Never a rescan:
        /// <c>DuplicateKeyScan</c> walks every key of every layer, and the editor has already run it.
        /// </summary>
        private void RefreshAdvisory()
        {
            if (!_hasAssigned || _key is null || _layer is null)
            {
                DuplicateAdvisory = string.Empty;

                return;
            }

            var matches = _advisories.ForKey(_layer.Index, _key.Index);

            DuplicateAdvisory = matches.Count > 0 ? matches[0].Message : string.Empty;
        }
    }
}
