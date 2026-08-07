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
    /// The step editor of the key inspector's Macro panel (mockup <c>2i</c>): the numbered rows, the
    /// reorder, the per-row delete, and <b>the per-step delay edited in place</b> — which is where
    /// specs/11-feature-dialogs.md §11.3's <c>Macro Timing Delays</c> dialog went.
    ///
    /// <para><b>§11.3's rules survive; only its window is gone.</b> The tokens are still Core's
    /// (<see cref="MacroDelayTokens"/>: <c>dran</c>, <c>d001</c>..<c>d999</c>, always zero-padded to
    /// three digits), resolution still goes <b>through the token and never through the code</b>
    /// (<c>dran</c> and the generated <c>d002</c> share code 10087), the range is still 1-999 with
    /// the arrows clamped and the typed value validated, and the feature is still gated on
    /// <see cref="FirmwareFeature.CustomMacroDelays"/> with 09 §2's own refusal wording. What
    /// changed is that the refusal is answered <em>in place</em>, the way the Tap &amp; hold panel
    /// answers its gate, rather than as a message box over a surface that is already on screen.</para>
    ///
    /// <para><b>A row is a step plus the delay behind it</b> — see
    /// <see cref="MacroInspectorStepViewModel"/> for why. Every write therefore rebuilds the whole
    /// keystroke list from the rows rather than patching an index: a reorder that moved a step
    /// without its delay would silently retime the macro.</para>
    ///
    /// <para><b>The list is deliberately not a <c>SelectingItemsControl</c>.</b> The editor's
    /// keyboard grammar leaves the arrow keys alone whenever a <c>SelectingItemsControl</c>,
    /// <c>RangeBase</c> or text input is in the focused element's ancestry
    /// (<c>KeyboardEditorView.BoardOwnsArrows</c>), so a step list built as a <c>ListBox</c> would
    /// swallow its own <c>⌥↑↓</c>. The rows are plain buttons and the selection is
    /// <see cref="SelectedStep"/>, this class's own.</para>
    ///
    /// <para><b>It writes; it never refreshes the editor.</b> Every mutation ends in
    /// <see cref="Changed"/> and the panel above forwards that to the editor's one refresh funnel.
    /// Nothing here re-reads counters, advisories or the library.</para>
    /// </summary>
    public sealed class MacroInspectorStepsViewModel : ViewModelBase
    {
        /// <summary>The section heading, verbatim from mockup <c>2i</c>.</summary>
        public const string SectionTitle = "Steps";

        /// <summary>First half of the reorder affordance, <c>drag ⠿ · ⌥↑↓</c>: the word before the mark.</summary>
        public const string ReorderHintPrefix = "drag";

        /// <summary>
        /// The keyboard half of it on macOS, <b>without the Option glyph</b>: U+2325 is in neither
        /// embedded IBM Plex family, so it is <em>drawn</em> as <c>IconOption</c> beside this text
        /// rather than typed — exactly as the layer chips do it since issue #109. The two arrows
        /// are U+2191/U+2193, which both families carry.
        /// </summary>
        public const string MacReorderShortcut = "· ↑↓";

        /// <summary>How the same gesture is spelled everywhere else — ⌥ is Alt on every platform.</summary>
        public const string PlainReorderShortcut = "· Alt+↑↓";

        /// <summary>The trailing row's caption, verbatim from mockup <c>2i</c> (the <c>＋</c> is geometry).</summary>
        public const string InsertStepCaption = "insert step";

        /// <summary>What the per-row <c>×</c> is for, as a tooltip. This app's wording.</summary>
        public const string RemoveStepHint = "Remove this step";

        /// <summary>
        /// What the drag handle is for, as a tooltip. This app's wording. The modifier is spelled
        /// in <b>words</b>, not as <c>⌥</c>: a tooltip is a bare string with nowhere to hang a
        /// drawn mark, and U+2325 is in neither embedded family.
        /// </summary>
        public const string MacReorderHandleHint = "Drag to reorder, or press Option+↑ / Option+↓";

        /// <summary>The same tooltip everywhere else.</summary>
        public const string PlainReorderHandleHint = "Drag to reorder, or press Alt+↑ / Alt+↓";

        /// <summary>The delay editor's own heading. This app's wording.</summary>
        public const string DelaySectionTitle = "DELAY AFTER THIS STEP";

        /// <summary>§11.3's random-delay caption, verbatim.</summary>
        public const string RandomDelayCaption = "Random Delay (1-150ms)";

        /// <summary>§11.3's custom-delay caption, verbatim.</summary>
        public const string CustomDelayCaption = "Custom Delay (1-999ms)";

        /// <summary>§11.3's validation message, verbatim.</summary>
        public const string InvalidDelayMessage =
            "Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.";

        /// <summary>
        /// 09 §2's refusal for custom and random delays, used where the gate row carries no message
        /// of its own. Only the Freestyle Edge/Pro gate this feature and their rows do carry it;
        /// pinned identical to them by test.
        /// </summary>
        public const string FirmwareRefusalMessage =
            "To utilize custom or random delays, please download and install the latest firmware.";

        /// <summary>Writes the delay editor's choice onto the row. This app's wording.</summary>
        public const string ApplyDelayCaption = "Set delay";

        /// <summary>Takes the delay off the row again. This app's wording.</summary>
        public const string ClearDelayCaption = "No delay";

        /// <summary>Closes the delay editor without writing. This app's wording.</summary>
        public const string CloseDelayCaption = "Done";

        /// <summary>What the delay affordance on a row without one reads. This app's wording.</summary>
        public const string AddDelayCaption = "delay…";

        /// <summary>
        /// The drag handle's tooltip as this platform spells the modifier. Static because the
        /// handle sits on a <see cref="MacroInspectorStepViewModel"/> row, which has no path back
        /// to this object — the view reaches it with <c>x:Static</c>.
        /// </summary>
        public static string ReorderHandleHint { get; } =
            KeyCaption.IsMacOs ? MacReorderHandleHint : PlainReorderHandleHint;

        /// <summary>The keyboard spelling of the reorder gesture for this platform.</summary>
        public static string BuildReorderShortcut(bool isMacOs)
        {
            return isMacOs ? MacReorderShortcut : PlainReorderShortcut;
        }

        /// <summary>
        /// The keyboard half of the reorder affordance as this platform spells it — <c>· ⌥↑↓</c> on
        /// macOS, <c>· Alt+↑↓</c> elsewhere. Fixed for the life of the panel; the accelerator it
        /// promises is kept by <c>Input/EditorShortcuts</c>, which maps ⌥ to <c>Alt</c> everywhere.
        /// </summary>
        public string ReorderShortcut { get; } = BuildReorderShortcut(KeyCaption.IsMacOs);

        /// <summary>
        /// Whether the drawn <c>⌥</c> shows beside <see cref="ReorderShortcut"/>. macOS only: every
        /// other platform reads <c>Alt+</c> in the text itself and must not draw a second modifier.
        /// </summary>
        public bool ShowsOptionMark { get; } = KeyCaption.IsMacOs;

        /// <summary>The rows, in playback order.</summary>
        public IReadOnlyList<MacroInspectorStepViewModel> Items
        {
            get => _items;
            private set
            {
                if (SetProperty(ref _items, value))
                {
                    OnPropertyChanged(nameof(HasItems));
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(NextStepNumberText));
                }
            }
        }

        /// <summary>Whether the macro under edit has any steps at all.</summary>
        public bool HasItems => _items.Count > 0;

        /// <summary>How many rows there are.</summary>
        public int Count => _items.Count;

        /// <summary>
        /// The number the next recorded step will take — what the trailing <c>＋ insert step</c> row
        /// is numbered and what the recording banner names (mockup <c>2i</c>: "Recording into step
        /// 04").
        /// </summary>
        public string NextStepNumberText => MacroInspectorStepViewModel.FormatNumber(_items.Count + 1);

        /// <summary>
        /// The row <c>⌥↑↓</c> moves and the delay editor is pointed at, or null. It is this class's
        /// own selection, deliberately not a <c>ListBox</c>'s — see the type's remarks.
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

                MoveStepUpCommand.NotifyCanExecuteChanged();
                MoveStepDownCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>The row whose delay editor is open, or null. Only ever one at a time.</summary>
        public MacroInspectorStepViewModel? DelayStep
        {
            get => _delayStep;
            private set
            {
                if (SetProperty(ref _delayStep, value))
                {
                    OnPropertyChanged(nameof(IsDelayEditorOpen));
                }
            }
        }

        /// <summary>Whether the in-place delay editor is showing.</summary>
        public bool IsDelayEditorOpen => _delayStep is not null;

        /// <summary>
        /// The custom delay being typed, in milliseconds; 0 means the field is empty. Assigning any
        /// value un-checks <see cref="IsRandomDelay"/> (§11.3: "typing in the custom field un-checks
        /// it"). Deliberately <b>unclamped</b> — §11.3 clamps the arrows and validates on accept, so
        /// an out-of-range value has to survive long enough to be reported.
        /// </summary>
        public int CustomDelayMilliseconds
        {
            get => _customDelayMilliseconds;
            set
            {
                if (SetProperty(ref _customDelayMilliseconds, value))
                {
                    IsRandomDelay = false;
                }
            }
        }

        /// <summary>Whether §11.3's random delay is chosen instead of a custom one.</summary>
        public bool IsRandomDelay
        {
            get => _isRandomDelay;
            set => SetProperty(ref _isRandomDelay, value);
        }

        /// <summary>Lowest custom delay §11.3 accepts (1 ms).</summary>
        public int MinimumDelayMilliseconds => MacroDelayTokens.MinDelayMilliseconds;

        /// <summary>Highest custom delay §11.3 accepts (999 ms).</summary>
        public int MaximumDelayMilliseconds => MacroDelayTokens.MaxDelayMilliseconds;

        /// <summary>§11.3's validation message while the choice is unusable, else an empty string.</summary>
        public string DelayError
        {
            get => _delayError;
            private set
            {
                if (SetProperty(ref _delayError, value))
                {
                    OnPropertyChanged(nameof(HasDelayError));
                }
            }
        }

        /// <summary>Whether there is a validation message to draw.</summary>
        public bool HasDelayError => _delayError.Length > 0;

        /// <summary>
        /// Whether this device's firmware clears 09 §2's custom/random delay gate. False draws the
        /// refusal in place of the editor — the sanctioned exception to "absent features are not
        /// shown, not disabled" — rather than hiding the affordance.
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

        /// <summary>Opens the in-place delay editor over one row.</summary>
        public IRelayCommand<MacroInspectorStepViewModel> EditDelayCommand { get; }

        /// <summary>Writes the chosen delay onto that row (§11.3's Ok).</summary>
        public IRelayCommand ApplyDelayCommand { get; }

        /// <summary>Takes the delay off the row.</summary>
        public IRelayCommand ClearDelayCommand { get; }

        /// <summary>Closes the editor without writing (§11.3's Cancel).</summary>
        public IRelayCommand CloseDelayCommand { get; }

        /// <summary>The custom field's Up arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand IncreaseDelayCommand { get; }

        /// <summary>The custom field's Down arrow, clamped to 1-999 (§11.3).</summary>
        public IRelayCommand DecreaseDelayCommand { get; }

        /// <summary>Opens the device's firmware support page; live only while the gate refuses.</summary>
        public IRelayCommand UpdateFirmwareCommand { get; }

        /// <summary>Raised after any write to the macro, so the panel can run the editor's funnel.</summary>
        public event EventHandler? Changed;

        private readonly TokenDialect _dialect;
        private readonly IUrlLauncher _urlLauncher;
        private readonly string? _supportUrl;
        private IReadOnlyList<MacroInspectorStepViewModel> _items = [];
        private MacroInspectorStepViewModel? _selectedStep;
        private MacroInspectorStepViewModel? _delayStep;
        private Macro? _macro;
        private string _delayError = string.Empty;
        private int _customDelayMilliseconds;
        private bool _isRandomDelay;

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
            EditDelayCommand = new RelayCommand<MacroInspectorStepViewModel>(OpenDelayEditor);
            ApplyDelayCommand = new RelayCommand(ApplyDelay, () => AreDelaysAvailable);
            ClearDelayCommand = new RelayCommand(ClearDelay);
            CloseDelayCommand = new RelayCommand(CloseDelayEditor);
            IncreaseDelayCommand = new RelayCommand(() => StepDelay(1));
            DecreaseDelayCommand = new RelayCommand(() => StepDelay(-1));
            UpdateFirmwareCommand = new RelayCommand(OpenSupportPage, () => CanUpdateFirmware);
        }

        /// <summary>
        /// Shows <paramref name="macro"/>'s steps; null empties the list. Called from the panel's
        /// <c>Refresh</c>, so it <b>never writes</b> — a step list that wrote from a refresh would
        /// write on every counter refresh, and forever.
        /// </summary>
        public void Load(Macro? macro)
        {
            var isNewMacro = !ReferenceEquals(macro, _macro);

            _macro = macro;

            if (isNewMacro)
            {
                CloseDelayEditor();
            }

            Rebuild();
        }

        /// <summary>Re-reads the macro after something outside this list wrote to it.</summary>
        public void RefreshFromModel()
        {
            Rebuild();
        }

        /// <summary>
        /// Moves the row at <paramref name="fromIndex"/> to <paramref name="toIndex"/> (both 0-based
        /// row positions) — what a drag lands on, and what <c>⌥↑↓</c> runs one step at a time.
        /// Returns whether anything moved.
        /// </summary>
        public bool MoveStep(int fromIndex, int toIndex)
        {
            if (_macro is null
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
            SelectByPosition(toIndex + 1);

            Announce();

            return true;
        }

        private void Select(MacroInspectorStepViewModel? step)
        {
            if (step is null || IndexOf(step) < 0)
            {
                return;
            }

            SelectedStep = step;
        }

        private void Remove(MacroInspectorStepViewModel? step)
        {
            if (_macro is null || step is null)
            {
                return;
            }

            var index = IndexOf(step);

            if (index < 0)
            {
                return;
            }

            var blocks = ReadBlocks();

            blocks.RemoveAt(index);

            WriteBlocks(blocks);

            CloseDelayEditor();
            SelectByPosition(Math.Min(index + 1, _items.Count));

            Announce();
        }

        private bool CanMoveSelected(int offset)
        {
            if (_selectedStep is null)
            {
                return false;
            }

            var target = IndexOf(_selectedStep) + offset;

            return target >= 0 && target < _items.Count;
        }

        private void MoveSelected(int offset)
        {
            if (_selectedStep is null)
            {
                return;
            }

            var index = IndexOf(_selectedStep);

            MoveStep(index, index + offset);
        }

        /// <summary>
        /// Opens the delay editor over <paramref name="step"/>, seeded with whatever that row
        /// already carries. A refused gate still opens it — the panel draws 09 §2's words in place
        /// of the fields, which is the polite refusal rather than a control that vanished.
        /// </summary>
        private void OpenDelayEditor(MacroInspectorStepViewModel? step)
        {
            if (step is null || IndexOf(step) < 0)
            {
                return;
            }

            SelectedStep = step;
            DelayStep = step;
            DelayError = string.Empty;

            // Seeded from the row, and the random flag last: assigning the millisecond field
            // un-checks it, so setting them the other way round would lose a random delay.
            SetProperty(ref _customDelayMilliseconds, step.DelayMilliseconds, nameof(CustomDelayMilliseconds));

            IsRandomDelay = step.IsRandomDelay;
        }

        private void CloseDelayEditor()
        {
            DelayStep = null;
            DelayError = string.Empty;

            SetProperty(ref _customDelayMilliseconds, 0, nameof(CustomDelayMilliseconds));

            IsRandomDelay = false;
        }

        /// <summary>
        /// §11.3's Ok. The choice is resolved <b>through the token</b> — never through the key code,
        /// because <c>dran</c> and the generated <c>d002</c> share code 10087 and a code lookup
        /// would silently turn a 2 ms delay into a random one (05 §7).
        /// </summary>
        private void ApplyDelay()
        {
            if (_macro is null || _delayStep is not { } step || !AreDelaysAvailable)
            {
                return;
            }

            var delay = ResolveDelay();

            if (delay is null)
            {
                DelayError = InvalidDelayMessage;

                return;
            }

            DelayError = string.Empty;

            var blocks = ReadBlocks();
            var index = IndexOf(step);

            if (index < 0)
            {
                return;
            }

            blocks[index] = blocks[index] with { Delay = new Keystroke(delay) };

            WriteBlocks(blocks);

            SelectByPosition(index + 1);

            // The row was rebuilt, so the open editor has to be re-pointed at the new instance or
            // its next write would target a row that no longer exists.
            ReopenDelayEditorAt(index);

            Announce();
        }

        private void ClearDelay()
        {
            if (_macro is null || _delayStep is not { } step)
            {
                return;
            }

            var index = IndexOf(step);

            if (index < 0)
            {
                return;
            }

            var blocks = ReadBlocks();

            // A delay-only row has nothing left once its delay goes, so clearing it drops the row.
            if (blocks[index].IsDelayOnly)
            {
                blocks.RemoveAt(index);

                WriteBlocks(blocks);
                CloseDelayEditor();
                SelectByPosition(Math.Min(index + 1, _items.Count));
                Announce();

                return;
            }

            blocks[index] = blocks[index] with { Delay = null };

            WriteBlocks(blocks);

            SelectByPosition(index + 1);
            ReopenDelayEditorAt(index);

            Announce();
        }

        private void StepDelay(int step)
        {
            CustomDelayMilliseconds = Math.Clamp(
                _customDelayMilliseconds + step,
                MacroDelayTokens.MinDelayMilliseconds,
                MacroDelayTokens.MaxDelayMilliseconds);
        }

        private KeyDefinition? ResolveDelay()
        {
            if (_isRandomDelay)
            {
                return MacroDelayTokens.ResolveRandom(_dialect);
            }

            if (!MacroDelayTokens.IsValidDelay(_customDelayMilliseconds))
            {
                return null;
            }

            return MacroDelayTokens.ResolveCustom(_customDelayMilliseconds, _dialect);
        }

        private void OpenSupportPage()
        {
            if (_supportUrl is not null)
            {
                _urlLauncher.Open(_supportUrl);
            }
        }

        private void ReopenDelayEditorAt(int index)
        {
            DelayStep = index >= 0 && index < _items.Count ? _items[index] : null;
        }

        private void SelectByPosition(int position)
        {
            SelectedStep = position >= 1 && position <= _items.Count ? _items[position - 1] : null;
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
        /// delete and a delay change alike.
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
            var delayPosition = _delayStep?.Position ?? 0;

            if (_macro is null || _macro.Keystrokes.Count == 0)
            {
                SelectedStep = null;
                DelayStep = null;
                Items = [];

                return;
            }

            var rows = new List<MacroInspectorStepViewModel>();
            var keystrokeIndex = 0;

            foreach (var block in ReadBlocks())
            {
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

            // Through the properties, not the fields: the rows about to be replaced are gone, and a
            // silent field write would leave the view bound to an instance that is no longer in the
            // list with no notification that it moved.
            SelectedStep = null;
            DelayStep = null;

            Items = rows;

            SelectByPosition(selectedPosition);

            DelayStep = delayPosition >= 1 && delayPosition <= rows.Count ? rows[delayPosition - 1] : null;
        }

        /// <summary>
        /// Where <paramref name="step"/> sits in <see cref="Items"/>, or -1.
        /// <see cref="IReadOnlyList{T}"/> carries no <c>IndexOf</c>, and every caller here needs the
        /// position rather than mere membership.
        /// </summary>
        private int IndexOf(MacroInspectorStepViewModel? step)
        {
            if (step is null)
            {
                return -1;
            }

            for (var index = 0; index < _items.Count; index++)
            {
                if (ReferenceEquals(_items[index], step))
                {
                    return index;
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
