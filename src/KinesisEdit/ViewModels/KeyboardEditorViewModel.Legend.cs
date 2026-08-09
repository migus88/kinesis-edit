using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The legend row under the board and the copy-key interaction it hosts (mockups 1e/2a). Split
    /// out of <see cref="KeyboardEditorViewModel"/>'s main file because that file is already the
    /// largest in the app and docs/guides/Coding Conventions.md forbids growing it into a god
    /// class; everything here is the same class and runs on the same rules.
    /// </summary>
    public sealed partial class KeyboardEditorViewModel
    {
        /// <summary>
        /// The legend row under the board: the five layer-scoped counts plus the row's two actions.
        /// Hosted by the Layout board only — the Lighting tab draws no state badges
        /// (<c>KeyboardView.ShowsStateBadges</c>), so it counts no states either.
        /// </summary>
        public BoardLegendViewModel BoardLegend { get; }

        /// <summary>
        /// The key an armed copy will copy <em>from</em>, or null when no copy is armed. The
        /// interaction is the editor's existing click-then-press grammar with the press replaced by
        /// a second click: arm from the selected key, then the next cap clicked is the target.
        /// </summary>
        public KeyboardKeyViewModel? CopySource
        {
            get => _copySource;
            private set
            {
                if (SetProperty(ref _copySource, value))
                {
                    OnPropertyChanged(nameof(IsCopyArmed));

                    NotifyCommands();
                }
            }
        }

        /// <summary>Whether a copy is armed and waiting for the user to click its target.</summary>
        public bool IsCopyArmed => _copySource is not null;

        /// <summary>
        /// What the legend row says about the copy in progress: the pick prompt, a refusal, or an
        /// empty string. Held here rather than on <see cref="BoardLegend"/> because it is editor
        /// state — the row only renders it.
        /// </summary>
        public string CopyPrompt
        {
            get => _copyPrompt;
            private set => SetProperty(ref _copyPrompt, value);
        }

        /// <summary>
        /// Arms a copy from the selected key. <b>One action, three entry points</b>: the legend
        /// row's <c>Copy key…</c> (mockup <c>1e</c>), the inspector footer's <c>Copy to…</c> and the
        /// locked-key panel's. They are the same command on purpose — a second copy path would be a
        /// second set of refusals to keep in step — and the earlier note in this file calling the
        /// footer's a different action is superseded by issue #92.
        /// </summary>
        public IRelayCommand CopyKeyCommand { get; }

        /// <summary>
        /// Arms a copy of <b>one macro</b> — the one the rail's Macro panel is editing — onto the
        /// next cap clicked (issue #141). Handed to <see cref="MacroInspectorPanelViewModel"/>,
        /// whose footer is its only entry point.
        /// <para>
        /// It is the <em>same</em> armed state as <see cref="CopyKeyCommand"/>'s, scoped by
        /// <see cref="_copyMacroSlot"/>, and that is deliberate: one <see cref="CopySource"/>, one
        /// prompt, one Escape route, one disarm set. A second armed state would be a second set of
        /// refusals to keep in step, which is the very thing the three entry points above were
        /// folded together to avoid.
        /// </para>
        /// </summary>
        public IRelayCommand CopyMacroCommand { get; }

        /// <summary>
        /// Disarms whichever copy is armed — whole-key or macro — without writing anything; the
        /// view's Escape route and the Macro panel's <c>Cancel copy</c> both run it.
        /// </summary>
        public IRelayCommand CancelCopyKeyCommand { get; }

        private KeyboardKeyViewModel? _copySource;
        private string _copyPrompt = string.Empty;

        /// <summary>
        /// Which of <see cref="CopySource"/>'s macro slots an armed copy is carrying, or null when
        /// the armed copy is the whole-key one. It is the <b>scope</b> of the one armed state:
        /// <see cref="CompleteCopyKey"/> branches on it, and every path that disarms clears it.
        /// <para>
        /// <see cref="MacroSites.FlatListSlot"/> (0) on a flat-list board (06 §1), 1..5 on a slot
        /// device — the slot the panel is editing, never a literal.
        /// </para>
        /// </summary>
        private int? _copyMacroSlot;

        /// <summary>
        /// §1.3's precondition, restated: a copy needs a <b>source</b>, and a board that is not
        /// being written to underneath it. The <c>!IsLoading &amp;&amp; !IsBusy</c> pair is the same
        /// guard every other model-writing command carries — a save serializes the model on a
        /// background thread.
        /// <para>
        /// <b>The source does not have to be editable.</b> A locked position (05 §5.3) can be copied
        /// <em>from</em> — its assignment is perfectly readable — and never <em>onto</em>, which
        /// <see cref="CompleteCopyKey"/>'s <c>!target.CanEdit</c> refusal already covers. Requiring
        /// <c>CanEdit</c> here refused the whole action rather than one direction of it, which is
        /// what made the locked-key panel's <c>Copy from…</c>/<c>Copy to…</c> asymmetry cosmetic.
        /// </para>
        /// <para>
        /// <b>It no longer consults the open overlay.</b> The key inspector's footer runs this very
        /// command, and the rail is not modal — nothing about it may be phrased in terms of
        /// <c>HasActiveOverlay</c>. Nothing is lost: <see cref="ShowOverlay"/> disarms an armed copy
        /// on the way in, and the scrim over the board is what stops a pick being finished under a
        /// panel.
        /// </para>
        /// </summary>
        private bool CanCopyKey()
        {
            return SelectedKey is not null
                   && !IsLoading
                   && !IsBusy;
        }

        /// <summary>
        /// Enters the armed state. Listening is ended first for the same reason
        /// <see cref="ShowOverlay"/> ends it: the two are different answers to "what does the next
        /// input mean", and only one of them may be live (invariant 5's shape, one level up).
        /// </summary>
        private void ArmCopyKey()
        {
            if (!CanCopyKey())
            {
                return;
            }

            CancelRemap();

            // A whole-key copy is not a macro copy: arming one over the other re-scopes the single
            // armed state rather than leaving a stale slot behind for CompleteCopyKey to branch on.
            _copyMacroSlot = null;

            CopySource = SelectedKey;
            CopyPrompt = BoardLegendViewModel.CopyTargetPrompt;

            RefreshLegend();
        }

        /// <summary>
        /// §1.3's precondition again, for one macro rather than the whole position: the source has
        /// to be a position that is actually <b>carrying</b> the macro the Macro panel is showing.
        /// The panel's own footer is the only caller, so "the rail is on the Macro panel" is not
        /// re-asked here — but "there is something to copy" is, because the panel offers the button
        /// beside an empty slot too.
        /// </summary>
        private bool CanCopyMacro()
        {
            return CanCopyKey() && FindMacroToCopy() is not null;
        }

        /// <summary>
        /// Enters the armed state <em>scoped to one macro</em>. Same shape as
        /// <see cref="ArmCopyKey"/> — a listen is ended, the source is the selected key, the row
        /// takes a prompt — with the slot the Macro panel is editing recorded beside it.
        /// <para>
        /// The slot comes from the panel rather than from the key, because the panel is what the
        /// user is looking at: it is the one thing that knows whether the slot on screen was chosen
        /// explicitly or fallen back to.
        /// </para>
        /// </summary>
        private void ArmMacroCopy()
        {
            if (!CanCopyMacro())
            {
                return;
            }

            CancelRemap();

            _copyMacroSlot = _macroInspectorPanel.SelectedSlotNumber;

            CopySource = SelectedKey;
            CopyPrompt = BoardLegendViewModel.CopyMacroTargetPrompt;

            RefreshLegend();
        }

        /// <summary>Leaves the armed state, whole-key or macro; the model is untouched.</summary>
        private void CancelCopyKey()
        {
            if (_copySource is null)
            {
                return;
            }

            _copyMacroSlot = null;

            CopySource = null;
            CopyPrompt = string.Empty;

            RefreshLegend();
        }

        /// <summary>
        /// The armed copy's target has been clicked. <see cref="_copyMacroSlot"/> decides which copy
        /// this is — one macro (<see cref="CompleteCopyMacro"/>) or the whole position — because
        /// there is one armed state for both and the scope is what tells them apart.
        /// <para>
        /// The whole-position copy has three outcomes, and the refusal is the reason this is not
        /// simply <see cref="KeyboardKey.CopyFrom"/> at the call site:
        /// </para>
        /// <list type="bullet">
        /// <item>The source itself — copying a position onto itself writes nothing, so the click is
        /// read as "never mind" and the copy is disarmed.</item>
        /// <item>A position that can never be remapped (05 §5.3) — <c>CopyFrom</c> would silently
        /// drop the key-data half and still copy the macros, which is exactly the half-copy this
        /// refuses. The row says why and <b>stays armed</b>: the refusal is about that one cap.</item>
        /// <item>Anything else — the copy lands, and the copy is disarmed.</item>
        /// </list>
        /// </summary>
        private void CompleteCopyKey(KeyboardKeyViewModel target)
        {
            var source = _copySource;

            if (source is null)
            {
                return;
            }

            if (_copyMacroSlot is int slot)
            {
                CompleteCopyMacro(source, slot, target);

                return;
            }

            if (ReferenceEquals(source, target))
            {
                CancelCopyKey();

                return;
            }

            if (!target.CanEdit)
            {
                CopyPrompt = BoardLegendViewModel.CopyTargetLockedPrompt;

                RefreshLegend();

                return;
            }

            target.Key.CopyFrom(source.Key, CopyScopeFor(target));

            CopySource = null;
            CopyPrompt = string.Empty;

            // Invariant 3: Core announces nothing, so the cap that just changed is re-read by hand.
            target.RefreshFromModel();

            // Invariant 16: the counters — the copy's macro half leaves an independent copy on a
            // second key (06 §1), so the profile really is carrying one macro more — the advisories,
            // the legend and the dirty flag all follow.
            RefreshCounters();
        }

        /// <summary>
        /// The armed <b>macro</b> copy's target has been clicked. One clone lands in the target's
        /// first empty slot and the source stays exactly where it was (06 §1: every slot holds its
        /// own copy, so this is the only thing "putting a macro on a second key" can honestly mean).
        /// <list type="bullet">
        /// <item><b>The source itself is a legitimate target.</b> Unlike a whole-key self-copy —
        /// which writes nothing and so reads as "never mind" — duplicating a macro into the key's
        /// own next free slot writes something real, and it is the one surviving replacement for the
        /// deleted library's <c>Duplicate</c> (issue #141).</item>
        /// <item>A position that cannot carry a macro (05 §5.3, 06 §2.2), a key whose slots are all
        /// taken, and a profile already at its firmware macro count (09 §2) each refuse with their
        /// own sentence and <b>stay armed</b>: the user is still picking, and the next cap may well
        /// accept.</item>
        /// <item>The macro itself having gone — deleted from the panel while the pick was armed —
        /// disarms, because there is nothing left to place.</item>
        /// </list>
        /// </summary>
        private void CompleteCopyMacro(KeyboardKeyViewModel source, int slot, KeyboardKeyViewModel target)
        {
            if (Layout is not { } layout || FindMacroAt(source, slot) is not { } macro)
            {
                CancelCopyKey();

                return;
            }

            if (!target.CanAssignMacro)
            {
                CopyPrompt = BoardLegendViewModel.CopyMacroTargetLockedPrompt;

                RefreshLegend();

                return;
            }

            // Invariant 11's third input-time refusal, restated for this path: a copy that pushed the
            // profile past 06 §6's count would leave Validate() stopping every save of it, which is
            // the same "quietly unsavable" outcome CopyScopeFor exists to prevent. The figure is the
            // firmware's, never a literal (MacroLimits).
            if (MacroLimits.ResolveMaxMacroCount(Device) is int limit && layout.MacroCount + 1 > limit)
            {
                CopyPrompt = MacroInspectorPanelViewModel.BuildMacroCountLimitMessage(limit);

                RefreshLegend();

                return;
            }

            // Core answers every remaining refusal by returning null rather than throwing: a full
            // key, and a target that is not in this layout at all.
            if (MacroPlacement.CopyTo(layout, macro, target.Key) is null)
            {
                CopyPrompt = BoardLegendViewModel.CopyMacroTargetFullPrompt;

                RefreshLegend();

                return;
            }

            _copyMacroSlot = null;

            CopySource = null;
            CopyPrompt = string.Empty;

            // Invariant 3, then invariant 16 — exactly as the whole-key copy above: the cap that
            // grew a macro dot is re-read by hand, and the funnel carries the new macro to the
            // counters, the advisories, the rail and the dirty flag.
            target.RefreshFromModel();

            RefreshCounters();
        }

        /// <summary>
        /// The macro <c>Copy macro to…</c> would carry: the one in the slot the rail's Macro panel
        /// is editing, on the selected position. Null when there is none — an empty slot, a position
        /// that refuses macros, nothing selected, or no rail yet (this runs from a
        /// <c>CanExecute</c>, which the panel's own constructor may reach before the field holding
        /// it has been assigned).
        /// </summary>
        private Macro? FindMacroToCopy()
        {
            if (_macroInspectorPanel is not { } panel || SelectedKey is not { } key)
            {
                return null;
            }

            return FindMacroAt(key, panel.SelectedSlotNumber);
        }

        /// <summary>
        /// The macro at one place, read off the model. Both macro stores answer it (06 §1): a slot
        /// on the slot families, and the layer-plus-trigger entry on a flat-list board — where
        /// <see cref="MacroSites.FlatListSlot"/> is the slot number and the key holds no macros of
        /// its own at all.
        /// <para>
        /// The layer is the shown one, which is sound for an armed copy: a layer switch disarms it
        /// (<see cref="SelectLayer"/>), so an armed pick and its source are never on different
        /// layers.
        /// </para>
        /// </summary>
        private Macro? FindMacroAt(KeyboardKeyViewModel key, int slot)
        {
            if (Layout is not { } layout)
            {
                return null;
            }

            if (layout.UsesFlatMacroList)
            {
                if (slot != MacroSites.FlatListSlot)
                {
                    return null;
                }

                var flat = layout.FindMacros(SelectedLayer?.Index ?? Macro.UnassignedIndex, key.Key.TriggerKey.Code);

                return flat.Count > 0 ? flat[0] : null;
            }

            if (slot < Macro.MinMacroIndex || slot > Macro.MaxMacroIndex || !key.Key.UsesMacroSlots)
            {
                return null;
            }

            return key.Key.GetMacro(slot);
        }

        /// <summary>
        /// The widest scope that is <b>correct</b> for this target. <see cref="KeyCopyScopes.All"/>
        /// wherever the target may carry a macro; <see cref="KeyCopyScopes.KeyData"/> where it may
        /// not (05 §5.3 — a modifier position).
        /// <para>
        /// It is not unconditionally <c>All</c>, because <see cref="KeyboardKey.CopyFrom"/>
        /// deliberately copies macros onto a restricted position anyway: that leaves
        /// <see cref="KeyboardLayout.Validate"/> reporting
        /// <see cref="ModelViolationKind.MacroOnRestrictedKey"/>, which is <em>not</em> one of the
        /// four budget breaches <see cref="Advisories.EditorAdvisories"/> reports — it is a
        /// violation that stops the save outright (04 §5.3). A copy that quietly made the profile
        /// unsavable would be worse than one that carried a little less.
        /// </para>
        /// </summary>
        private static KeyCopyScopes CopyScopeFor(KeyboardKeyViewModel target)
        {
            return target.CanAssignMacro ? KeyCopyScopes.All : KeyCopyScopes.KeyData;
        }

        /// <summary>
        /// Re-reads the shown layer and hands the legend row its five counts.
        /// <para>
        /// The whole layer is re-read, not just the cap that changed: the counts are derived from
        /// the caps (<see cref="KeyboardLayerViewModel.RefreshCounts"/>), a single-key edit refreshes
        /// only the cap it touched (invariant 3), and a macro assigned from the macro panel refreshes
        /// no cap at all — so without this the row would count yesterday's board. It is the shown
        /// layer only, because the row is layer-scoped.
        /// </para>
        /// </summary>
        private void RefreshLegend()
        {
            SelectedLayer?.RefreshFromModel();

            BoardLegend.Refresh(SelectedLayer, _copyPrompt);

            // The key inspector is pushed here too. This method is the tail of RefreshCounters,
            // which every path that can write the layout already ends in, so hanging the rail off it
            // makes "every mutation reaches the rail" true by construction rather than by a list
            // somebody has to remember to maintain (KeyboardEditorViewModel.Inspector.cs).
            RefreshInspector();
        }
    }
}
