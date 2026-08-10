using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// §11.6's <c>Search Keys (Macro)</c> and ⌘F: the two commands that put a token picker over the
    /// editor, the gate that says whether there is a macro to insert into, and the one write that
    /// appends the answer to it.
    /// <para>
    /// It is a type of its own rather than seven more members on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. What holds these
    /// seven together is a single question — <b>which macro is open, and may a keystroke be
    /// appended to it?</b> — asked by the predicate, answered off the model by
    /// <see cref="FindOpenMacro"/>, and acted on by the insert. Splitting them across a class that
    /// also loads profiles is what let the gate and the write drift apart before.
    /// </para>
    /// <para>
    /// <b>It is not a view model.</b> The editor publishes both commands under its own names —
    /// <c>InsertSpecialActionCommand</c> is bound in <c>Views/KeyboardEditorView.axaml</c> and
    /// <c>OpenSearchCommand</c> is run by the grammar in its code-behind — so a view never sees this
    /// type. Everything it reads about the editor arrives through <see cref="IMacroInsertionHost"/>
    /// rather than a back-reference.
    /// </para>
    /// <para>
    /// <b>The insert ends in the host's <c>RefreshCounters()</c></b> (docs/app/keyboard-editor.md,
    /// invariant 16): Core announces nothing, so the funnel is what re-reads the rail's step list,
    /// the counters, the advisories and the dirty flag.
    /// </para>
    /// </summary>
    public sealed class MacroInsertionHost
    {
        /// <summary>Opens Search Keys over the macro and inserts the picked action (11 §11.6).</summary>
        public IRelayCommand InsertSpecialActionCommand { get; }

        /// <summary>
        /// ⌘F, the grammar's "focus the token search from anywhere in the editor"
        /// (docs/design/mockups.md <c>2b</c>).
        /// <para>
        /// It has somewhere to write now. On the Layout tab it puts the caret in the <b>key
        /// inspector's</b> own search field, where ↵ assigns the picked action to the selected
        /// position — which is what the accelerator was always meant to do, and could not before the
        /// rail existed. With the rail on its Macro panel it <b>is</b> the insertion picker
        /// (<see cref="InsertSpecialActionCommand"/>): finding a token there means inserting it.
        /// </para>
        /// </summary>
        public IRelayCommand OpenSearchCommand { get; }

        private readonly IMacroInsertionHost _host;
        private readonly RecentTokenStore _recentTokens;

        /// <summary>
        /// Builds the two commands over the editor they act on and the session's assignment history.
        /// <paramref name="recentTokens"/> is the editor's <b>one</b> store, shared with the rail's
        /// picker and the Tap &amp; hold panel's two, so an action inserted into a macro is offered
        /// by the rail's own <c>Recent</c> chip afterwards.
        /// </summary>
        public MacroInsertionHost(IMacroInsertionHost host, RecentTokenStore recentTokens)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _recentTokens = recentTokens ?? throw new ArgumentNullException(nameof(recentTokens));

            InsertSpecialActionCommand = new RelayCommand(InsertSpecialAction, CanInsertIntoMacro);
            OpenSearchCommand = new RelayCommand(OpenSearch, CanOpenSearch);
        }

        /// <summary>
        /// §11.6's insertion targets the macro the <b>key inspector</b> has open — since issue #93
        /// that is the app's one macro editor — so three things have to be true: the rail is open,
        /// it is showing its Macro panel, and the selected position really carries a macro. Without
        /// the mode test the button would stay live beside a Remap panel and the picked token would
        /// be appended to a macro the user cannot see.
        /// </summary>
        public bool CanInsertIntoMacro()
        {
            return _host.Layout is not null
                   && _host.Inspector.IsOpen
                   && _host.Inspector.SelectedMode == KeyInspectorMode.Macro
                   && FindOpenMacro() is not null
                   && !_host.IsLoading
                   && !_host.IsBusy
                   && _host.ActiveOverlay is null;
        }

        /// <summary>Whether ⌘F has anywhere to go: a loaded editor that is neither reading nor writing.</summary>
        public bool CanOpenSearch()
        {
            return _host.Layout is not null && !_host.IsLoading && !_host.IsBusy;
        }

        /// <summary>
        /// The macro the rail's Macro panel is editing, or null. It is read off the model rather than
        /// asked of the panel: the panel holds it privately, and both stores answer the same two
        /// questions the panel asks — the key's <b>active</b> slot on a slot device (which the panel
        /// normalises to the first populated one when it reads), the layer-plus-trigger entry on a
        /// flat-list one (06 §1).
        /// </summary>
        public Macro? FindOpenMacro()
        {
            if (_host.SelectedKey is not { } key || _host.Layout is not { } layout)
            {
                return null;
            }

            if (!layout.UsesFlatMacroList)
            {
                return key.Key.GetMacro(key.Key.ActiveMacroIndex);
            }

            var flat = layout.FindMacros(_host.SelectedLayer?.Index ?? Macro.UnassignedIndex, key.Key.TriggerKey.Code);

            return flat.Count > 0 ? flat[0] : null;
        }

        /// <summary>
        /// Appends one key to the macro the rail has open — §11.6's <c>Search Keys (Macro)</c> hook.
        /// The write lands on the model and then goes through the editor's one refresh funnel, which
        /// is what re-reads the rail's step list, the counters, the advisories and the dirty flag;
        /// Core announces nothing on its own. False when no macro is open.
        /// </summary>
        public bool InsertIntoOpenMacro(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (FindOpenMacro() is not { } macro)
            {
                return false;
            }

            macro.AddKeystroke(new Keystroke(key));

            _host.RefreshCounters();

            return true;
        }

        /// <summary>
        /// §11.6's <c>Search Keys (Macro)</c>: the same picker the key inspector hosts, wrapped in a
        /// modal because an insertion is a question with one answer that has to come back here. The
        /// session's <c>Recent</c> history is shared, so an action inserted into a macro is offered
        /// by the rail's own chip afterwards.
        /// </summary>
        private void InsertSpecialAction()
        {
            if (!CanInsertIntoMacro())
            {
                return;
            }

            var overlay = new TokenPickerOverlayViewModel(
                TokenPickerOverlayViewModel.MacroTitle,
                _host.Layout!.Dialect,
                _recentTokens);

            ShowMacroInsertOverlay(
                overlay,
                handler => overlay.Selected += handler,
                handler => overlay.Selected -= handler);
        }

        /// <summary>
        /// ⌘F. With the rail on its Macro panel this <em>is</em> the insertion picker, so the
        /// token the user searches for is inserted where they were working. Everywhere else it puts
        /// the caret in the <b>key inspector's</b> search field — the rail is not modal, so nothing
        /// is opened over anything: the picker is already on screen beside the board, and ↵ on a row
        /// assigns it to the selected position.
        /// <para>
        /// A modal panel that is already up keeps the keyboard: ⌘F never replaces one feature panel
        /// with another, and it never reaches past a scrim into the rail underneath it.
        /// </para>
        /// </summary>
        private void OpenSearch()
        {
            if (!CanOpenSearch())
            {
                return;
            }

            if (_host.ActiveOverlay is TokenPickerOverlayViewModel picker)
            {
                picker.FocusSearch();

                return;
            }

            if (CanInsertIntoMacro())
            {
                InsertSpecialAction();

                return;
            }

            if (_host.ActiveOverlay is not null)
            {
                return;
            }

            // The rail's own Remap panel. It refuses politely on a locked position and while nothing
            // is selected — the panel decides, not this class.
            _host.Inspector.Open();

            _host.FocusRemapSearch();
        }

        /// <summary>
        /// Shows a one-shot panel whose single result is a keystroke to append to the macro under
        /// edit — the Macro Timing Delays and Search Keys panels of §11.3/§11.6. Both hooks come
        /// off again the moment the panel closes, however it closed, so a dismissed panel can
        /// never write into the macro afterwards.
        /// </summary>
        private void ShowMacroInsertOverlay(
            EditorOverlayViewModel overlay,
            Action<Action<KeyDefinition>> subscribe,
            Action<Action<KeyDefinition>> unsubscribe)
        {
            _host.ShowOverlay(overlay);

            if (!ReferenceEquals(_host.ActiveOverlay, overlay))
            {
                return;
            }

            // A lambda, not a method group: InsertIntoOpenMacro reports "no macro is being edited"
            // with a bool this path has nothing to do with, and the panel may be gone by then.
            var insert = new Action<KeyDefinition>(key => InsertIntoOpenMacro(key));

            EventHandler? closed = null;

            closed = (_, _) =>
            {
                unsubscribe(insert);

                overlay.Closed -= closed;
            };

            subscribe(insert);

            overlay.Closed += closed;
        }
    }

    /// <summary>
    /// What <see cref="MacroInsertionHost"/> needs from the editor it inserts into: the model and
    /// the selection the open macro is found from, the rail and the overlay its two gates read, and
    /// the three calls a picker makes.
    /// <para>
    /// A narrow interface rather than a bundle of delegates because there are ten members, and a
    /// <see cref="KeyboardEditorViewModel"/> back-reference rather than either would let this reach
    /// the whole god class it was split out of. It is implemented by the editor, explicitly where
    /// the member is not already part of that class's public surface — a split must not make a
    /// private method public.
    /// </para>
    /// </summary>
    public interface IMacroInsertionHost
    {
        /// <summary>The loaded model; its dialect is what the picker spells its catalog in.</summary>
        KeyboardLayout? Layout { get; }

        /// <summary>The position whose macro the rail has open.</summary>
        KeyboardKeyViewModel? SelectedKey { get; }

        /// <summary>The layer the picture is showing — a flat-list board's macros are found by it.</summary>
        KeyboardLayerViewModel? SelectedLayer { get; }

        /// <summary>The key inspector rail: whether it is open, and which mode it is on.</summary>
        KeyInspectorViewModel Inspector { get; }

        /// <summary>The feature panel over the editor, or null. A modal already up keeps the keyboard.</summary>
        EditorOverlayViewModel? ActiveOverlay { get; }

        /// <summary>Whether the profile is still being read.</summary>
        bool IsLoading { get; }

        /// <summary>Whether a save is in flight.</summary>
        bool IsBusy { get; }

        /// <summary>
        /// Opens a panel over the editor, standing every other keystroke consumer down first. The
        /// sequencing is the editor's and is deliberately not repeated here.
        /// </summary>
        void ShowOverlay(EditorOverlayViewModel overlay);

        /// <summary>Puts the caret in the rail's own Remap panel search field — ⌘F's other half.</summary>
        void FocusRemapSearch();

        /// <summary>
        /// The editor's one post-write funnel (docs/app/keyboard-editor.md, invariant 16). The
        /// insert ends in it, as a call rather than an inlined copy.
        /// </summary>
        void RefreshCounters();
    }
}
