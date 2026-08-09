using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The key inspector rail and everything that wires it to the editor (issue #92, mockups
    /// <c>1e</c>/<c>2a</c>/<c>2h</c>). Split out of <see cref="KeyboardEditorViewModel"/>'s main file
    /// for the same reason <c>KeyboardEditorViewModel.Legend.cs</c> was: that file is already the
    /// largest in the app and docs/guides/Coding Conventions.md forbids growing it into a god class.
    /// Everything here is the same class and runs on the same rules.
    ///
    /// <para><b>The rail is not modal, and the whole of this file depends on it.</b> It never sets
    /// and never reads <c>HasActiveOverlay</c>: the board stays clickable while it is open, so the
    /// remap workflow, the armed copy and the keyboard grammar all go on working underneath it. What
    /// it <em>does</em> take part in is capture — a panel that arms a record button owns the next
    /// keystroke — which is why <see cref="KeyboardEditorViewModel.IsCaptureActive"/> grows
    /// <see cref="KeyInspectorViewModel.IsRecording"/> and why the routing table below has a new
    /// first branch.</para>
    ///
    /// <para><b>Everything the rail shows is pushed.</b> Core raises no change notification at all,
    /// so the rail is refreshed from <c>RefreshLegend()</c> — the tail of the one funnel every
    /// mutation path already ends in — and from the three paths that move the <em>selection</em>
    /// without writing anything.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// The key inspector rail: the Layout tab's third column. Built once, in the constructor,
        /// and refreshed from then on — see the type's remarks.
        /// </summary>
        public KeyInspectorViewModel Inspector { get; }

        /// <summary>
        /// <c>⌥↑</c> — moves the selected macro step one place earlier (mockup <c>2i</c>: "drag ⠿ ·
        /// ⌥↑↓"). It is surfaced on the editor because that is where the grammar is dispatched from
        /// (<c>KeyboardEditorView.OnPreviewKeyDown</c>): this app has no <c>KeyBinding</c>s and no
        /// <c>KeyGesture</c>s anywhere, and a shortcut that only worked while the rail happened to
        /// hold focus would not be one.
        /// </summary>
        public IRelayCommand MoveMacroStepUpCommand => _macroInspectorPanel.Steps.MoveStepUpCommand;

        /// <summary><c>⌥↓</c> — moves the selected macro step one place later.</summary>
        public IRelayCommand MoveMacroStepDownCommand => _macroInspectorPanel.Steps.MoveStepDownCommand;

        /// <summary>
        /// The session's assignment history, behind the picker's <c>Recent</c> chip. <b>One store per
        /// editor</b>, shared by the rail's picker, the Tap &amp; hold panel's two, the
        /// macro-insertion picker and the Macro panel's chord composer, so an action inserted into a
        /// macro is offered by the rail too. In memory and never persisted; see
        /// <see cref="RecentTokenStore"/>.
        /// </summary>
        private readonly RecentTokenStore _recentTokens = new();

        private RemapPanelViewModel _remapPanel = null!;
        private TapAndHoldPanelViewModel _tapAndHoldPanel = null!;
        private MacroInspectorPanelViewModel _macroInspectorPanel = null!;
        private EventHandler _inspectorRecordingChangedHandler = null!;
        private EventHandler _remapAssignedHandler = null!;
        private EventHandler _tapAndHoldAssignedHandler = null!;
        private EventHandler _macroInspectorAssignedHandler = null!;
        private EventHandler _macroInspectorNameChangedHandler = null!;

        /// <summary>
        /// Whether the capture service is running <b>because a rail panel asked for it</b>. It is the
        /// "never stop a capture you did not start" rule made explicit: the rail raises
        /// <see cref="KeyInspectorViewModel.RecordingChanged"/> on every mode switch and every
        /// selection change too, and answering one of those with an unconditional
        /// <c>_capture.Stop()</c> would deafen a panel that really is armed.
        /// </summary>
        private bool _isInspectorCapturing;

        /// <summary>
        /// <b>The stand-down seam</b> (issue #122, AC 3): the one entry point for "the user did
        /// something else in the app, so the keyboard is not the macro's any more". Recording is the
        /// only editor state that silently claims every keystroke in the window, so a user who has
        /// moved on must never be left recording into a panel they are no longer looking at.
        ///
        /// <para>Driven from the view, which is where those interactions are observable
        /// (<c>KeyboardEditorView</c>): the window losing focus (<c>Window.Deactivated</c> — an
        /// OS-reserved chord like ⌘Tab never reaches the app at all, so this is what recovers from
        /// one), a tunnelled pointer press anywhere but the panel's own record control
        /// (<see cref="IsRecordingControl"/>), and focus landing in a text input — which used to
        /// suspend capture <em>silently</em> and let the user type into a box while the banner still
        /// said they were recording (docs/app/keystroke-capture.md).</para>
        ///
        /// <para>The existing stand-downs are untouched and still do their own work: a mode switch,
        /// an editor-tab switch, a layer switch, a key change, a modal opening
        /// (<see cref="DeactivateInspector"/>) and the panel's own Stop.</para>
        ///
        /// <para><b>Idempotent, and silent when nothing moved.</b> The guard is what keeps it from
        /// raising <c>RecordingChanged</c> on every click of an editor that is not recording — which
        /// would put the capture service's start/stop bookkeeping through a cycle per pointer press.</para>
        /// </summary>
        public void StopRecordingOnInteraction()
        {
            if (!Inspector.IsRecording)
            {
                return;
            }

            Inspector.Deactivate();
        }

        /// <summary>
        /// Whether <paramref name="command"/> is the showing panel's own record/stop control, which
        /// <see cref="StopRecordingOnInteraction"/> must leave alone — see
        /// <see cref="KeyInspectorPanelViewModel.IsRecordingControl"/>.
        /// </summary>
        public bool IsRecordingControl(ICommand? command)
        {
            return Inspector.ActivePanel?.IsRecordingControl(command) == true;
        }

        /// <summary>
        /// Opens one macro position <b>where macros are edited</b>: goes to the Layout tab, puts the
        /// board's selection on <paramref name="keyIndex"/> of <paramref name="layerIndex"/>, points
        /// the rail at its Macro panel and — when <paramref name="startRecording"/> — arms Record.
        /// <paramref name="slot"/> is 1..5 on a slot device and 0 on a flat-list one.
        /// <para>
        /// The advisory strip's macro callback is its one caller
        /// (<c>KeyboardEditorViewModel.SelectAnchoredMacro</c>): an advisory names a <em>site</em> —
        /// layer, key, slot — and this is what turns a site back into the rail showing it.
        /// </para>
        /// </summary>
        public void EditMacroAt(int layerIndex, int keyIndex, int slot, bool startRecording)
        {
            if (FindLayer(layerIndex) is not { } layer)
            {
                return;
            }

            // The board and the rail live on the Layout tab, so going there is the first half of
            // "open this macro where it is edited".
            SelectTab(EditorTab.Keys);

            if (!ReferenceEquals(layer, SelectedLayer))
            {
                SelectLayer(layer);
            }

            if (layer.FindByIndex(keyIndex) is not { } key)
            {
                return;
            }

            // Never SelectKeyCommand: the click contract promotes a second hit on the already
            // selected cap into a remap, and this is not a click.
            SelectKeyDirectly(key);

            // Before the rail is refreshed, because its Macro panel reads this field to decide which
            // of the key's macros it is showing (06 §1). In-memory only; it is never serialized.
            if (slot >= Macro.MinMacroIndex && key.Key.UsesMacroSlots)
            {
                key.Key.ActiveMacroIndex = slot;
            }

            Inspector.Open();

            SelectInspectorMode(KeyInspectorMode.Macro);

            RefreshInspector();

            if (startRecording)
            {
                ArmMacroRecording();
            }
        }

        /// <summary>
        /// Builds the rail, its two mode panels and every subscription between them. Called from the
        /// constructor, after the three commands the footer runs exist.
        ///
        /// <para><b>The dialect is a device fact, not a profile fact.</b> The picker needs one to
        /// build its catalog and <c>Layout</c> does not exist until <see cref="LoadAsync"/> has run —
        /// but <c>KeyboardLayout.Dialect</c> is itself nothing but
        /// <see cref="KeyboardLayout.DialectFor"/> over the device id, which this class has
        /// had since its constructor. Asking the same function directly is what lets the rail be
        /// built once, eagerly, instead of appearing halfway through a load; that the two agree is
        /// pinned by a test rather than assumed.</para>
        ///
        /// <para><b>The two <c>Assigned</c> handlers are required, not decorative.</b> A panel writes
        /// into <c>KeyboardKey</c> and Core announces nothing, so a cap that is not re-read by hand
        /// shows the old legend and every counter above it stays stale.</para>
        /// </summary>
        private KeyInspectorViewModel CreateInspector()
        {
            _remapPanel = new RemapPanelViewModel(KeyboardLayout.DialectFor(Device.DeviceId), _recentTokens);
            _tapAndHoldPanel = new TapAndHoldPanelViewModel(
                Device.DeviceId,
                Device.Firmware,
                _urlLauncher,
                _recentTokens);

            // The Macro panel's footer runs two of this editor's commands — the armed macro copy and
            // its cancel (KeyboardEditorViewModel.Legend.cs) — injected exactly as the rail's own
            // footer takes CopyKeyCommand/CancelCopyKeyCommand. The panel neither owns nor duplicates
            // the armed state: there is one CopySource, one prompt and one Escape route.
            //
            // It no longer resolves a MacroLibrary (issue #141): the library was an app-level
            // identity over macros the hardware keeps as independent copies, and the panel's name
            // dropdown that read it is an inline field over Macro.Name now.
            //
            // It no longer takes the session's recent-token store (issue #139): #128's chord composer
            // hosted a fourth picker over it, and the composer that replaced it sets a step's key from
            // `Record` alone. The store keeps its other three pickers.
            _macroInspectorPanel = new MacroInspectorPanelViewModel(
                Device,
                _urlLauncher,
                CopyMacroCommand,
                CancelCopyKeyCommand);

            _remapAssignedHandler = (_, _) => RefreshCounters();
            _tapAndHoldAssignedHandler = (_, _) => OnTapAndHoldAssigned();
            _macroInspectorAssignedHandler = (_, _) => OnMacroInspectorAssigned();
            _macroInspectorNameChangedHandler = (_, _) => OnMacroNameChanged();

            _remapPanel.Assigned += _remapAssignedHandler;
            _tapAndHoldPanel.Assigned += _tapAndHoldAssignedHandler;
            _macroInspectorPanel.Assigned += _macroInspectorAssignedHandler;
            _macroInspectorPanel.NameChanged += _macroInspectorNameChangedHandler;

            var inspector = new KeyInspectorViewModel(
                ResetKeyCommand,
                CopyKeyCommand,
                CancelCopyKeyCommand,
                [_remapPanel, _tapAndHoldPanel, _macroInspectorPanel]);

            _inspectorRecordingChangedHandler = (_, _) => OnInspectorRecordingChanged();

            inspector.RecordingChanged += _inspectorRecordingChangedHandler;

            return inspector;
        }

        /// <summary>
        /// Hands the rail the editor's current state. Called from the tail of
        /// <see cref="RefreshLegend"/> — so every path that can write the layout reaches it — and
        /// from the selection paths, which move what the rail is <em>about</em> without writing
        /// anything at all.
        /// </summary>
        private void RefreshInspector()
        {
            Inspector.Refresh(SelectedKey, SelectedLayer, Layout, _advisories);
        }

        /// <summary>
        /// Stands whatever the rail has armed down, without closing it. Called wherever the editor
        /// takes the keyboard back: a layer switch, a section switch, a save, an import, and a modal
        /// panel opening over the board.
        /// </summary>
        private void DeactivateInspector()
        {
            Inspector.Deactivate();
        }

        private KeyboardLayerViewModel? FindLayer(int layerIndex)
        {
            foreach (var layer in Layers)
            {
                if (layer.Index == layerIndex)
                {
                    return layer;
                }
            }

            return null;
        }

        /// <summary>
        /// Puts the rail on one of its modes through the rail's own command, so the mode-switch
        /// warning and the panel hand-off happen exactly as they do for a click on the tab.
        /// </summary>
        private void SelectInspectorMode(KeyInspectorMode mode)
        {
            foreach (var tab in Inspector.Tabs)
            {
                if (tab.Mode == mode)
                {
                    Inspector.SelectModeCommand.Execute(tab);

                    return;
                }
            }
        }

        /// <summary>
        /// Arms the rail's Macro panel for the next physical keystroke. Its Record is a
        /// <em>toggle</em>, so an already-armed panel is left alone rather than stood down — and the
        /// panel answers its own availability gates, which is why nothing is checked here.
        /// </summary>
        private void ArmMacroRecording()
        {
            if (!_macroInspectorPanel.IsRecording)
            {
                _macroInspectorPanel.RecordCommand.Execute(null);
            }
        }

        /// <summary>
        /// A rail panel wrote a tap-and-hold. Invariant 3: the cap is re-read by hand, and then the
        /// funnel rebuilds the counters, the advisories, the legend and the dirty flag.
        /// </summary>
        private void OnTapAndHoldAssigned()
        {
            SelectedKey?.RefreshFromModel();

            RefreshCounters();
        }

        /// <summary>
        /// The showing panel started or stopped waiting for a physical keystroke. Capture belongs to
        /// the editor and never to a panel (docs/app/keyboard-editor.md, invariant 4), so this is
        /// where the service is turned on and off — with one guard a plain start/stop would not need:
        /// <see cref="_isInspectorCapturing"/>, because the rail announces its recording state on
        /// every mode switch and every selection change, not only when something really armed.
        /// </summary>
        private void OnInspectorRecordingChanged()
        {
            var isRecording = Inspector.IsRecording;

            OnPropertyChanged(nameof(IsCaptureActive));

            if (isRecording == _isInspectorCapturing)
            {
                return;
            }

            _isInspectorCapturing = isRecording;

            if (isRecording)
            {
                // A listening key, a recording macro and an armed rail field are three answers to
                // "what does the next keystroke mean", and only one of them may be live
                // (invariant 5).
                CancelRemap();

                _capture.Start();
            }
            else
            {
                _capture.Stop();
            }

            NotifyCommands();
        }

        /// <summary>
        /// The rail's Macro panel wrote to the profile's macros. Same shape as
        /// <see cref="OnTapAndHoldAssigned"/>, and required for the same reason: Core announces
        /// nothing, so the cap is re-read by hand (its macro dot) and then the funnel rebuilds the
        /// counters, the advisories, the legend and the dirty flag.
        /// <para>
        /// It ends in <see cref="NotifyCommands"/> as well, because a macro that has just been
        /// created or deleted changes the answer to "is there a macro to copy" —
        /// <c>CopyMacroCommand</c>'s predicate, which nothing else re-asks after a model write.
        /// </para>
        /// </summary>
        private void OnMacroInspectorAssigned()
        {
            SelectedKey?.RefreshFromModel();

            RefreshCounters();

            NotifyCommands();
        }

        /// <summary>
        /// The rail's Macro panel renamed the open macro. <b>A different bit from
        /// <see cref="OnMacroInspectorAssigned"/></b>, which is why the panel raises a second event:
        /// a name is not in <c>layout&lt;n&gt;.txt</c> at all, so no session can see it move and
        /// only the per-profile rename mark makes it unsaved work
        /// (<c>KeyboardEditorViewModel.MacroNames.cs</c>).
        /// <para>
        /// The funnel runs anyway. Nothing about a name moves a counter, but the mark is folded into
        /// <c>IsDirty</c> by <c>RefreshDirtyState</c>, which is the funnel's own tail — and the
        /// legend and the rail are refreshed from the same place, so one call keeps every reader of
        /// the name honest instead of a list somebody has to maintain.
        /// </para>
        /// </summary>
        private void OnMacroNameChanged()
        {
            MarkMacroNamesDirty();

            RefreshCounters();
        }

        /// <summary>
        /// Whether the keystroke being routed belongs to an armed rail panel — the new first branch
        /// of <see cref="OnKeystrokeCaptured"/>.
        /// <para>
        /// <b>Armed, not merely open</b>, and that is the one real difference from the modal this
        /// replaces. A feature panel took a keystroke on being <em>open</em>, because the scrim under
        /// it meant nothing else could want one; the rail is not modal, so a panel that is merely
        /// showing must let the keystroke fall through to the cap the user is remapping beside it.
        /// </para>
        /// </summary>
        private bool TryRouteToInspector(CapturedKeystroke keystroke)
        {
            if (Inspector.ActivePanel is not IKeystrokeSink { WantsKeystrokes: true } sink)
            {
                return false;
            }

            // Latched for the view, which is still to see this same key event: the field disarms as
            // it takes the key, and "nothing is armed any more" must not read as "the rail was idle,
            // close it" (see TryTakeOverlayKeystroke). It is set BEFORE the dispatch for exactly
            // that reason — afterwards is already too late.
            _hasOverlayTakenKeystroke = true;

            sink.ReceiveKeystroke(keystroke);

            return true;
        }

        /// <summary>
        /// Stands the rail down for good and drops every subscription it owns.
        /// <para>
        /// <b>The rail is closed <em>before</em> the handlers come off, and the order is the whole
        /// point.</b> Closing it deactivates the showing panel, which raises
        /// <see cref="KeyInspectorViewModel.RecordingChanged"/>, which is what runs
        /// <see cref="OnInspectorRecordingChanged"/> and hands the capture service back. Unsubscribe
        /// first and an armed record button would leave capture running for an editor that no longer
        /// exists — and the service is app-wide, so it would go on swallowing the dashboard's
        /// keystrokes behind us.
        /// </para>
        /// <para>
        /// <b>This is also the navigation path.</b> The shell disposes the open editor whenever it
        /// is replaced — Home, or another device (<c>MainWindowViewModel.CloseEditor</c>) — so
        /// making <see cref="Dispose"/> authoritative is what covers "the user navigated away with a
        /// hold action half-recorded", without this class having to know anything about how the
        /// shell asks to close.
        /// </para>
        /// </summary>
        private void DetachInspector()
        {
            Inspector.Close();

            Inspector.RecordingChanged -= _inspectorRecordingChangedHandler;

            _remapPanel.Assigned -= _remapAssignedHandler;
            _tapAndHoldPanel.Assigned -= _tapAndHoldAssignedHandler;
            _macroInspectorPanel.Assigned -= _macroInspectorAssignedHandler;
            _macroInspectorPanel.NameChanged -= _macroInspectorNameChangedHandler;

            // Belt to that brace: a panel that never announced itself cannot leave a claim behind.
            if (_isInspectorCapturing)
            {
                _isInspectorCapturing = false;

                _capture.Stop();
            }
        }
    }
}
