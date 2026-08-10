using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the editor is <b>about</b>: the open section, the layer the picture shows, the key every
    /// key-scoped action applies to, and the three gestures that move them — a click on a cap
    /// (specs/10-apps-and-ui.md's click contract), a layer switch, a section switch — plus the two
    /// selections that are not gestures at all, an arrow across the physical board and
    /// <c>Review N</c>'s landing.
    /// <para>
    /// It is a type of its own rather than fifteen more members on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What earns it the
    /// file is that all of it answers <b>one</b> question — <i>what is the editor pointed at, and
    /// what does moving that pointer stand down?</i> — and that the answer has to be given in one
    /// place: the click contract's second branch, the arrow's deliberate refusal of it (invariant
    /// 24) and the review anchors' identical refusal are three readings of the same rule, and they
    /// drifted apart the moment they were three methods on a 2 000-line class.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes <see cref="Layers"/>,
    /// <see cref="SelectedLayer"/>, <see cref="SelectedKey"/>, <see cref="Tabs"/>,
    /// <see cref="SelectedTab"/> and all three commands under its own names — the board, the layer
    /// switcher and the section strip bind them, and <c>Views/KeyboardEditorView.axaml.cs</c> runs
    /// <see cref="MoveSelection"/> — so a view never sees this type. Everything it reads about the
    /// editor arrives through <see cref="IEditorSelectionHost"/> rather than a back-reference, and
    /// it <b>announces nothing</b>: a property may only be raised by the class that declares it, so
    /// the host is told what moved and raises the names itself.
    /// </para>
    /// <para>
    /// <b>It never sees <see cref="EditorKeystrokeRouter"/>, and that is the knot issue #154 is
    /// about.</b> The click contract's second branch <em>is</em> <c>BeginRemap</c>, which is the
    /// router's — so <see cref="IEditorSelectionHost"/> carries <c>BeginRemap</c>,
    /// <c>CancelRemap</c> and <c>IsListening</c> and the editor forwards them. Collaborators do not
    /// reach each other; the editor is the only thing that knows both of them exist.
    /// </para>
    /// <para>
    /// <b>Nothing here writes to the model</b>, which is why no path of it ends in the host's
    /// <c>RefreshCounters()</c> (invariant 16): a selection change moves what the chrome is about,
    /// never what it counts. The two cheap refreshes it does run — the advisory strip's projection
    /// and the legend row — are exactly the ones the editor already ran from these methods.
    /// </para>
    /// </summary>
    public sealed class EditorSelection
    {
        /// <summary>
        /// The editor's sections, filtered by what the device carries
        /// (<see cref="EditorTabViewModel.CreateAll"/>): Layout always, Settings where the board has
        /// a settings file, Lighting only where its led file is the model
        /// <see cref="LightingTabViewModel"/> edits. Every entry opens a working section — a feature
        /// the board lacks is not rendered at all rather than disabled.
        /// </summary>
        public IReadOnlyList<EditorTabViewModel> Tabs { get; }

        /// <summary>
        /// The open section. The setter runs <see cref="SelectTab"/> — the very guard the command
        /// runs — so a two-way binding cannot open a tab the strip does not carry.
        /// </summary>
        public EditorTab SelectedTab
        {
            get => _selectedTab;
            set => SelectTab(value);
        }

        /// <summary>
        /// The device's layers, in model order. Written by the editor's <c>Apply</c> as each profile
        /// arrives, which is the one path that rebuilds the picture.
        /// </summary>
        public IReadOnlyList<KeyboardLayerViewModel> Layers
        {
            get => _layers;
            set
            {
                // The same "one write per real change" the ObservableObject.SetProperty this
                // replaces enforced: an empty list is Array.Empty either way, so two loads that both
                // fail to build a picture must not announce a picture that moved.
                if (EqualityComparer<IReadOnlyList<KeyboardLayerViewModel>>.Default.Equals(_layers, value))
                {
                    return;
                }

                _layers = value;

                _host.OnLayersChanged();
            }
        }

        /// <summary>The layer the picture is showing.</summary>
        public KeyboardLayerViewModel? SelectedLayer
        {
            get => _selectedLayer;
            private set
            {
                if (EqualityComparer<KeyboardLayerViewModel?>.Default.Equals(_selectedLayer, value))
                {
                    return;
                }

                _selectedLayer = value;

                _host.OnSelectedLayerChanged();
            }
        }

        /// <summary>The key every key-scoped action applies to, or null when nothing is selected.</summary>
        public KeyboardKeyViewModel? SelectedKey
        {
            get => _selectedKey;
            private set
            {
                if (EqualityComparer<KeyboardKeyViewModel?>.Default.Equals(_selectedKey, value))
                {
                    return;
                }

                _selectedKey = value;

                _host.OnSelectedKeyChanged();
            }
        }

        /// <summary>Opens a section of the editor; a section this strip does not carry is refused.</summary>
        public IRelayCommand<EditorTabViewModel> SelectTabCommand { get; }

        /// <summary>Switches the picture to another layer, cancelling anything in progress.</summary>
        public IRelayCommand<KeyboardLayerViewModel> SelectLayerCommand { get; }

        /// <summary>
        /// What a click on a key cap runs: it selects the key, and a second click on the key that
        /// is already selected starts listening (specs/10-apps-and-ui.md, "Remap workflow").
        /// <para>
        /// <b>It is built with no predicate, and must stay that way</b> (invariant 35). A cap
        /// <c>Button</c> binds this command directly, so gating it disables all ~76 caps and
        /// <c>:disabled</c> outranks every state in the keycap theme — the board would go dead
        /// rather than inert. Every refusal a click can meet is made inside
        /// <see cref="SelectKey"/>.
        /// </para>
        /// </summary>
        public IRelayCommand<KeyboardKeyViewModel> SelectKeyCommand { get; }

        private readonly IEditorSelectionHost _host;

        /// <summary>
        /// The board picture, which belongs to the <b>device</b> and not to the profile: it is
        /// resolved once by the editor's constructor and this holds the very same instance, never a
        /// copy. <see cref="MoveSelection"/> is what needs it here — <c>KeyAdjacency</c> scores the
        /// physical grid — while the editor keeps reading it for <c>BoardWidth</c>/<c>BoardHeight</c>
        /// and for building the layers.
        /// </summary>
        private readonly KeyboardVisual? _visual;

        private IReadOnlyList<KeyboardLayerViewModel> _layers = [];
        private KeyboardLayerViewModel? _selectedLayer;
        private KeyboardKeyViewModel? _selectedKey;
        private EditorTab _selectedTab = EditorTab.Keys;

        /// <summary>
        /// Builds the selection over the editor it points at, the device whose sections the strip is
        /// filtered from, and the board picture an arrow is scored against.
        /// <para>
        /// <b>The strip is fixed here, at construction</b>, which is why
        /// <paramref name="isLightingSupported"/> is a parameter rather than something read later:
        /// the editor's constructor runs before any profile has been read, and demo mode never reads
        /// one, so "does this board have a lighting section" can only ever be a device-level
        /// question. It is also why <see cref="SelectTabCommand"/> is deliberately absent from the
        /// editor's <c>NotifyCommands()</c> — no state can change the answer to a predicate that
        /// reads nothing but whether it was handed a tab.
        /// </para>
        /// <para>
        /// Nothing here calls the host: the editor is still mid-constructor when this runs, and the
        /// first <see cref="SelectTab"/> is deliberately the last thing that constructor does.
        /// </para>
        /// </summary>
        public EditorSelection(
            IEditorSelectionHost host,
            DeviceDefinition device,
            bool isLightingSupported,
            KeyboardVisual? visual)
        {
            ArgumentNullException.ThrowIfNull(device);

            _host = host ?? throw new ArgumentNullException(nameof(host));
            _visual = visual;

            // The Lighting tab is rendered only for a board whose led file is the two-layer key
            // backlight model the panel edits — absent, never disabled, on every other board and on
            // one with no lighting hardware at all.
            Tabs = EditorTabViewModel.CreateAll(device, isLightingSupported);

            SelectTabCommand = new RelayCommand<EditorTabViewModel>(OnSelectTab, tab => tab is not null);
            SelectLayerCommand = new RelayCommand<KeyboardLayerViewModel>(SelectLayer);
            SelectKeyCommand = new RelayCommand<KeyboardKeyViewModel>(SelectKey);
        }

        /// <summary>
        /// Moves the key selection one step across the <b>physical</b> board — the grammar's
        /// "↑↓←→ move key selection across the physical grid, not tab order"
        /// (docs/design/mockups.md <c>2b</c>). Returns whether the selection actually moved, which
        /// is what tells the view there is a new cap to put the focus ring on.
        /// <para>
        /// The geometry is <see cref="KeyAdjacency"/>'s and lives here only because the board
        /// picture does: <c>_visual</c> is the editor's, never the view's. With nothing selected yet
        /// the first cap of the shown layer is where an arrow lands, so the grammar has an entry
        /// point that does not need a click first.
        /// </para>
        /// <para>
        /// It lands through <see cref="SelectKeyDirectly"/> and <b>never</b> through
        /// <see cref="SelectKeyCommand"/> (invariant 24): the latter promotes a second hit on the
        /// already-selected cap into listening, and arrowing onto a key must never start capture.
        /// </para>
        /// </summary>
        public bool MoveSelection(NavigationDirection direction)
        {
            if (direction is NavigationDirection.None || _visual is null || _host.IsLoading || _host.IsBusy)
            {
                return false;
            }

            if (SelectedLayer is not { } layer || layer.Keys.Count == 0)
            {
                return false;
            }

            if (SelectedKey is null)
            {
                SelectKeyDirectly(layer.Keys[0]);

                return true;
            }

            if (KeyAdjacency.Next(_visual, SelectedKey.Index, direction) is not { } target
                || layer.FindByIndex(target.Index) is not { } cap)
            {
                return false;
            }

            SelectKeyDirectly(cap);

            return true;
        }

        /// <summary>
        /// Opens a section, standing down everything that belongs to the one being left. The
        /// three stand-downs are deliberately spelled out rather than folded into a helper: they are
        /// <em>interleaved</em> with other work at three of their seven sites, and each of them is
        /// here for its own reason.
        /// </summary>
        public void SelectTab(EditorTab tab)
        {
            // A section this strip does not carry stays shut whichever way it is asked for, so a
            // two-way binding cannot open what the command refuses. There is no longer a second
            // case: a tab that exists always works, because a feature the board lacks is not
            // rendered at all (EditorTabViewModel).
            if (FindTab(tab) is null)
            {
                return;
            }

            // Listening belongs to the keyboard picture, which only the Layout tab draws, so it is
            // ended here or the capture service keeps swallowing keystrokes behind the section the
            // user moved to. There is no second consumer to stand down: the rail is the app's one
            // recording surface and it is deactivated just below.
            _host.CancelRemap();

            // An armed copy is finished with a click on the board; a section that does not draw
            // the board could never finish it, so it ends here too.
            _host.CancelCopyKey();

            // The rail is drawn on the Layout tab only, so a record button armed in it must not go
            // on capturing behind a section that does not show it. The rail is stood down, not
            // closed: coming back to the Layout tab must find it as it was left.
            _host.DeactivateInspector();

            if (_selectedTab != tab)
            {
                _selectedTab = tab;

                _host.OnTabChanged();
            }

            foreach (var entry in Tabs)
            {
                entry.IsSelected = entry.Tab == _selectedTab;
            }

            // The strip is the *open* section's, so the sentence and the count move with the tab.
            // The set itself does not: a tab switch writes nothing.
            _host.RefreshAdvisorySummary();

            // The two macro-insertion commands are only available while the macro panel is on
            // screen, so switching sections has to re-evaluate them.
            _host.NotifyCommands();
        }

        /// <summary>
        /// Switches layers. Anything half-done belongs to the layer it was started on — a key
        /// listening for its new assignment most of all — so the switch cancels it and the whole
        /// key collection is swapped.
        /// <para>
        /// <b>There is no identity guard here and none is wanted</b> (invariant 28). Re-selecting
        /// the layer that is already shown is idempotent because nothing in this method writes to
        /// the model — the guard the other one-of-N commands need is exactly what a method that
        /// writes would have to acquire.
        /// </para>
        /// </summary>
        public void SelectLayer(KeyboardLayerViewModel? layer)
        {
            _host.CancelRemap();
            _host.CancelCopyKey();
            _host.DeactivateInspector();
            ClearSelectedKey();

            foreach (var entry in Layers)
            {
                entry.IsSelected = ReferenceEquals(entry, layer);
            }

            SelectedLayer = layer;

            // The Layout tab's strip is about the layer on screen, so it follows the switch. So is
            // the legend row: its five counts are the shown layer's, and a switch writes nothing,
            // so neither goes through RefreshCounters.
            _host.RefreshAdvisorySummary();
            _host.RefreshLegend();
        }

        /// <summary>
        /// The click contract of specs/10-apps-and-ui.md: the first click selects, a second click
        /// on the same key starts listening, and a click on the listening key cancels it again.
        /// Selecting a different key always cancels listening first.
        /// <para>
        /// An armed <c>Copy key…</c> takes the click ahead of all of that: while a copy is waiting
        /// for its target, the next cap clicked <em>is</em> the target and nothing else
        /// (<c>CompleteCopyKey</c>). Without the interception the second half of the pick would be
        /// read as the click contract's "a second hit on the selected cap" and start a remap on the
        /// very key the user meant to copy from.
        /// </para>
        /// <para>
        /// <b>Every refusal is made here, never by a predicate</b> (invariant 35) — including the
        /// remap the second branch asks for, which the host's <c>BeginRemap</c> declines on its own
        /// terms for a locked position, a load, a save, an armed rail or an open modal.
        /// </para>
        /// </summary>
        public void SelectKey(KeyboardKeyViewModel? key)
        {
            // Clicking a cap is a request for the inspector, whichever branch the click then takes —
            // including a second click on the cap that is already selected, which must reopen a rail
            // the user pressed Escape on rather than only starting a remap. It is why the rail needs
            // Open() at all: Refresh alone cannot tell "the user asked again" from "somebody else's
            // edit went through the funnel".
            if (key is not null)
            {
                _host.OpenInspector();
            }

            if (_host.IsCopyArmed)
            {
                if (key is not null)
                {
                    _host.CompleteCopyKey(key);

                    return;
                }

                // A click that selects nothing is not a target; it ends the pick and then falls
                // through to the ordinary "nothing is selected" branch below.
                _host.CancelCopyKey();
            }

            if (key is null)
            {
                _host.CancelRemap();
                ClearSelectedKey();

                // Nothing is selected, so the rail has nothing to be about. A selection change
                // writes nothing, so it never reaches RefreshCounters — hence the explicit push
                // here and in SelectKeyDirectly.
                _host.RefreshInspector();

                return;
            }

            if (ReferenceEquals(key, SelectedKey))
            {
                if (_host.IsListening)
                {
                    _host.CancelRemap();
                }
                else
                {
                    _host.BeginRemap();
                }

                return;
            }

            SelectKeyDirectly(key);
        }

        /// <summary>
        /// Moves the selection to <paramref name="key"/> and nothing else — no second-click
        /// promotion to listening. It is what the click contract's "a different key" branch does,
        /// and what <c>Review</c> and the arrow keys need: neither reviewing an advisory nor
        /// arrowing onto a cap may arm the keyboard.
        /// </summary>
        public void SelectKeyDirectly(KeyboardKeyViewModel key)
        {
            if (ReferenceEquals(key, SelectedKey))
            {
                return;
            }

            _host.CancelRemap();
            ClearSelectedKey();

            key.IsSelected = true;
            SelectedKey = key;

            _host.RefreshInspector();
        }

        /// <summary>Drops the selection, and the cap's own selected flag with it.</summary>
        public void ClearSelectedKey()
        {
            if (SelectedKey is not null)
            {
                SelectedKey.IsSelected = false;
            }

            SelectedKey = null;
        }

        /// <summary>
        /// <c>Review N</c>'s key half: puts the board's selection on the anchored cap. Handed to
        /// <see cref="AdvisoryStripViewModel"/> as a callback, because the board is this class's.
        /// <para>
        /// It lands through <see cref="SelectKeyDirectly"/>, <b>never</b>
        /// <see cref="SelectKeyCommand"/>: the click contract promotes a second hit on the
        /// already-selected cap into listening, and reviewing is reading.
        /// </para>
        /// </summary>
        public void SelectAnchoredKey(AdvisoryAnchor anchor)
        {
            if (anchor.KeyIndex is not int keyIndex || SelectedLayer?.FindByIndex(keyIndex) is not { } key)
            {
                return;
            }

            SelectKeyDirectly(key);
        }

        /// <summary>
        /// <c>Review N</c>'s macro half: opens the anchored macro where it is edited — the board's
        /// position, on the key inspector's Macro panel. The strip's other callback, for the same
        /// reason the key half is one: the board and the rail are the editor's.
        /// <para>
        /// An anchor names a <em>site</em> (layer, key, slot) while a macro is one logical thing that
        /// may fire from three of them, so a review that merely highlighted the macro could be
        /// pointing at all three at once. Landing on the anchored position is the answer that is
        /// always about the one the advisory is about.
        /// </para>
        /// </summary>
        public void SelectAnchoredMacro(AdvisoryAnchor anchor)
        {
            if (anchor.KeyIndex is not int keyIndex || anchor.LayerIndex is not int layerIndex)
            {
                return;
            }

            _host.EditMacroAt(layerIndex, keyIndex, anchor.MacroIndex ?? MacroSites.FlatListSlot, startRecording: false);
        }

        private EditorTabViewModel? FindTab(EditorTab tab)
        {
            foreach (var entry in Tabs)
            {
                if (entry.Tab == tab)
                {
                    return entry;
                }
            }

            return null;
        }

        private void OnSelectTab(EditorTabViewModel? tab)
        {
            if (tab is null)
            {
                return;
            }

            SelectTab(tab.Tab);
        }
    }

    /// <summary>
    /// What <see cref="EditorSelection"/> needs from the editor it points: the four flags a gesture
    /// is decided from, the eleven calls moving the pointer makes, and the four announcements.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are nineteen members, and a
    /// <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let the
    /// selection reach the whole god class it was split out of. It is implemented by the editor,
    /// explicitly where the member is not already part of that class's public surface — a split
    /// must not make a private method public.
    /// </para>
    /// <para>
    /// <b><see cref="BeginRemap"/>, <see cref="CancelRemap"/> and <see cref="IsListening"/> are the
    /// point of the interface.</b> They belong to <see cref="EditorKeystrokeRouter"/>, and the click
    /// contract's second branch is a call to the first of them — but a collaborator never sees
    /// another collaborator, so the editor forwards all three. That indirection is what let the two
    /// seams, which are genuinely knotted, be split at all.
    /// </para>
    /// <para>
    /// <b>The last four are announcement callbacks</b>, and they are what keeps rule 3 ("a
    /// collaborator announces nothing") intact while five bindable members live out here: the
    /// selection reports that something moved and the editor raises its own property names.
    /// </para>
    /// </summary>
    public interface IEditorSelectionHost
    {
        /// <summary>Whether the profile is still being read; an arrow does not move during a load.</summary>
        bool IsLoading { get; }

        /// <summary>Whether a save is in flight.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Whether a <c>Copy key…</c> is waiting for its target. While it is, the next cap clicked
        /// <em>is</em> the target and the click contract does not run at all.
        /// </summary>
        bool IsCopyArmed { get; }

        /// <summary>
        /// Whether a key is listening for its new assignment — the click contract's second branch
        /// chooses between cancelling and starting on it. <see cref="EditorKeystrokeRouter"/>'s
        /// state, read through the editor.
        /// </summary>
        bool IsListening { get; }

        /// <summary>
        /// Opens the key inspector rail. A click on a cap is a request for it whichever branch the
        /// click then takes, so a rail the user pressed Escape on reopens on the second click too.
        /// </summary>
        void OpenInspector();

        /// <summary>
        /// Hands the rail what the editor is now about. A selection change writes nothing, so it
        /// never reaches <c>RefreshCounters()</c> — this is the explicit push that replaces it.
        /// </summary>
        void RefreshInspector();

        /// <summary>Stands whatever the rail has armed down, without closing it.</summary>
        void DeactivateInspector();

        /// <summary>
        /// Puts the selected key into listening state — <see cref="EditorKeystrokeRouter"/>'s, and
        /// the click contract's second branch. It answers its own gates, so nothing is checked here.
        /// </summary>
        void BeginRemap();

        /// <summary>Leaves listening state; every gesture that moves the pointer ends one first.</summary>
        void CancelRemap();

        /// <summary>Drops an armed <c>Copy key…</c>.</summary>
        void CancelCopyKey();

        /// <summary>Finishes an armed <c>Copy key…</c> onto the cap that was clicked.</summary>
        void CompleteCopyKey(KeyboardKeyViewModel target);

        /// <summary>
        /// Opens one macro position where macros are edited — the Layout tab, the board's position,
        /// the rail's Macro panel. <c>Review N</c>'s macro half is its one caller.
        /// </summary>
        void EditMacroAt(int layerIndex, int keyIndex, int slot, bool startRecording);

        /// <summary>
        /// Narrows the held advisory set onto the strip for the open section and layer. It rebuilds
        /// nothing: the tab and the layer decide what the strip is about, and neither writes.
        /// </summary>
        void RefreshAdvisorySummary();

        /// <summary>Re-reads the legend row's five counts, which are the shown layer's.</summary>
        void RefreshLegend();

        /// <summary>Re-asks every editing command's predicate; the editor decides which ones those are.</summary>
        void NotifyCommands();

        /// <summary>
        /// The layer collection was replaced: the editor raises <c>Layers</c>. Nothing else — the
        /// setter it replaces re-asked no command, because a load's own <c>RefreshCounters()</c>
        /// and the <c>SelectLayer</c> that follows it both do.
        /// </summary>
        void OnLayersChanged();

        /// <summary>
        /// The shown layer moved: the editor raises <c>SelectedLayer</c>, then re-evaluates the
        /// commands — the two things its own setter did before the state moved out here, in that
        /// order and no others.
        /// </summary>
        void OnSelectedLayerChanged();

        /// <summary>
        /// The selected key moved: the editor raises <c>SelectedKey</c>, then re-evaluates the
        /// commands. The same pair, for the same reason.
        /// </summary>
        void OnSelectedKeyChanged();

        /// <summary>
        /// The open section moved: the editor raises <c>SelectedTab</c>. Deliberately <b>not</b>
        /// followed by <c>NotifyCommands()</c> here — <see cref="EditorSelection.SelectTab"/> calls
        /// that itself, unconditionally, which is what the setter this replaces did too.
        /// </summary>
        void OnTabChanged();
    }
}
