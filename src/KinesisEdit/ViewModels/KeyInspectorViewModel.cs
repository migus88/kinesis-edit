using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The key inspector rail — the 268 px third column of the Layout tab (mockups <c>1e</c> dark /
    /// <c>2a</c> light, docs/design/handoff.md § "Layout tab"). It names the position, says what the
    /// board shipped it doing and what it does now, offers the one slot's modes as tabs, hosts
    /// whichever mode panel is showing, and carries the footer the handoff puts under all of them.
    ///
    /// <para><b>It does not know the editor.</b> Like <see cref="BoardLegendViewModel"/> before it
    /// (issue #91), the rail is constructed <b>once</b> by <c>KeyboardEditorViewModel</c>, is handed
    /// that class's own commands, and is <b>pushed</b> new state by
    /// <see cref="Refresh(KeyboardKeyViewModel?, KeyboardLayerViewModel?, KeyboardLayout?, EditorAdvisories)"/>
    /// from the same funnel that already ends in <c>RefreshLegend()</c>. Holding a reference to the
    /// editor would make the largest class in the app circular with its own rail, which
    /// docs/guides/Coding Conventions.md forbids.</para>
    ///
    /// <para><b>It is not modal, and this is load-bearing.</b> The rail never sets and never reads
    /// <c>HasActiveOverlay</c>: the board stays clickable while it is open, which is the whole
    /// point of a rail rather than the feature panels it replaces. Selecting another cap simply
    /// refreshes it.</para>
    ///
    /// <para><b>The modes are one slot.</b> Core says so — <c>KeyboardKey.SetTapAndHold</c> clears
    /// the position's remap and multi-modifier, and <c>Remap</c> clears the tap-and-hold. So
    /// switching mode while the position already carries a rule raises
    /// <see cref="ModeSwitchWarning"/>, stated <em>at the point of switching rather than after</em>
    /// (mockup <c>2h</c>). The warning never blocks — nothing here refuses an edit.</para>
    ///
    /// <para><b>Three things the rail deliberately does not own.</b> <c>Revert key</c> is the
    /// editor's existing <c>ResetKeyCommand</c> and <c>Copy to…</c> its existing
    /// <c>CopyKeyCommand</c>, both passed in — a second reset path and a second copy path would be
    /// two more sets of refusals to keep in step. And the advisory note is <em>read</em> off the set
    /// handed to <see cref="Refresh"/>; nothing here rescans, because <c>DuplicateKeyScan</c> walks
    /// every key of every layer and two derivations of one finding are two things to disagree.</para>
    ///
    /// <para><b>It is always on screen (issue #119).</b> The rail is a permanent column of the
    /// Layout tab: it has no close button, no selection state collapses it, and with nothing
    /// selected it draws <see cref="NoSelectionMessage"/> in place of the header, the modes and the
    /// footer. The board therefore never jumps sideways to re-centre, which is what a rail that
    /// appeared and vanished under the pointer used to do.</para>
    ///
    /// <para><b>The Macro tab is a panel like the others (issue #93).</b> It hosts
    /// <see cref="MacroInspectorPanelViewModel"/> — mockup <c>2i</c>'s "selecting a key edits its
    /// macro right here" — and the rail is <b>never narrower than 300 px</b> while it is showing
    /// (<see cref="IsWide"/>). It used to bridge out to the Macros tab; navigating away from the
    /// board when a mode tab is pressed was the placeholder, not the design.</para>
    ///
    /// <para><b><c>Multi-mod</c> is not drawn on this board.</b> The tab is built only where
    /// <c>KeyboardKey.SupportsMultiModifiers</c> is true; elsewhere it is absent, not disabled
    /// (docs/design/handoff.md:137). Mockup <c>1e</c> draws it beside an "Advantage 360 only" note
    /// — the handoff and issue #92 override that, and the deviation is recorded.</para>
    /// </summary>
    public sealed class KeyInspectorViewModel : ViewModelBase
    {
        /// <summary>
        /// The rail's exclusivity sentence, verbatim from mockup <c>2a</c> — which tightens
        /// <c>1e</c>'s shorter "This key does one thing" and supersedes it.
        /// </summary>
        public const string ExclusivitySentence = "This key does one thing — picking another replaces it.";

        /// <summary>
        /// What the rail says while nothing is selected (issue #119). The rail is a permanent column
        /// of the Layout tab now, so it needs a state for "there is no position to be about"; one
        /// short muted line, centred, in place of the header, the modes and the footer.
        /// <para>
        /// Its own sentence rather than a borrowed one: <c>TapAndHoldPanelViewModel</c> and
        /// <c>MacroInspectorPanelViewModel</c> each carry a <c>NoSelectionMessage</c>, but those
        /// answer "this panel has nothing to edit" from inside a rail that is already explaining
        /// itself — and neither is reachable any more, because the empty state replaces the panels
        /// wholesale.
        /// </para>
        /// </summary>
        public const string NoSelectionMessage = "Select a key on the board to edit it.";

        /// <summary>The word after the token in the header: "Left half · [d] position".</summary>
        public const string PositionSuffix = "position";

        /// <summary>The middle dot the header and the assignment line are built round.</summary>
        public const string Separator = "·";

        /// <summary>The assignment line's first label: "factory [d]".</summary>
        public const string FactoryLabel = "factory";

        /// <summary>Its second: "now [esc]".</summary>
        public const string CurrentLabel = "now";

        /// <summary>
        /// The footer's revert action. Sentence case, as the mockups draw the inspector's actions —
        /// and a rename of spec 10's <c>Reset Key</c>, whose command it runs unchanged.
        /// </summary>
        public const string RevertKeyCaption = "Revert key";

        /// <summary>The footer's copy action — #91's armed pick, reached from the rail.</summary>
        public const string CopyToCaption = "Copy to…";

        /// <summary>What the rail says while that pick is armed and waiting for its target click.</summary>
        public const string CancelCopyCaption = "Cancel copy";

        /// <summary>
        /// The mode-switch warning, one noun short of a sentence. Mockup <c>2h</c> states it as
        /// "Assigning this clears the remap that was on this key"; the noun moves with whatever the
        /// position is actually carrying, so the warning is never about a rule that is not there.
        /// </summary>
        public const string ModeSwitchWarningFormat = "Assigning this clears the {0} that was on this key.";

        /// <summary>What a remapped position is carrying, in the warning's words.</summary>
        public const string RemapNoun = "remap";

        /// <summary>What a tap-and-hold position is carrying.</summary>
        public const string TapAndHoldNoun = "tap and hold";

        /// <summary>What a macro position is carrying.</summary>
        public const string MacroNoun = "macro";

        /// <summary>What a multi-modifier position is carrying.</summary>
        public const string MultiModifierNoun = "multi-modifier";

        /// <summary>
        /// The warning shown when the user picks a mode other than the one
        /// <paramref name="assigned"/> the position already carries. See
        /// <see cref="ModeSwitchWarningFormat"/>.
        /// </summary>
        public static string BuildModeSwitchWarning(KeyInspectorMode assigned)
        {
            var noun = assigned switch
            {
                KeyInspectorMode.Remap => RemapNoun,
                KeyInspectorMode.TapAndHold => TapAndHoldNoun,
                KeyInspectorMode.Macro => MacroNoun,
                KeyInspectorMode.MultiModifier => MultiModifierNoun,
                _ => string.Empty
            };

            return string.Format(CultureInfo.InvariantCulture, ModeSwitchWarningFormat, noun);
        }

        /// <summary>
        /// Whether the rail is showing a position — a key is selected and the rail has not been
        /// stood down.
        /// <para>
        /// <b>It no longer decides whether the rail is on screen (issue #119).</b> The rail is a
        /// permanent column of the Layout tab: nothing binds visibility to this, and with no
        /// selection the frame stays exactly where it is and draws
        /// <see cref="NoSelectionMessage"/> instead. What is left is the question the editor
        /// actually asks — "is there a position in the rail to act on" — which is what gates
        /// <c>InsertSpecialActionCommand</c>.
        /// </para>
        /// </summary>
        public bool IsOpen
        {
            get => _isOpen;
            private set => SetProperty(ref _isOpen, value);
        }

        /// <summary>
        /// Whether a key is selected at all. The rail's own switch since issue #119: everything the
        /// inspector draws — the header, the modes, the footer — is drawn only when this is true,
        /// and the empty state takes the whole frame when it is not.
        /// <para>
        /// Deliberately <em>not</em> <see cref="IsOpen"/>. That one also answers "…and the rail has
        /// not been stood down", which <see cref="Close"/> still does on the way out of an editor;
        /// a view driven by it would blank the rail while the editor is being disposed.
        /// </para>
        /// </summary>
        public bool HasSelection
        {
            get => _hasSelection;
            private set => SetProperty(ref _hasSelection, value);
        }

        /// <summary>
        /// Which drawn panel of the board the position sits in — "Left half", "Right half", or an
        /// empty string on a board drawn in one piece
        /// (<see cref="KeyboardKeyViewModel.DescribeSection"/>).
        /// </summary>
        public string PositionDescription
        {
            get => _positionDescription;
            private set
            {
                if (SetProperty(ref _positionDescription, value))
                {
                    OnPropertyChanged(nameof(HasPositionDescription));
                }
            }
        }

        /// <summary>Whether the header names a half at all.</summary>
        public bool HasPositionDescription => _positionDescription.Length > 0;

        /// <summary>
        /// The token the header names the position by — <c>[d]</c>. It is the <b>factory</b>
        /// assignment, because that is what identifies the position on the board however often it
        /// is remapped, and it is mono because it is literally a value in the layout file.
        /// </summary>
        public string PositionToken => _factoryAssignmentText;

        /// <summary>
        /// What the cap itself reads right now (<see cref="KeyboardKeyViewModel.Caption"/>) — the
        /// mockups' 32 px accent tile at the head of the rail, which is how the user checks at a
        /// glance that the inspector is talking about the key they clicked.
        /// </summary>
        public string CapCaption
        {
            get => _capCaption;
            private set => SetProperty(ref _capCaption, value);
        }

        /// <summary>The assignment line's factory half: <c>[d]</c>.</summary>
        public string FactoryAssignmentText
        {
            get => _factoryAssignmentText;
            private set
            {
                if (SetProperty(ref _factoryAssignmentText, value))
                {
                    OnPropertyChanged(nameof(PositionToken));
                }
            }
        }

        /// <summary>Its "now" half: <c>[esc]</c>, or the same token again on an untouched key.</summary>
        public string CurrentAssignmentText
        {
            get => _currentAssignmentText;
            private set => SetProperty(ref _currentAssignmentText, value);
        }

        /// <summary>
        /// Whether the selected position can never be remapped (05 §5.3). The rail then draws
        /// <see cref="LockedPanel"/> in place of its modes and its footer.
        /// </summary>
        public bool IsLocked
        {
            get => _isLocked;
            private set => SetProperty(ref _isLocked, value);
        }

        /// <summary>
        /// The locked-position panel. Built once and refreshed like everything else, rather than
        /// created on demand — a rail that allocated a panel while the user clicked around the
        /// board would be doing it on the selection path.
        /// </summary>
        public LockedKeyPanelViewModel LockedPanel { get; }

        /// <summary>
        /// The mode tabs, in the mockups' order and laid out 2×2 by the view. Rebuilt only when the
        /// <em>set</em> changes — which today means when a position that supports multi-modifiers
        /// is selected on a board that has them.
        /// </summary>
        public IReadOnlyList<KeyInspectorTabViewModel> Tabs
        {
            get => _tabs;
            private set => SetProperty(ref _tabs, value);
        }

        /// <summary>Which mode the rail is showing.</summary>
        public KeyInspectorMode SelectedMode
        {
            get => _selectedMode;
            private set => SetProperty(ref _selectedMode, value);
        }

        /// <summary>
        /// The panel for <see cref="SelectedMode"/>, or null when no panel was supplied for it —
        /// which is the normal state of the <see cref="KeyInspectorMode.Macro"/> tab, and of every
        /// tab before the panels land.
        /// </summary>
        public KeyInspectorPanelViewModel? ActivePanel
        {
            get => _activePanel;
            private set => SetProperty(ref _activePanel, value);
        }

        /// <summary>
        /// What switching to <see cref="SelectedMode"/> is about to replace, or an empty string when
        /// it would replace nothing. Non-blocking: it says what will happen, it does not ask.
        /// </summary>
        public string ModeSwitchWarning
        {
            get => _modeSwitchWarning;
            private set
            {
                if (SetProperty(ref _modeSwitchWarning, value))
                {
                    OnPropertyChanged(nameof(HasModeSwitchWarning));
                }
            }
        }

        /// <summary>Whether there is a warning to draw.</summary>
        public bool HasModeSwitchWarning => _modeSwitchWarning.Length > 0;

        /// <summary>
        /// The footer's advisory note — the first thing the editor has already noticed about this
        /// position, or an empty string. Read, never recomputed; see the type's remarks.
        /// </summary>
        public string AdvisoryNote
        {
            get => _advisoryNote;
            private set
            {
                if (SetProperty(ref _advisoryNote, value))
                {
                    OnPropertyChanged(nameof(HasAdvisory));
                }
            }
        }

        /// <summary>Whether this position carries one.</summary>
        public bool HasAdvisory => _advisoryNote.Length > 0;

        /// <summary>
        /// Whether a copy is armed and waiting for its target click. Read off the editor's own
        /// cancel command — a command that can execute <em>is</em> the armed state — so the rail
        /// mirrors nothing and cannot drift from it.
        /// </summary>
        public bool IsCopyArmed => CancelCopyKeyCommand.CanExecute(null);

        /// <summary>
        /// Whether the showing panel is waiting for a physical keystroke. The editor folds this into
        /// <c>IsCaptureActive</c>, which is what keeps ⌘S from firing while a hold action is being
        /// recorded.
        /// </summary>
        public bool IsRecording => _activePanel?.IsRecording == true;

        /// <summary>
        /// Whether the showing panel wants the wide rail (docs/design/handoff.md § Geometry). Today
        /// that is the Macro panel and nothing else (mockup <c>2i</c>) — the width follows the mode
        /// rather than being decided anywhere outside the panel that needs it.
        /// <para>
        /// <b>Since issue #119 it raises a floor rather than setting a width.</b> The rail is
        /// drag-adjustable, so the editor computes
        /// <c>KeyboardEditorViewModel.EffectiveInspectorRailWidth</c> as the larger of the user's
        /// own width and the handoff's 300: the macro variant is never violated, and a user who
        /// dragged the rail wider is never yanked back to 300.
        /// </para>
        /// </summary>
        public bool IsWide => _activePanel?.WantsWideRail == true;

        /// <summary>Raised whenever <see cref="IsRecording"/> moves, in either direction.</summary>
        public event EventHandler? RecordingChanged;

        /// <summary>Chooses a mode. A dead tab (a locked position) is refused silently.</summary>
        public IRelayCommand<KeyInspectorTabViewModel> SelectModeCommand { get; }

        /// <summary>
        /// The footer's <c>Revert key</c>: the editor's own <c>ResetKeyCommand</c>, which calls
        /// <c>ClearRemap()</c> and deliberately not <c>Remap(OriginalKey)</c> — the latter would
        /// also destroy the position's tap-and-hold and multi-modifier.
        /// </summary>
        public IRelayCommand ResetKeyCommand { get; }

        /// <summary>The footer's <c>Copy to…</c>: the editor's own <c>CopyKeyCommand</c>.</summary>
        public IRelayCommand CopyKeyCommand { get; }

        /// <summary>Disarms that pick: the editor's own <c>CancelCopyKeyCommand</c>.</summary>
        public IRelayCommand CancelCopyKeyCommand { get; }

        /// <summary>
        /// Stands the rail down. <b>It is no longer reachable from the screen (issue #119)</b>: the
        /// header's ghost close button is gone and Escape's last stage clears the <em>selection</em>
        /// instead, which drops the rail to its empty state without collapsing anything. What is
        /// left is the programmatic stand-down the editor performs on the way out — see
        /// <see cref="Close"/>.
        /// </summary>
        public IRelayCommand CloseCommand { get; }

        private readonly Dictionary<KeyInspectorMode, KeyInspectorPanelViewModel> _panels = [];
        private readonly EventHandler _panelRecordingChangedHandler;
        private readonly EventHandler _copyArmedChangedHandler;
        private IReadOnlyList<KeyInspectorTabViewModel> _tabs = [];
        private KeyInspectorPanelViewModel? _activePanel;
        private KeyboardKeyViewModel? _key;
        private KeyboardLayerViewModel? _layer;
        private KeyboardLayout? _layout;
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private KeyInspectorMode _selectedMode = KeyInspectorMode.Remap;
        private string _positionDescription = string.Empty;
        private string _capCaption = string.Empty;
        private string _factoryAssignmentText = string.Empty;
        private string _currentAssignmentText = string.Empty;
        private string _modeSwitchWarning = string.Empty;
        private string _advisoryNote = string.Empty;
        private bool _isOpen;
        private bool _hasSelection;
        private bool _isLocked;

        /// <summary>Whether the user dismissed the rail for the key it is currently holding.</summary>
        private bool _isDismissed;

        /// <summary>
        /// Builds the rail over the three editor commands its footer runs, and over whichever mode
        /// panels exist. <paramref name="panels"/> is optional and may be short: a mode with no
        /// panel still draws its tab (the <see cref="KeyInspectorMode.Macro"/> bridge always is
        /// one), which is what lets the rail land before the panels that fill it.
        /// </summary>
        public KeyInspectorViewModel(
            IRelayCommand resetKeyCommand,
            IRelayCommand copyKeyCommand,
            IRelayCommand cancelCopyKeyCommand,
            IEnumerable<KeyInspectorPanelViewModel>? panels = null)
        {
            ResetKeyCommand = resetKeyCommand ?? throw new ArgumentNullException(nameof(resetKeyCommand));
            CopyKeyCommand = copyKeyCommand ?? throw new ArgumentNullException(nameof(copyKeyCommand));
            CancelCopyKeyCommand = cancelCopyKeyCommand ?? throw new ArgumentNullException(nameof(cancelCopyKeyCommand));

            LockedPanel = new LockedKeyPanelViewModel(CopyKeyCommand);

            SelectModeCommand = new RelayCommand<KeyInspectorTabViewModel>(SelectMode);
            CloseCommand = new RelayCommand(Close);

            _panelRecordingChangedHandler = (_, _) => RaiseRecordingChanged();
            _copyArmedChangedHandler = (_, _) => OnPropertyChanged(nameof(IsCopyArmed));

            // The command belongs to the editor, which owns this rail, so the two live and die
            // together and there is nothing here to unsubscribe from later.
            CancelCopyKeyCommand.CanExecuteChanged += _copyArmedChangedHandler;

            if (panels is not null)
            {
                foreach (var panel in panels)
                {
                    _panels[panel.Mode] = panel;

                    panel.RecordingChanged += _panelRecordingChangedHandler;
                }
            }

            RebuildTabs(supportsMultiModifiers: false);

            // The rail opens on Remap, so the Remap panel is the showing one from the start.
            // SelectedMode and ActivePanel must never disagree — a rail that named a mode while
            // hosting nothing would be a state no refresh path produces and no view can draw.
            MoveTo(_selectedMode);
        }

        /// <summary>
        /// Takes the editor's current state. Called from the same funnel that ends in
        /// <c>RefreshLegend()</c> — Core announces no change, so a rail that waited to be told
        /// would wait forever.
        /// <para>
        /// A key it has not seen before <b>opens</b> the rail and puts it on whichever mode that
        /// position already carries. The same key again leaves the open/closed state alone, so a
        /// refresh caused by somebody else's edit cannot reopen a rail the user pressed Escape on;
        /// <see cref="Open"/> is how the selection path says "the user asked for this one again".
        /// </para>
        /// </summary>
        public void Refresh(
            KeyboardKeyViewModel? key,
            KeyboardLayerViewModel? layer,
            KeyboardLayout? layout,
            EditorAdvisories? advisories)
        {
            var isNewKey = !ReferenceEquals(key, _key);

            _key = key;
            _layer = layer;
            _layout = layout;
            _advisories = advisories ?? EditorAdvisories.Empty;

            if (isNewKey)
            {
                _isDismissed = false;
            }

            PositionDescription = key is null
                ? string.Empty
                : KeyboardKeyViewModel.DescribeSection(key.Section, layer?.Sections.Count ?? 0);

            CapCaption = key?.Caption ?? string.Empty;
            FactoryAssignmentText = key?.FactoryAssignmentText ?? string.Empty;
            CurrentAssignmentText = key?.CurrentAssignmentText ?? string.Empty;
            IsLocked = key is not null && !key.CanEdit;
            HasSelection = key is not null;
            IsOpen = key is not null && !_isDismissed;

            RebuildTabs(key?.Key.SupportsMultiModifiers == true);

            if (isNewKey)
            {
                MoveTo(AssignedModeOf(key) ?? KeyInspectorMode.Remap);
            }

            RefreshAdvisoryNote();
            RefreshModeSwitchWarning();

            LockedPanel.Refresh(layout);

            // Every panel, not only the showing one: a panel the user switches to must already be
            // right, and the alternative is a second refresh path down the mode switch.
            foreach (var panel in _panels.Values)
            {
                panel.Refresh(key, layer, layout, _advisories);
            }
        }

        /// <summary>
        /// Reopens a rail the user dismissed, for the key it is already holding. The editor's
        /// selection path calls it: clicking the selected cap again is a request for the inspector,
        /// not a refresh.
        /// </summary>
        public void Open()
        {
            _isDismissed = false;

            IsOpen = _key is not null;
        }

        /// <summary>
        /// Stands the showing panel down <b>without closing the rail</b>: whatever it had armed
        /// stops, and nothing is written. It is what the editor calls wherever it takes the keyboard
        /// back for itself — a layer switch, a section switch, a save, an import, a modal panel
        /// opening over the board — where <see cref="Close"/> would be wrong, because the user never
        /// dismissed anything and the rail must still be there when they come back.
        /// </summary>
        public void Deactivate()
        {
            _activePanel?.Deactivate();

            RaiseRecordingChanged();
        }

        /// <summary>
        /// Stands the rail down without touching the selection: whatever the showing panel had armed
        /// stops, and <see cref="IsOpen"/> goes false so nothing downstream still thinks there is a
        /// position to act on.
        /// <para>
        /// <b>It does not collapse anything (issue #119).</b> The rail is a permanent column; this
        /// is the editor's own teardown path (<c>DetachInspector</c>), which runs when the editor is
        /// disposed and the whole view goes with it. Escape reaches the same visual result the other
        /// way round — by clearing the selection, which drops the rail to
        /// <see cref="NoSelectionMessage"/>.
        /// </para>
        /// </summary>
        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _activePanel?.Deactivate();

            _isDismissed = true;

            IsOpen = false;

            RaiseRecordingChanged();
        }

        /// <summary>
        /// Puts the rail on <paramref name="tab"/>'s mode. A dead tab is refused silently: a locked
        /// position's tabs exist to explain themselves, not to be pressed.
        /// </summary>
        private void SelectMode(KeyInspectorTabViewModel? tab)
        {
            if (tab is null || !tab.IsEnabled || tab.Mode == _selectedMode)
            {
                return;
            }

            MoveTo(tab.Mode);

            RefreshModeSwitchWarning();
        }

        /// <summary>
        /// Moves the selection to <paramref name="mode"/> and stands the outgoing panel down. The
        /// model is untouched — switching mode is a question about what the user is <em>about</em>
        /// to do, and Core clears the other rules only when something is actually assigned.
        /// </summary>
        private void MoveTo(KeyInspectorMode mode)
        {
            if (_activePanel is not null && _activePanel.Mode != mode)
            {
                _activePanel.Deactivate();
            }

            SelectedMode = mode;

            foreach (var tab in _tabs)
            {
                tab.IsSelected = tab.Mode == mode;
            }

            ActivePanel = _panels.GetValueOrDefault(mode);

            // The rail's macro floor follows the panel (docs/design/handoff.md § Geometry), and a
            // mode switch is the only thing that moves it. The editor listens to this to recompute
            // EffectiveInspectorRailWidth.
            OnPropertyChanged(nameof(IsWide));

            RaiseRecordingChanged();
        }

        /// <summary>
        /// Builds the tab set. <see cref="KeyInspectorMode.MultiModifier"/> joins it only where the
        /// position has the firmware for it; elsewhere it is absent, never dim. Rebuilt only when
        /// the set really changes, so a click on the board does not throw the tabs away.
        /// </summary>
        private void RebuildTabs(bool supportsMultiModifiers)
        {
            var wanted = supportsMultiModifiers ? 4 : 3;

            if (_tabs.Count == wanted)
            {
                return;
            }

            var tabs = new List<KeyInspectorTabViewModel>(wanted)
            {
                KeyInspectorTabViewModel.Enabled(KeyInspectorMode.Remap),
                KeyInspectorTabViewModel.Enabled(KeyInspectorMode.TapAndHold),
                KeyInspectorTabViewModel.Enabled(KeyInspectorMode.Macro)
            };

            if (supportsMultiModifiers)
            {
                tabs.Add(KeyInspectorTabViewModel.Enabled(KeyInspectorMode.MultiModifier));
            }

            foreach (var tab in tabs)
            {
                tab.IsSelected = tab.Mode == _selectedMode;
            }

            Tabs = tabs;
        }

        /// <summary>
        /// Which of the one slot's modes the position is carrying right now, or null when it is
        /// carrying none. The order is Core's own precedence: a tap-and-hold and a multi-modifier
        /// each clear the remap, so a position that has both flags is really the later one.
        /// </summary>
        private static KeyInspectorMode? AssignedModeOf(KeyboardKeyViewModel? key)
        {
            if (key is null)
            {
                return null;
            }

            if (key.Key.IsTapAndHold)
            {
                return KeyInspectorMode.TapAndHold;
            }

            if (key.Key.HasMultiModifiers)
            {
                return KeyInspectorMode.MultiModifier;
            }

            if (key.IsMacro)
            {
                return KeyInspectorMode.Macro;
            }

            return key.IsModified ? KeyInspectorMode.Remap : null;
        }

        /// <summary>
        /// Re-reads what switching mode would replace. Empty whenever the position carries nothing,
        /// or carries exactly what the chosen mode writes.
        /// </summary>
        private void RefreshModeSwitchWarning()
        {
            var assigned = AssignedModeOf(_key);

            ModeSwitchWarning = assigned is null || assigned == _selectedMode
                ? string.Empty
                : BuildModeSwitchWarning(assigned.Value);
        }

        /// <summary>
        /// Takes the first advisory anchored to this position off the set the editor handed over.
        /// One, not all: the footer is a note, and the summary strip above the board is where the
        /// section's whole story is told (invariant 21 — an advisory appears exactly twice).
        /// </summary>
        private void RefreshAdvisoryNote()
        {
            if (_key is null || _layer is null)
            {
                AdvisoryNote = string.Empty;

                return;
            }

            var matches = _advisories.ForKey(_layer.Index, _key.Index);

            AdvisoryNote = matches.Count > 0 ? matches[0].Message : string.Empty;
        }

        private void RaiseRecordingChanged()
        {
            OnPropertyChanged(nameof(IsRecording));

            RecordingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
