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
        /// Hosted by the Layout/Macros board only — the Lighting tab draws no state badges
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
        /// Arms a copy from the selected key (mockup 1e's <c>Copy key…</c>). Deliberately distinct
        /// from the inspector's own <c>Copy to…</c> of
        /// <a href="https://github.com/migus88/kinesis-edit/issues/92">#92</a>, which the handoff
        /// places in the inspector footer.
        /// </summary>
        public IRelayCommand CopyKeyCommand { get; }

        /// <summary>Disarms it without writing anything; the view's Escape route runs it.</summary>
        public IRelayCommand CancelCopyKeyCommand { get; }

        private KeyboardKeyViewModel? _copySource;
        private string _copyPrompt = string.Empty;

        /// <summary>
        /// §1.3's precondition, restated: a copy needs a source that has something to copy and a
        /// board that may be edited. The <c>!IsLoading &amp;&amp; !IsBusy</c> pair is the same guard
        /// every other model-writing command carries — a save serializes the model on a background
        /// thread — and an open feature panel owns the surface outright, so the board underneath it
        /// cannot be clicked to finish the pick.
        /// </summary>
        private bool CanCopyKey()
        {
            return SelectedKey is { CanEdit: true }
                   && !IsLoading
                   && !IsBusy
                   && ActiveOverlay is null;
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

            CopySource = SelectedKey;
            CopyPrompt = BoardLegendViewModel.CopyTargetPrompt;

            RefreshLegend();
        }

        /// <summary>Leaves the armed state; the model is untouched.</summary>
        private void CancelCopyKey()
        {
            if (_copySource is null)
            {
                return;
            }

            CopySource = null;
            CopyPrompt = string.Empty;

            RefreshLegend();
        }

        /// <summary>
        /// The armed copy's target has been clicked. Three outcomes, and the refusal is the reason
        /// this is not simply <see cref="KeyboardKey.CopyFrom"/> at the call site:
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

            // The macro half of the copy adds rows to the profile's macro list, exactly as a reset
            // removes them, so the panel is sitting on a list that has moved.
            _macroPanel?.RefreshFromModel();

            // Invariant 16: the counters, the advisories, the legend and the dirty flag all follow.
            RefreshCounters();
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
        }
    }
}
