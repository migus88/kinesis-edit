using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The step editor of the key inspector's Macro panel: the rows, the selection every edit is
    /// aimed at, the reorder, the per-row delete, the <c>＋</c> placeholder, and <b>every write to
    /// the macro's keystroke list</b>.
    ///
    /// <para><b>The rows carry no numbers since issue #146.</b> Mockup <c>2i</c> drew them
    /// <c>01</c>…<c>07</c>; the designer's mock draws a token chip and an action word, and the count
    /// once, in the header (<see cref="CountText"/>). A per-row ordinal in a list that is reordered
    /// by dragging is a label that is right only until the gesture it invites — and it cost a column
    /// of a rail that had to hold a chip, an action, a delay and a delete.</para>
    ///
    /// <para><b>It is the model gateway, and the composer is the surface</b> (issue #139). The four
    /// things a step has — its key, its held modifiers, its direction and the delay behind it — are
    /// edited in <c>MacroInspectorPanelViewModel.Composer.cs</c>, which owns no macro and reaches
    /// this class through the <c>TrySet…</c> methods below. That split exists because
    /// <see cref="MacroInspectorStepViewModel"/> is a read-only projection that keeps no
    /// <see cref="Keystroke"/> reference at all: the composer <em>cannot</em> write through the row
    /// it is pointed at, so it must write through the object that owns <c>_macro</c>.</para>
    ///
    /// <para><b>§11.3's rules survive; both its window and its in-place editor are gone.</b> The
    /// tokens are still Core's (<see cref="MacroDelayTokens"/>: <c>dran</c>, <c>d001</c>..<c>d999</c>,
    /// always zero-padded to three digits), resolution still goes <b>through the token and never
    /// through the code</b> (<c>dran</c> and the generated <c>d002</c> share code 10087), the range
    /// is still 1-999 validated on the way in, and the feature is still gated on
    /// <see cref="FirmwareFeature.CustomMacroDelays"/> with 09 §2's own refusal wording answered
    /// <em>in place</em>. What changed with #139 is that there is no longer a per-row
    /// <c>delay…</c> affordance opening a third surface with its own <c>Set delay</c>: the delay is
    /// one section of the one composer, and it writes the moment it is touched.</para>
    ///
    /// <para><b>A row is a step plus the delay behind it</b> — see
    /// <see cref="MacroInspectorStepViewModel"/> for why. <b>Every write therefore rebuilds the whole
    /// keystroke list from the rows</b> (<see cref="ReadBlocks"/> → <c>StepBlock</c> →
    /// <see cref="WriteBlocks"/>) rather than patching an index: a reorder that moved a step without
    /// its delay would silently retime the macro, and the same reconstruction is what makes a key
    /// change, a modifier change and a delay change all land on the right pair of keystrokes. There
    /// is deliberately <b>no second write path</b>.</para>
    ///
    /// <para><b>The list is deliberately not a <c>SelectingItemsControl</c>.</b> The editor's
    /// keyboard grammar leaves the arrow keys alone whenever a <c>SelectingItemsControl</c>,
    /// <c>RangeBase</c> or text input is in the focused element's ancestry
    /// (<c>KeyboardEditorView.BoardOwnsArrows</c>), so a step list built as a <c>ListBox</c> would
    /// swallow its own <c>⌥↑↓</c>. The rows are plain buttons and the selection is
    /// <see cref="SelectedStep"/>, this class's own — which is also what the fixed-height
    /// <c>ScrollViewer</c> around them must not change.</para>
    ///
    /// <para><b>It writes; it never refreshes the editor.</b> Every mutation ends in
    /// <see cref="Changed"/> and the panel above forwards that to the editor's one refresh funnel.
    /// Nothing here re-reads counters, advisories or the library.</para>
    /// </summary>
    public sealed class MacroInspectorStepsViewModel : ViewModelBase
    {
        /// <summary>
        /// The section heading. Mockup <c>2i</c> wrote it <c>Steps</c>; issue #146's mock writes
        /// <c>Sequence</c>, which is also what the header's record button records — a sequence, not
        /// a step — and what the composer beneath it is deliberately <em>not</em> called.
        /// </summary>
        public const string SectionTitle = "Sequence";

        /// <summary>What <see cref="CountText"/> reads on a macro with nothing in it.</summary>
        public const string StepCountZero = "no steps";

        /// <summary>What it reads on a macro of one — spelled out rather than <c>1 steps</c>.</summary>
        public const string StepCountOne = "1 step";

        /// <summary>What it reads on every other count; <c>{0}</c> is the number of rows.</summary>
        public const string StepCountFormat = "{0} steps";

        /// <summary>The trailing row's caption, verbatim from mockup <c>2i</c> (the <c>＋</c> is geometry).</summary>
        public const string InsertStepCaption = "insert step";

        /// <summary>What the per-row <c>×</c> is for, as a tooltip. This app's wording.</summary>
        public const string RemoveStepHint = "Remove this step";

        /// <summary>
        /// What the drag handle is for, as a tooltip. This app's wording. The modifier is spelled
        /// in <b>words</b>, not as <c>⌥</c>: a tooltip is a bare string with nowhere to hang a
        /// drawn mark, and U+2325 is in neither embedded family.
        /// <para>
        /// <b>It is the only place the reorder shortcut is written down now</b> (issue #146). The
        /// header's <c>drag ⠿ · ⌥↑↓</c> block went with the mock, which puts the step count there
        /// instead — so this tooltip has to keep naming the accelerator, or the keyboard half of the
        /// reorder becomes undiscoverable.
        /// </para>
        /// </summary>
        public const string MacReorderHandleHint = "Drag to reorder, or press Option+↑ / Option+↓";

        /// <summary>The same tooltip everywhere else.</summary>
        public const string PlainReorderHandleHint = "Drag to reorder, or press Alt+↑ / Alt+↓";

        /// <summary>§11.3's random-delay caption, verbatim.</summary>
        public const string RandomDelayCaption = "Random Delay (1-150ms)";

        /// <summary>§11.3's custom-delay caption, verbatim.</summary>
        public const string CustomDelayCaption = "Custom Delay (1-999ms)";

        /// <summary>
        /// §11.3's validation message, verbatim. It stays here, beside the gate and the tokens it
        /// belongs with, even though the field that can now provoke it is the composer's: §11.3 is
        /// this class's contract, and splitting its three strings across two files is how one of
        /// them drifts.
        /// </summary>
        public const string InvalidDelayMessage =
            "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.";

        /// <summary>
        /// 09 §2's refusal for custom and random delays, used where the gate row carries no message
        /// of its own. Only the Freestyle Edge/Pro gate this feature and their rows do carry it;
        /// pinned identical to them by test.
        /// </summary>
        public const string FirmwareRefusalMessage =
            "To utilize custom or random delays, please download and install the latest firmware.";

        /// <summary>
        /// The drag handle's tooltip as this platform spells the modifier. Static because the
        /// handle sits on a <see cref="MacroInspectorStepViewModel"/> row, which has no path back
        /// to this object — the view reaches it with <c>x:Static</c>.
        /// </summary>
        public static string ReorderHandleHint { get; } =
            KeyCaption.IsMacOs ? MacReorderHandleHint : PlainReorderHandleHint;

        /// <summary>
        /// How many rows the list holds, as the Sequence header states it beside its title —
        /// <c>no steps</c>, <c>1 step</c>, <c>5 steps</c>. It counts <see cref="Items"/>, so an open
        /// <c>＋</c> placeholder is included: the number labels what the user is looking at, and a
        /// header reading <c>4 steps</c> over five visible rows would be counting something else.
        /// </summary>
        public string CountText => BuildCountText(_items.Count);

        /// <summary>
        /// The rows, in playback order — the macro's steps, plus the <c>＋</c> placeholder when one
        /// is open. Rebuilt whole on every write.
        /// </summary>
        public IReadOnlyList<MacroInspectorStepViewModel> Items
        {
            get => _items;
            private set
            {
                if (SetProperty(ref _items, value))
                {
                    OnPropertyChanged(nameof(HasItems));
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(CountText));
                }
            }
        }

        /// <summary>Whether there is anything at all in the list.</summary>
        public bool HasItems => _items.Count > 0;

        /// <summary>How many rows there are, the placeholder included.</summary>
        public int Count => _items.Count;

        /// <summary>
        /// The row every composer control is aimed at, and the row <c>⌥↑↓</c> moves. It is this
        /// class's own selection, deliberately not a <c>ListBox</c>'s — see the type's remarks.
        /// </summary>
        public MacroInspectorStepViewModel? SelectedStep
        {
            get => _selectedStep;
            private set
            {
                if (ReferenceEquals(_selectedStep, value))
                {
                    return;
                }

                if (_selectedStep is not null)
                {
                    _selectedStep.IsSelected = false;
                }

                _selectedStep = value;

                if (_selectedStep is not null)
                {
                    _selectedStep.IsSelected = true;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));

                MoveStepUpCommand.NotifyCanExecuteChanged();
                MoveStepDownCommand.NotifyCanExecuteChanged();

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Whether anything is selected. It is what the composer's "everything is disabled with no
        /// selection" rule reads (issue #139) — the one state in which only the Sequence header's
        /// <c>Record</c> and <c>＋</c> are live, because those two are how a selection comes to exist.
        /// </summary>
        public bool HasSelection => _selectedStep is not null;

        /// <summary>
        /// Whether a <c>＋</c> placeholder is open. It is a row in <see cref="Items"/> and
        /// <b>nothing in <c>Macro.Keystrokes</c></b> until a key lands on it — see
        /// <see cref="MacroInspectorStepViewModel.CreatePlaceholder"/>.
        /// </summary>
        public bool HasPlaceholder => _placeholderIndex >= 0;

        /// <summary>Lowest custom delay §11.3 accepts (1 ms).</summary>
        public int MinimumDelayMilliseconds => MacroDelayTokens.MinDelayMilliseconds;

        /// <summary>Highest custom delay §11.3 accepts (999 ms).</summary>
        public int MaximumDelayMilliseconds => MacroDelayTokens.MaxDelayMilliseconds;

        /// <summary>
        /// Whether this device's firmware clears 09 §2's custom/random delay gate. False draws the
        /// refusal in place of the composer's delay controls — the sanctioned exception to "absent
        /// features are not shown, not disabled" — rather than hiding the affordance.
        /// </summary>
        public bool AreDelaysAvailable { get; }

        /// <summary>09 §2's own words for the refusal, or an empty string while the gate passes.</summary>
        public string DelayUnavailableReason { get; }

        /// <summary>Whether the refusal has a firmware page to send the user to.</summary>
        public bool CanUpdateFirmware => !AreDelaysAvailable && _supportUrl is not null;

        /// <summary>Caption of that action, shared with the gate's own dialog so the two read alike.</summary>
        public string UpdateFirmwareCaption => FirmwareFeatureGate.UpdateFirmwareButtonCaption;

        /// <summary>Points <see cref="SelectedStep"/> at a row; the pointer runs it.</summary>
        public IRelayCommand<MacroInspectorStepViewModel> SelectStepCommand { get; }

        /// <summary>Drops one row and everything it occupies — the row's <c>×</c>.</summary>
        public IRelayCommand<MacroInspectorStepViewModel> RemoveStepCommand { get; }

        /// <summary><c>⌥↑</c>: moves the selected row one place earlier.</summary>
        public IRelayCommand MoveStepUpCommand { get; }

        /// <summary><c>⌥↓</c>: moves the selected row one place later.</summary>
        public IRelayCommand MoveStepDownCommand { get; }

        /// <summary>Opens the device's firmware support page; live only while the gate refuses.</summary>
        public IRelayCommand UpdateFirmwareCommand { get; }

        /// <summary>Raised after any write to the macro, so the panel can run the editor's funnel.</summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Raised whenever <see cref="SelectedStep"/> moves, including to null. The composer above
        /// re-seeds itself from it — and normalizes the newly selected step, which is the one write
        /// a mere click is entitled to make (issue #139).
        /// </summary>
        public event EventHandler? SelectionChanged;

        private readonly TokenDialect _dialect;
        private readonly IUrlLauncher _urlLauncher;
        private readonly string? _supportUrl;
        private IReadOnlyList<MacroInspectorStepViewModel> _items = [];
        private MacroInspectorStepViewModel? _selectedStep;
        private Macro? _macro;

        /// <summary>
        /// Which <b>block</b> the <c>＋</c> placeholder occupies — 0..block count, or -1 for none.
        /// It is a block index rather than a row index because it is also the insertion point the
        /// materialized step takes, and rows and blocks diverge by exactly this row while it is open.
        /// </summary>
        private int _placeholderIndex = -1;

        /// <summary>
        /// How many real rows the macro produced at the last rebuild — <see cref="Items"/> minus the
        /// placeholder. It is what every block index is bounded against; <see cref="CountText"/>
        /// deliberately counts the <em>rows</em> instead, because that is what is on screen.
        /// </summary>
        private int _blockCount;

        /// <summary>
        /// Builds the step editor for one open device. The firmware gate is resolved <b>once</b>,
        /// from the snapshot the shell already carries — it cannot move while a session is open —
        /// and the dialect is a device fact too (<c>KeyboardLayout.DialectFor</c>), which is what
        /// lets this be constructed before any profile has been read.
        /// </summary>
        public MacroInspectorStepsViewModel(DeviceId deviceId, FirmwareState firmware, IUrlLauncher urlLauncher)
        {
            _dialect = KeyboardLayout.DialectFor(deviceId);
            _urlLauncher = urlLauncher ?? throw new ArgumentNullException(nameof(urlLauncher));
            _supportUrl = FirmwareSupportUrls.FindUrl(deviceId);

            AreDelaysAvailable = FirmwareGateService.IsAvailable(deviceId, FirmwareFeature.CustomMacroDelays, firmware);
            DelayUnavailableReason = AreDelaysAvailable
                ? string.Empty
                : FirmwareGateCatalog.Find(deviceId, FirmwareFeature.CustomMacroDelays)?.Message ?? FirmwareRefusalMessage;

            SelectStepCommand = new RelayCommand<MacroInspectorStepViewModel>(Select);
            RemoveStepCommand = new RelayCommand<MacroInspectorStepViewModel>(Remove);
            MoveStepUpCommand = new RelayCommand(() => MoveSelected(-1), () => CanMoveSelected(-1));
            MoveStepDownCommand = new RelayCommand(() => MoveSelected(1), () => CanMoveSelected(1));
            UpdateFirmwareCommand = new RelayCommand(OpenSupportPage, () => CanUpdateFirmware);
        }

        /// <summary>
        /// Shows <paramref name="macro"/>'s steps; null empties the list. Called from the panel's
        /// <c>Refresh</c>, so it <b>never writes</b> — a step list that wrote from a refresh would
        /// write on every counter refresh, and forever.
        ///
        /// <para><b>A different macro drops the selection and any open placeholder.</b> Both are
        /// facts about the macro under edit: a placeholder carried across a slot change would point
        /// at an insertion index in a list it was never measured against. The <em>same</em> macro
        /// arriving again is the ordinary refresh and keeps both, which is what lets a placeholder
        /// survive the counter refresh that follows every unrelated write.</para>
        /// </summary>
        public void Load(Macro? macro)
        {
            var isNewMacro = !ReferenceEquals(macro, _macro);

            _macro = macro;

            if (isNewMacro)
            {
                _placeholderIndex = -1;

                SelectedStep = null;
            }

            Rebuild();
        }

        /// <summary>
        /// Takes over a macro that was just created <b>for</b> the open placeholder, keeping it.
        /// It is <see cref="Load"/> minus the drop, and it exists because the panel's
        /// <c>EnsureMacro</c> runs in the middle of materializing that placeholder: the macro
        /// identity really does change at that moment, and answering it by discarding the row the
        /// new macro was created for would throw the insertion point away between two halves of one
        /// action.
        /// </summary>
        public void Adopt(Macro macro)
        {
            ArgumentNullException.ThrowIfNull(macro);

            _macro = macro;

            Rebuild();
        }

        /// <summary>Re-reads the macro after something outside this list wrote to it.</summary>
        public void RefreshFromModel()
        {
            Rebuild();
        }

        /// <summary>
        /// Opens the <c>＋</c> placeholder after the selected row — at the end when nothing is
        /// selected — and selects it. Nothing is written: the row exists only in <see cref="Items"/>
        /// until <see cref="TrySetSelectedKey"/> lands a key on it. A placeholder that is already
        /// open is simply re-selected rather than a second one being made.
        /// </summary>
        public void InsertPlaceholder()
        {
            if (_placeholderIndex >= 0)
            {
                SelectPlaceholder();

                return;
            }

            var after = BlockIndexOf(_selectedStep);

            _placeholderIndex = after >= 0 ? after + 1 : _blockCount;

            Rebuild();

            SelectPlaceholder();
        }

        /// <summary>
        /// Drops an open placeholder without writing anything, and reports whether there was one.
        /// An abandoned placeholder is discarded the moment the composer stops being about it —
        /// another row selected, another key, another slot, a revert, or the panel deactivating.
        /// </summary>
        public bool DiscardPlaceholder()
        {
            if (_placeholderIndex < 0)
            {
                return false;
            }

            _placeholderIndex = -1;

            if (_selectedStep?.IsPlaceholder == true)
            {
                SelectedStep = null;
            }

            Rebuild();

            return true;
        }

        /// <summary>
        /// Writes <paramref name="key"/> onto the selected step — or turns the open placeholder into
        /// a real step carrying it, inserted at exactly the position it was drawn at. The one path
        /// by which the composer's <c>Record</c> lands a key. False when there is nothing to write
        /// to, or when the selected row is a bare delay (which has no key to change).
        /// </summary>
        public bool TrySetSelectedKey(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (_macro is null || _selectedStep is not { } step)
            {
                return false;
            }

            if (step.IsPlaceholder)
            {
                return TryMaterializePlaceholder(key);
            }

            if (!TryReadSelectedBlock(out var blocks, out var index))
            {
                return false;
            }

            var current = blocks[index].Keystroke;

            // A fresh keystroke rather than a mutated one: WriteDownUp is a fact about the KEY
            // (05 §1.1, §3.12), so carrying the old key's value over would be wrong the moment the
            // two keys disagree. The direction and the pedal marker are facts about the step and do
            // carry over.
            var replacement = new Keystroke(key, current.Modifiers)
            {
                DiffPressRelease = current.DiffPressRelease
            };

            replacement.UpDown = replacement.Modifiers == MacroModifiers.None ? current.UpDown : KeyDirection.None;

            blocks[index] = blocks[index] with { Keystroke = replacement };

            return Commit(blocks, index);
        }

        /// <summary>
        /// Writes the held-modifier set of the selected step (05 §5.1) — what the composer's four
        /// latches produce.
        ///
        /// <para><b>The chord guard of 05 §5.8, written and not merely masked.</b> A keystroke that
        /// carries modifiers can have no explicit direction: <see cref="Keystroke.EffectiveDirection"/>
        /// discards <see cref="Keystroke.UpDown"/> on the way to the file. Masking alone would leave
        /// the old direction sitting in the model, so unticking the last modifier would bring a
        /// <c>press</c> the user had visibly lost silently back. Ticking one therefore <b>clears the
        /// field</b>, and the step stays a <c>tap</c> afterwards.</para>
        ///
        /// <para>A step whose key is itself a modifier is the exception the model already enforces:
        /// <see cref="Keystroke"/> drops any modifier assigned to such a key, so the set lands as
        /// <see cref="MacroModifiers.None"/> and the direction survives — which is why the composer
        /// draws the four latches <em>disabled</em> there rather than as controls that do nothing.</para>
        /// </summary>
        public bool TrySetSelectedModifiers(MacroModifiers modifiers)
        {
            if (!TryReadSelectedBlock(out var blocks, out var index))
            {
                return false;
            }

            var current = blocks[index].Keystroke;
            var replacement = new Keystroke(current.Key, modifiers)
            {
                WriteDownUp = current.WriteDownUp,
                DiffPressRelease = current.DiffPressRelease
            };

            replacement.UpDown = replacement.Modifiers == MacroModifiers.None ? current.UpDown : KeyDirection.None;

            if (replacement.IsEquivalentTo(current))
            {
                return false;
            }

            blocks[index] = blocks[index] with { Keystroke = replacement };

            return Commit(blocks, index);
        }

        /// <summary>
        /// Writes the selected step's direction — <c>tap</c>, <c>press</c> or <c>release</c>
        /// (06 §2.2). <b>Refused on a chord</b>: 05 §5.8 would discard the direction on the way to
        /// the file, so writing one would be promising a step the firmware never plays. The composer
        /// disables both segments in that state for the same reason.
        /// </summary>
        public bool TrySetSelectedDirection(KeyDirection direction)
        {
            if (!TryReadSelectedBlock(out var blocks, out var index))
            {
                return false;
            }

            var current = blocks[index].Keystroke;

            if (current.Modifiers != MacroModifiers.None || current.UpDown == direction)
            {
                return false;
            }

            var replacement = new Keystroke(current.Key, current.Modifiers)
            {
                UpDown = direction,
                WriteDownUp = current.WriteDownUp,
                DiffPressRelease = current.DiffPressRelease
            };

            blocks[index] = blocks[index] with { Keystroke = replacement };

            return Commit(blocks, index);
        }

        /// <summary>
        /// Writes the delay behind the selected step, or takes it off again
        /// (<see cref="MacroInspectorDelay.None"/>). §11.3's rules in full: the key is resolved
        /// <b>through the token</b> and never through the code — <c>dran</c> and the generated
        /// <c>d002</c> share code 10087, so a code lookup would silently turn a 2 ms delay into a
        /// random one (05 §7) — the range is 1-999, and a firmware that fails 09 §2's gate writes
        /// nothing at all.
        ///
        /// <para>Clearing a <b>delay-only</b> row drops the row, because a row that was nothing but
        /// a delay has nothing left once it goes.</para>
        /// </summary>
        public bool TrySetSelectedDelay(MacroInspectorDelay delay)
        {
            if (!AreDelaysAvailable || _macro is null || _selectedStep is not { } step)
            {
                return false;
            }

            var index = BlockIndexOf(step);

            if (index < 0)
            {
                return false;
            }

            var blocks = ReadBlocks();

            if (!delay.IsPresent)
            {
                if (blocks[index].IsDelayOnly)
                {
                    blocks.RemoveAt(index);

                    WriteBlocks(blocks);
                    SelectByBlockPosition(Math.Min(index, _blockCount - 1));
                    Announce();

                    return true;
                }

                if (blocks[index].Delay is null)
                {
                    return false;
                }

                blocks[index] = blocks[index] with { Delay = null };

                return Commit(blocks, index);
            }

            if (ResolveDelay(delay) is not { } resolved)
            {
                return false;
            }

            blocks[index] = blocks[index].IsDelayOnly
                ? new StepBlock(new Keystroke(resolved), null, IsDelayOnly: true)
                : blocks[index] with { Delay = new Keystroke(resolved) };

            return Commit(blocks, index);
        }

        /// <summary>
        /// Moves the row at <paramref name="fromIndex"/> to <paramref name="toIndex"/> (both 0-based
        /// row positions) — what a drag lands on, and what <c>⌥↑↓</c> runs one step at a time.
        /// Returns whether anything moved.
        ///
        /// <para><b>Refused while a placeholder is open.</b> That row is in no keystroke list, so
        /// there is no reorder of the macro that could express moving it — and moving another row
        /// around it would silently change where the placeholder's key is about to land.</para>
        /// </summary>
        public bool MoveStep(int fromIndex, int toIndex)
        {
            if (_macro is null
                || _placeholderIndex >= 0
                || fromIndex == toIndex
                || fromIndex < 0
                || toIndex < 0
                || fromIndex >= _items.Count
                || toIndex >= _items.Count)
            {
                return false;
            }

            var blocks = ReadBlocks();
            var moved = blocks[fromIndex];

            blocks.RemoveAt(fromIndex);
            blocks.Insert(toIndex, moved);

            WriteBlocks(blocks);

            // The rows were rebuilt, so the selection has to be re-established by position.
            SelectByBlockPosition(toIndex);

            Announce();

            return true;
        }

        private void Select(MacroInspectorStepViewModel? step)
        {
            if (step is null)
            {
                return;
            }

            if (step.IsPlaceholder)
            {
                SelectedStep = step;

                return;
            }

            var block = BlockIndexOf(step);

            if (block < 0)
            {
                return;
            }

            // Selecting anything else abandons the placeholder — and rebuilds the rows, so the row
            // that was clicked is gone by the time it is selected. Its block index is not.
            if (DiscardPlaceholder())
            {
                SelectByBlockPosition(block);

                return;
            }

            SelectedStep = step;
        }

        private void Remove(MacroInspectorStepViewModel? step)
        {
            if (step is null)
            {
                return;
            }

            if (step.IsPlaceholder)
            {
                DiscardPlaceholder();

                return;
            }

            if (_macro is null)
            {
                return;
            }

            var index = BlockIndexOf(step);

            if (index < 0)
            {
                return;
            }

            var blocks = ReadBlocks();

            blocks.RemoveAt(index);

            WriteBlocks(blocks);

            SelectByBlockPosition(Math.Min(index, _blockCount - 1));

            Announce();
        }

        private bool CanMoveSelected(int offset)
        {
            if (_selectedStep is null || _selectedStep.IsPlaceholder || _placeholderIndex >= 0)
            {
                return false;
            }

            var target = BlockIndexOf(_selectedStep) + offset;

            return target >= 0 && target < _blockCount;
        }

        private void MoveSelected(int offset)
        {
            if (_selectedStep is null)
            {
                return;
            }

            var index = BlockIndexOf(_selectedStep);

            MoveStep(index, index + offset);
        }

        /// <summary>
        /// Turns the open placeholder into a real step carrying <paramref name="key"/>, inserted at
        /// the block it was drawn at — through <see cref="WriteBlocks"/> like every other write, so
        /// the step's neighbours keep their own delays.
        /// </summary>
        private bool TryMaterializePlaceholder(KeyDefinition key)
        {
            if (_macro is null || _placeholderIndex < 0)
            {
                return false;
            }

            var blocks = ReadBlocks();
            var index = Math.Clamp(_placeholderIndex, 0, blocks.Count);

            blocks.Insert(index, new StepBlock(new Keystroke(key), null, IsDelayOnly: false));

            _placeholderIndex = -1;

            SelectedStep = null;

            return Commit(blocks, index);
        }

        /// <summary>
        /// The selected row's block, ready to be rewritten — refused for no selection, the
        /// placeholder, and a delay-only row, none of which carries a key to qualify.
        /// </summary>
        private bool TryReadSelectedBlock(out List<StepBlock> blocks, out int index)
        {
            blocks = [];
            index = -1;

            if (_macro is null || _selectedStep is not { } step || step.IsDelayOnly)
            {
                return false;
            }

            index = BlockIndexOf(step);

            if (index < 0)
            {
                return false;
            }

            blocks = ReadBlocks();

            return true;
        }

        /// <summary>Writes <paramref name="blocks"/> back, keeps the selection on the row that moved, and announces.</summary>
        private bool Commit(IReadOnlyList<StepBlock> blocks, int index)
        {
            WriteBlocks(blocks);

            SelectByBlockPosition(index);

            Announce();

            return true;
        }

        private KeyDefinition? ResolveDelay(MacroInspectorDelay delay)
        {
            if (delay.IsRandom)
            {
                return MacroDelayTokens.ResolveRandom(_dialect);
            }

            if (!MacroDelayTokens.IsValidDelay(delay.Milliseconds))
            {
                return null;
            }

            return MacroDelayTokens.ResolveCustom(delay.Milliseconds, _dialect);
        }

        private void OpenSupportPage()
        {
            if (_supportUrl is not null)
            {
                _urlLauncher.Open(_supportUrl);
            }
        }

        /// <summary>
        /// The header's count in words and figures. Three shapes rather than one format, because
        /// <c>0 steps</c> reads as a count of nothing where <c>no steps</c> reads as an empty macro,
        /// and <c>1 steps</c> is simply wrong.
        /// </summary>
        private static string BuildCountText(int count)
        {
            return count switch
            {
                <= 0 => StepCountZero,
                1 => StepCountOne,
                _ => string.Format(CultureInfo.InvariantCulture, StepCountFormat, count)
            };
        }

        private void SelectByPosition(int position)
        {
            SelectedStep = position >= 1 && position <= _items.Count ? _items[position - 1] : null;
        }

        /// <summary>
        /// Selects the placeholder row itself. It is <em>not</em> a block, so it cannot go through
        /// <see cref="SelectByBlockPosition"/> — which maps a block onto the row that draws it and
        /// would land one row past the placeholder every time.
        /// </summary>
        private void SelectPlaceholder()
        {
            SelectByPosition(_placeholderIndex + 1);
        }

        /// <summary>
        /// Selects the row drawing block <paramref name="block"/>. Row indices and block indices
        /// diverge by one from the placeholder onwards while one is open, which is the whole reason
        /// callers hold a block index across a rebuild rather than a row position.
        /// </summary>
        private void SelectByBlockPosition(int block)
        {
            if (block < 0)
            {
                SelectedStep = null;

                return;
            }

            var row = _placeholderIndex >= 0 && block >= _placeholderIndex ? block + 1 : block;

            SelectByPosition(row + 1);
        }

        private void Announce()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Reads the macro's keystrokes as the rows draw them: one block per step, with the delay
        /// that follows it folded in. A delay with nothing in front of it is a block of its own.
        /// </summary>
        private List<StepBlock> ReadBlocks()
        {
            var blocks = new List<StepBlock>();

            if (_macro is null)
            {
                return blocks;
            }

            for (var index = 0; index < _macro.Keystrokes.Count; index++)
            {
                var keystroke = _macro.Keystrokes[index];

                if (TryReadDelay(keystroke.Key, out _, out _))
                {
                    blocks.Add(new StepBlock(keystroke, null, IsDelayOnly: true));

                    continue;
                }

                Keystroke? delay = null;

                if (index + 1 < _macro.Keystrokes.Count
                    && TryReadDelay(_macro.Keystrokes[index + 1].Key, out _, out _))
                {
                    delay = _macro.Keystrokes[index + 1];
                    index++;
                }

                blocks.Add(new StepBlock(keystroke, delay, IsDelayOnly: false));
            }

            return blocks;
        }

        /// <summary>
        /// Rewrites the macro's whole keystroke list from <paramref name="blocks"/> and rebuilds the
        /// rows. Whole-list rather than an index patch on purpose: a step and its delay move
        /// together, and reconstructing the stream is the only way that stays true for a reorder, a
        /// delete, a key change, a modifier change and a delay change alike.
        /// </summary>
        private void WriteBlocks(IReadOnlyList<StepBlock> blocks)
        {
            if (_macro is null)
            {
                return;
            }

            _macro.ClearKeystrokes();

            foreach (var block in blocks)
            {
                _macro.AddKeystroke(block.Keystroke);

                if (block.Delay is { } delay)
                {
                    _macro.AddKeystroke(delay);
                }
            }

            Rebuild();
        }

        private void Rebuild()
        {
            var selectedPosition = _selectedStep?.Position ?? 0;
            var blocks = ReadBlocks();

            if (_placeholderIndex > blocks.Count)
            {
                // A delete or a reload shrank the list under an open placeholder; it lands at the
                // end rather than being drawn at an index that no longer exists.
                _placeholderIndex = blocks.Count;
            }

            var rows = new List<MacroInspectorStepViewModel>(blocks.Count + 1);
            var keystrokeIndex = 0;

            for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
            {
                AppendPlaceholder(rows, blockIndex);

                var block = blocks[blockIndex];
                var delay = block.IsDelayOnly
                    ? ReadDelay(block.Keystroke.Key)
                    : block.Delay is { } following ? ReadDelay(following.Key) : MacroInspectorDelay.None;

                rows.Add(new MacroInspectorStepViewModel(
                    block.Keystroke,
                    keystrokeIndex,
                    rows.Count + 1,
                    _dialect,
                    delay,
                    block.IsDelayOnly));

                keystrokeIndex += block.Delay is null ? 1 : 2;
            }

            AppendPlaceholder(rows, blocks.Count);

            // Through the property, not the field: the rows about to be replaced are gone, and a
            // silent field write would leave the view bound to an instance that is no longer in the
            // list with no notification that it moved.
            SelectedStep = null;

            _blockCount = blocks.Count;

            Items = rows;

            OnPropertyChanged(nameof(HasPlaceholder));

            SelectByPosition(selectedPosition);
        }

        /// <summary>
        /// Draws the placeholder when <paramref name="blockIndex"/> is the block it sits before. It
        /// is rebuilt rather than carried, because its <see cref="MacroInspectorStepViewModel.Position"/>
        /// is get-only and every other row's moves with it.
        /// </summary>
        private void AppendPlaceholder(List<MacroInspectorStepViewModel> rows, int blockIndex)
        {
            if (_placeholderIndex == blockIndex)
            {
                rows.Add(MacroInspectorStepViewModel.CreatePlaceholder(rows.Count + 1));
            }
        }

        /// <summary>
        /// Where <paramref name="step"/>'s block sits in the macro, or -1 — for a row that is not in
        /// the list, and for the placeholder, which has no block at all.
        /// <see cref="IReadOnlyList{T}"/> carries no <c>IndexOf</c>, and every caller here needs the
        /// position rather than mere membership.
        /// </summary>
        private int BlockIndexOf(MacroInspectorStepViewModel? step)
        {
            if (step is null || step.IsPlaceholder)
            {
                return -1;
            }

            for (var index = 0; index < _items.Count; index++)
            {
                if (ReferenceEquals(_items[index], step))
                {
                    return _placeholderIndex >= 0 && index > _placeholderIndex ? index - 1 : index;
                }
            }

            return -1;
        }

        private MacroInspectorDelay ReadDelay(KeyDefinition key)
        {
            if (!TryReadDelay(key, out var milliseconds, out var isRandom))
            {
                return MacroInspectorDelay.None;
            }

            return isRandom ? MacroInspectorDelay.Random : MacroInspectorDelay.Custom(milliseconds);
        }

        /// <summary>
        /// Whether <paramref name="key"/> is one of §11.3's delay pseudo-keys, read <b>off the
        /// token</b> and never off the code — 05 §7 gives <c>dran</c> and the generated <c>d002</c>
        /// the same code, so a code test would call a 2 ms delay random.
        /// </summary>
        private bool TryReadDelay(KeyDefinition key, out int milliseconds, out bool isRandom)
        {
            milliseconds = 0;
            isRandom = false;

            var token = key.GetToken(_dialect);

            if (token.Length == 0)
            {
                // A key this dialect does not name still has to be recognised: the same file can
                // have been written by an older app, and Core's own naming helper falls back the
                // same way.
                token = key.GetToken(TokenDialect.Gen1);
            }

            if (string.Equals(token, MacroDelayTokens.RandomToken, StringComparison.OrdinalIgnoreCase))
            {
                isRandom = true;

                return true;
            }

            if (token.Length != 4 || (token[0] != 'd' && token[0] != 'D'))
            {
                return false;
            }

            return int.TryParse(token[1..], NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds)
                   && MacroDelayTokens.IsValidDelay(milliseconds);
        }

        /// <summary>One drawn row's worth of the macro: a step, and the delay behind it.</summary>
        private readonly record struct StepBlock(Keystroke Keystroke, Keystroke? Delay, bool IsDelayOnly);
    }
}
