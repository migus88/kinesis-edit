using CommunityToolkit.Mvvm.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The legend row under the keyboard canvas (mockups 1e/2a): the badge vocabulary restated at
    /// legend size, each mark beside a live count of the keys wearing it on the <b>shown layer</b>,
    /// plus the two actions the mockups put there — <c>Copy key…</c> and <c>Reset layer</c>.
    /// <para>
    /// It is a type of its own rather than five more properties on
    /// <see cref="KeyboardEditorViewModel"/>, which is already the largest class in the app and
    /// which docs/guides/Coding Conventions.md forbids growing into a god class. Nothing here
    /// decides anything: the counts are pushed in by <see cref="Refresh"/> from the layer that owns
    /// them, and both commands are the editor's.
    /// </para>
    /// <para>
    /// <b>Layer-scoped, and refreshed from one funnel.</b> Core announces no change, so a count
    /// could not observe an edit even if it wanted to. <c>KeyboardEditorViewModel.RefreshCounters()</c>
    /// is the funnel every layout mutation already ends in (docs/app/keyboard-editor.md,
    /// invariant 16) and it is what calls <see cref="Refresh"/>; a layer switch calls it too,
    /// because the row is about the layer on screen.
    /// </para>
    /// <para>
    /// <b>The advisory count is an aggregate, not a third appearance.</b> Invariant 21 — every
    /// advisory appears exactly twice, on its own key and in the summary strip — is about where an
    /// advisory is *reported*. This is a tally of how many positions of the layer carry one, the
    /// same number the strip's own sentence already names, and it points at nothing.
    /// </para>
    /// </summary>
    public sealed class BoardLegendViewModel : ViewModelBase
    {
        /// <summary>The row's own section label. Mono and uppercase, like every section label.</summary>
        public const string LegendLabel = "LEGEND";

        /// <summary>Caption of the remap count — the cap's accent bar (mockup 1e, "Remapped 3").</summary>
        public const string RemappedCaption = "Remapped";

        /// <summary>Caption of the macro count — the cap's macro dot ("Macro 2").</summary>
        public const string MacroCaption = "Macro";

        /// <summary>Caption of the tap-and-hold count — the cap's corner triangle ("Tap-and-hold 11").</summary>
        public const string TapAndHoldCaption = "Tap-and-hold";

        /// <summary>
        /// Caption of the advisory count — the cap's amber bar. Mockup 2a is where it joins the
        /// other four ("Advisory 3"); 1e's darker twin draws only the other four.
        /// </summary>
        public const string AdvisoryCaption = "Advisory";

        /// <summary>Caption of the locked count — the cap's hatched face ("Locked 1").</summary>
        public const string LockedCaption = "Locked";

        /// <summary>
        /// The row's first action. Sentence case and an ellipsis, as the mockups draw it: it arms a
        /// pick rather than doing something outright.
        /// </summary>
        public const string CopyKeyCaption = "Copy key…";

        /// <summary>
        /// The row's second action, and a deliberate deviation from spec 10's verbatim
        /// <c>Reset Layer</c>: the mockups draw the row's actions in sentence case.
        /// </summary>
        public const string ResetLayerCaption = "Reset layer";

        /// <summary>What the row reads while a copy is armed and waiting for its target.</summary>
        public const string CopyTargetPrompt = "Pick a target key · Esc to cancel";

        /// <summary>
        /// What it reads when the picked target is a position that can never be remapped (05 §5.3).
        /// The copy stays armed: the refusal is about that one cap, and the user is still picking.
        /// </summary>
        public const string CopyTargetLockedPrompt = "That position cannot be edited — pick another, or press Esc.";

        /// <summary>Remapped positions on the shown layer.</summary>
        public int RemappedCount
        {
            get => _remappedCount;
            private set => SetProperty(ref _remappedCount, value);
        }

        /// <summary>Positions carrying at least one macro on the shown layer.</summary>
        public int MacroCount
        {
            get => _macroCount;
            private set => SetProperty(ref _macroCount, value);
        }

        /// <summary>Positions carrying a tap-and-hold assignment on the shown layer.</summary>
        public int TapAndHoldCount
        {
            get => _tapAndHoldCount;
            private set => SetProperty(ref _tapAndHoldCount, value);
        }

        /// <summary>Positions carrying an advisory on the shown layer.</summary>
        public int AdvisoryCount
        {
            get => _advisoryCount;
            private set => SetProperty(ref _advisoryCount, value);
        }

        /// <summary>Positions that can never be remapped on the shown layer.</summary>
        public int LockedCount
        {
            get => _lockedCount;
            private set => SetProperty(ref _lockedCount, value);
        }

        /// <summary>
        /// What the row says about the copy in progress — <see cref="CopyTargetPrompt"/>, a
        /// refusal, or an empty string when no copy is armed. It is the whole of the row's state:
        /// the editor holds the armed source, and this is what that state <em>reads</em> as.
        /// </summary>
        public string CopyPrompt
        {
            get => _copyPrompt;
            private set
            {
                if (SetProperty(ref _copyPrompt, value))
                {
                    OnPropertyChanged(nameof(IsPickingCopyTarget));
                }
            }
        }

        /// <summary>Whether a copy is armed and the row is showing its prompt.</summary>
        public bool IsPickingCopyTarget => _copyPrompt.Length > 0;

        /// <summary>Arms a copy from the selected key (the editor's own command).</summary>
        public IRelayCommand CopyKeyCommand { get; }

        /// <summary>Resets every key of the shown layer (the editor's own command).</summary>
        public IRelayCommand ResetLayerCommand { get; }

        private int _remappedCount;
        private int _macroCount;
        private int _tapAndHoldCount;
        private int _advisoryCount;
        private int _lockedCount;
        private string _copyPrompt = string.Empty;

        /// <summary>Builds the row over the two commands its buttons run.</summary>
        public BoardLegendViewModel(IRelayCommand copyKeyCommand, IRelayCommand resetLayerCommand)
        {
            CopyKeyCommand = copyKeyCommand ?? throw new ArgumentNullException(nameof(copyKeyCommand));
            ResetLayerCommand = resetLayerCommand ?? throw new ArgumentNullException(nameof(resetLayerCommand));
        }

        /// <summary>
        /// Re-reads the five counts off <paramref name="layer"/> and takes the copy prompt the
        /// editor is currently showing. A null layer — nothing loaded, or a device with no picture
        /// — zeroes the row rather than hiding it, so the board and its legend appear together.
        /// <para>
        /// The counts are read from the layer's own properties, never recomputed here: the layer is
        /// what holds the caps, and two places deriving the same five numbers is two places to
        /// disagree.
        /// </para>
        /// </summary>
        public void Refresh(KeyboardLayerViewModel? layer, string copyPrompt)
        {
            RemappedCount = layer?.RemappedCount ?? 0;
            MacroCount = layer?.MacroCount ?? 0;
            TapAndHoldCount = layer?.TapAndHoldCount ?? 0;
            AdvisoryCount = layer?.AdvisoryCount ?? 0;
            LockedCount = layer?.LockedCount ?? 0;
            CopyPrompt = copyPrompt ?? string.Empty;
        }
    }
}
