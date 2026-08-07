using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the editor owes the Macros tab (issue #93, mockup <c>1i</c>) — its
    /// <see cref="IMacroLibraryHost"/> half, and the one push that keeps it current. Split out of
    /// <see cref="KeyboardEditorViewModel"/>'s main file for the same reason
    /// <c>KeyboardEditorViewModel.Legend.cs</c>, <c>…Inspector.cs</c> and <c>…MacroLibrary.cs</c>
    /// were: that file is the largest in the app and docs/guides/Coding Conventions.md forbids
    /// growing it into a god class. Everything here is the same class and runs on the same rules.
    ///
    /// <para><b>The tab is a library and never an editor</b> ("selecting a key edits its macro right
    /// here — the Macros tab is a library, not the editor", mockup <c>2i</c>). So every one of its
    /// "open this macro" actions lands on <see cref="EditMacroAt"/>, which goes to the <em>board</em>
    /// — Layout tab, position selected, rail on its Macro panel — rather than opening an editor of
    /// its own. There is exactly one recording surface in this app and it is the rail.</para>
    ///
    /// <para><b>Everything the tab shows is pushed</b>, exactly like the rail's: Core raises no
    /// change notification, so <see cref="RefreshMacroLibraryPanel"/> hangs off the tail of
    /// <c>RefreshLegend()</c> — the one funnel every mutation path already ends in — and off the two
    /// selection paths, which move what the slot branch is <em>about</em> without writing
    /// anything.</para>
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <inheritdoc />
        public void CommitMacroLibraryEdit()
        {
            // A delete or a duplicate moves names about without renaming anything, so the dirty flag
            // has to be set by hand — see KeyboardEditorViewModel.MacroLibrary.cs for why a macro
            // name is part of the session's dirty model at all.
            MarkMacroNamesDirty();

            RefreshCounters();
        }

        /// <inheritdoc />
        public void RefreshMacroViews()
        {
            RefreshCounters();
        }

        /// <inheritdoc />
        public void PickMacroKey()
        {
            // The board is the Layout tab's; the Macros tab draws none of its own.
            SelectTab(EditorTab.Keys);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public async Task<bool> ConfirmMacroDeleteAsync(
            string title,
            string message,
            string confirmCaption,
            string declineCaption)
        {
            MessageBoxOutcome outcome;

            try
            {
                outcome = await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = title,
                    Message = message,
                    Icon = MessageBoxIcon.Confirmation,
                    Buttons = MessageBoxButtons.YesNo,
                    YesCaption = confirmCaption,
                    NoCaption = declineCaption,

                    // The app's one red answer (mockup 1k): a delete takes the macro off every key
                    // that fires it. There is deliberately NO suppression key — the question exists
                    // to name those keys, and an answer the user switched off would take a macro off
                    // three positions in silence.
                    DestructiveResult = MessageBoxResult.Yes
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // A box that cannot be put on screen must delete nothing, and must not bring the app
                // down either — the same rule the two reset confirmations follow.
                return false;
            }

            return outcome.Result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// Hands the Macros tab the editor's current state. See the type's remarks for why it is a
        /// push and where it is called from.
        /// </summary>
        private void RefreshMacroLibraryPanel()
        {
            MacroLibraryPanel.Refresh(SelectedKey, SelectedLayer, Layout);
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
    }
}
