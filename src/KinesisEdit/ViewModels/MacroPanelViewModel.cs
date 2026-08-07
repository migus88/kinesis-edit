using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The macro panel of specs/10-apps-and-ui.md — the in-window macro editor the RGB app opens
    /// in place and the Advantage2 app docks at the bottom: the trigger key comes from the board's
    /// selection, Record routes captured keystrokes into the macro instead of remapping, and
    /// Backspace/Clear/Copy/Paste, the co-trigger toggles, the speed and repeat sliders and the
    /// keystroke budget sit around it.
    /// <para>
    /// <b>Every limit is read from the device</b> (<see cref="MacroCapability"/>), and where the
    /// model holds more than the file keeps, the panel offers only what the file keeps: the slot
    /// strip is <see cref="MacroCapability.PersistedSlotsPerKey"/> and the co-trigger cap is
    /// <see cref="MacroCapability.PersistedCoTriggersPerMacro"/>, because a macro in slot 4 of a
    /// Freestyle Edge — or a second co-trigger on one — is silently gone the next time the file is
    /// written (06 §1, §3). The speed/repeat ranges and defaults, the per-macro and per-layout
    /// keystroke budgets and the macro count come from the same record, the last of which
    /// <see cref="FirmwareGateService"/> may raise to
    /// <see cref="MacroCapability.GatedMaxMacroCount"/> (specs/09-firmware.md §2). Nothing here
    /// hard-codes the Freestyle Edge RGB's numbers, and both macro storage models of
    /// specs/06-macros.md §1 — per-key slots and the Advantage360 flat list — are supported even
    /// though only the RGB has a board picture today.
    /// </para>
    /// <para>
    /// Breaching a budget is reported, never refused (docs/app/keyboard-model.md, invariant 1);
    /// <see cref="KeyboardLayout.Validate"/> at save time is the backstop.
    /// </para>
    /// </summary>
    public sealed class MacroPanelViewModel : ViewModelBase, IKeystrokeSink
    {
        /// <summary>
        /// specs/02-devices.md, verbatim: what the legacy apps say when a macro is aimed at a
        /// position that cannot hold one (<see cref="KeyboardKey.CanAssignMacro"/>, 05 §5.3 —
        /// which the geometries mark on exactly the modifier positions).
        /// </summary>
        public const string RestrictedKeyMessage = "You cannot assign a macro to a modifier key";

        /// <summary>Shown while no key is selected on the board. This app's wording.</summary>
        public const string NoTriggerKeyMessage = "Select a key on the keyboard to give it a macro.";

        /// <summary>Shown on a device without macro support (<see cref="MacroCapability.IsSupported"/>).</summary>
        public const string NotSupportedMessage = "This device does not support macros.";

        /// <summary>Refusal when the co-trigger cap of 06 §5 is already reached. This app's wording.</summary>
        public const string CoTriggerLimitMessageFormat = "A macro can hold at most {0} co-triggers.";

        /// <summary>Refusal when the profile already holds its macro count (06 §6). This app's wording.</summary>
        public const string MacroCountLimitMessageFormat = "This profile already holds its maximum of {0} macros.";

        /// <summary>Refusal when Paste is used before anything was copied. This app's wording.</summary>
        public const string NothingToPasteMessage = "Copy a key's macros first.";

        /// <summary>Refusal when every macro slot of the trigger key is taken (06 §1).</summary>
        public const string NoFreeSlotMessage = "Every macro slot of this key is taken.";

        /// <summary>Confirmation of specs/10-apps-and-ui.md ("Macro assigned to &lt;co-triggers + key&gt;").</summary>
        public const string AssignedMessageFormat = "Macro assigned to {0}";

        /// <summary>Joins the co-triggers and the trigger key in the assignment confirmation.</summary>
        public const string TriggerDescriptionSeparator = " + ";

        /// <summary>Builds the co-trigger refusal for <paramref name="limit"/> slots (06 §5).</summary>
        public static string BuildCoTriggerLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, CoTriggerLimitMessageFormat, limit);
        }

        /// <summary>Builds the macro-count refusal for <paramref name="limit"/> macros (06 §6).</summary>
        public static string BuildMacroCountLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, MacroCountLimitMessageFormat, limit);
        }

        /// <summary>Builds the assignment confirmation of specs/10-apps-and-ui.md.</summary>
        public static string BuildAssignedMessage(string triggerDescription)
        {
            return string.Format(CultureInfo.InvariantCulture, AssignedMessageFormat, triggerDescription);
        }

        /// <summary>
        /// The macro count the profile is held to: <see cref="MacroCapability.MaxMacroCount"/>,
        /// raised to <see cref="MacroCapability.GatedMaxMacroCount"/> when
        /// <see cref="FirmwareFeature.ExpandedMacroCount"/> is available on this firmware
        /// (specs/09-firmware.md §2 — the Freestyle Edge/Pro gate at 1.0.340; demo mode passes
        /// every gate, which the service already answers).
        /// <para>
        /// <b>Null is "no limit", not "zero"</b>: 06 §6's table lists a macros-per-layout figure
        /// for the Freestyle, RGB and Advantage360 families and none at all for the Advantage2, so
        /// that board's macro count is unbounded and every consumer here treats a null as such.
        /// </para>
        /// </summary>
        public static int? ResolveMaxMacroCount(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var macros = device.Device.Macros;
            var baseline = macros.MaxMacroCount;

            if (macros.GatedMaxMacroCount is not int gated)
            {
                return baseline;
            }

            return FirmwareGateService.IsAvailable(device.DeviceId, FirmwareFeature.ExpandedMacroCount, device.Firmware)
                ? gated
                : baseline;
        }

        /// <summary>The profile the panel edits.</summary>
        public KeyboardLayout Layout { get; }

        /// <summary>The device's macro limits — the source of every number the panel shows.</summary>
        public MacroCapability Capability { get; }

        /// <summary>Whether the device supports macros at all.</summary>
        public bool IsSupported => Capability.IsSupported;

        /// <summary>Whether macros sit in the trigger key's numbered slots (06 §1).</summary>
        public bool UsesMacroSlots { get; }

        /// <summary>Whether macros sit in the layout's flat list instead (Advantage360, 06 §1).</summary>
        public bool UsesFlatMacroList => Layout.UsesFlatMacroList;

        /// <summary>The firmware-resolved macro count limit, or null where there is none (see <see cref="ResolveMaxMacroCount"/>).</summary>
        public int? MaxMacroCount { get; }

        /// <summary>The board's selected key — the macro's trigger — or null when nothing is selected.</summary>
        public KeyboardKeyViewModel? TriggerKey
        {
            get => _triggerKey;
            private set
            {
                if (SetProperty(ref _triggerKey, value))
                {
                    RaiseTriggerChanged();
                }
            }
        }

        /// <summary>Whether the panel is about some key at all.</summary>
        public bool HasTriggerKey => DisplayedTriggerKey is not null;

        /// <summary>
        /// What the trigger key is called. Macros match on
        /// <see cref="KeyboardKey.TriggerKey"/> rather than on the remapped action (05 §1.3), so
        /// the caption names that identity and not the cap's current caption.
        /// </summary>
        public string TriggerCaption =>
            DisplayedTriggerKey is { } key
                ? KeyCaption.For(key.TriggerKey, _dialect, KeyCaption.IsMacOs, EmbeddedFontGlyphCoverage.Instance)
                : string.Empty;

        /// <summary>Whether the key whose macro is open can carry one at all (05 §5.3, 06 §2.2).</summary>
        public bool CanEditMacro => IsSupported && _target.Key is { CanAssignMacro: true };

        /// <summary>The panel's status or refusal line, or null when there is nothing to say.</summary>
        public string? Message
        {
            get => _message;
            private set
            {
                if (SetProperty(ref _message, value))
                {
                    OnPropertyChanged(nameof(HasMessage));
                }
            }
        }

        /// <summary>Whether the panel has something to say.</summary>
        public bool HasMessage => _message is not null;

        /// <summary>
        /// Whether captured keystrokes are being appended to the macro instead of remapping keys
        /// (specs/10-apps-and-ui.md, "Routing").
        /// </summary>
        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                if (!SetProperty(ref _isRecording, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(WantsKeystrokes));
                NotifyCommands();

                RecordingChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>The macro panel wants keystrokes exactly while it is recording.</summary>
        public bool WantsKeystrokes => IsRecording;

        /// <summary>
        /// The macro slots the device's dialect actually writes
        /// (<see cref="MacroCapability.PersistedSlotsPerKey"/>, 06 §1); empty on the flat-list
        /// families.
        /// </summary>
        public IReadOnlyList<MacroSlotViewModel> Slots { get; }

        /// <summary>The slot being edited, or null when the device has no slots.</summary>
        public MacroSlotViewModel? SelectedSlot
        {
            get => _selectedSlot;
            private set => SetProperty(ref _selectedSlot, value);
        }

        /// <summary>The keystrokes of the macro under edit and the four ways they change.</summary>
        public MacroStepListViewModel Steps { get; }

        /// <summary>The six co-trigger toggles (06 §5).</summary>
        public IReadOnlyList<MacroCoTriggerViewModel> CoTriggers { get; }

        /// <summary>
        /// Co-triggers the panel lets a macro carry: the count the dialect <b>writes</b>
        /// (<see cref="MacroCapability.PersistedCoTriggersPerMacro"/>, 06 §2.1, §3), not the four
        /// the model can hold — the old Freestyle serializer keeps only the first, and the rest
        /// would vanish on the next save with nothing said.
        /// </summary>
        public int MaxCoTriggers { get; }

        /// <summary>Playback speed of the macro under edit, clamped to the device's range (06 §4).</summary>
        public int Speed
        {
            get => _speed;
            set => ApplySpeed(value);
        }

        /// <summary>Lowest playback speed the device accepts (06 §4).</summary>
        public int SpeedMinimum => Capability.Speed?.Minimum ?? 0;

        /// <summary>Highest playback speed the device accepts (06 §4).</summary>
        public int SpeedMaximum => Capability.Speed?.Maximum ?? 0;

        /// <summary>Whether the device has a per-macro speed setting at all.</summary>
        public bool HasSpeed => Capability.Speed is not null;

        /// <summary>Repeat/multiplay factor of the macro under edit, clamped to the range (06 §4).</summary>
        public int Repeat
        {
            get => _repeat;
            set => ApplyRepeat(value);
        }

        /// <summary>Lowest repeat factor the device accepts (06 §4).</summary>
        public int RepeatMinimum => Capability.Repeat?.Minimum ?? 0;

        /// <summary>Highest repeat factor the device accepts (06 §4).</summary>
        public int RepeatMaximum => Capability.Repeat?.Maximum ?? 0;

        /// <summary>
        /// Whether the device has a per-macro repeat setting the file keeps. The Advantage2 has a
        /// repeat range in the model but its serializer writes no <c>{xN}</c> token at all (06 §3,
        /// <see cref="MacroCapability.PersistsRepeat"/>), so the control is hidden there rather
        /// than offering a value the next save discards.
        /// </summary>
        public bool HasRepeat => Capability.Repeat is not null && Capability.PersistsRepeat;

        /// <summary>The three budget readouts of 06 §6.</summary>
        public MacroBudgetViewModel Budget { get; }

        /// <summary>Every macro of the profile, each rendered as the file will carry it (06 §3).</summary>
        public IReadOnlyList<MacroListEntryViewModel> Macros
        {
            get => _macros;
            private set => SetProperty(ref _macros, value);
        }

        /// <summary>
        /// The editor's advisory set, pushed in after every rebuild so a row that is over one of
        /// the device's budgets can carry its amber dot. Setting it re-marks the rows, and
        /// rebuilding the rows re-applies it — the two orders happen in both directions (a macro
        /// edit rebuilds the rows first and the advisories second; a layer reset the other way
        /// round), so neither may be the only place it is done.
        /// <para>
        /// A pushed value rather than a computed one: the panel edits macros and knows nothing
        /// about <see cref="KeyboardLayout.Validate"/>, and re-running that walk here would run it
        /// twice per edit.
        /// </para>
        /// </summary>
        public EditorAdvisories Advisories
        {
            get => _advisories;
            set
            {
                _advisories = value ?? EditorAdvisories.Empty;

                ApplyAdvisories();
            }
        }

        /// <summary>The list row matching the macro under edit, or null.</summary>
        public MacroListEntryViewModel? SelectedMacro
        {
            get => _selectedMacro;
            private set => SetProperty(ref _selectedMacro, value);
        }

        /// <summary>The macro being edited — a slot's macro, a list row's macro, or a fresh draft.</summary>
        public Macro? EditedMacro => _target.Macro;

        /// <summary>Whether the macro under edit already sits in the profile (rather than being a draft).</summary>
        public bool IsEditedMacroAssigned => _target.Macro is { } macro && IsInModel(macro);

        /// <summary>Whether a key's macros are on the panel's internal clipboard.</summary>
        public bool HasClipboard => _clipboard.HasContent;

        /// <summary>
        /// The key the panel is <em>about</em>: the owner of the macro under edit, falling back to
        /// the board's selection while no macro is open — which is what lets the header still name
        /// a modifier position the refusal is about.
        /// </summary>
        private KeyboardKey? DisplayedTriggerKey => _target.Key ?? _triggerKey?.Key;

        /// <summary>Whether a real slot of the edited key is targeted (slot-based devices only).</summary>
        private bool HasTargetSlot => _target.Slot >= Macro.MinMacroIndex && _target.Slot <= Slots.Count;

        /// <summary>Starts routing captured keystrokes into the macro (specs/10-apps-and-ui.md).</summary>
        public IRelayCommand RecordCommand { get; }

        /// <summary>Stops recording; the macro keeps everything recorded so far.</summary>
        public IRelayCommand StopRecordingCommand { get; }

        /// <summary>
        /// Drops the last recorded keystroke. This is the panel's <b>button</b> — the physical
        /// Backspace key is recordable macro content and never an editing shortcut (06 §2.2).
        /// </summary>
        public IRelayCommand BackspaceCommand { get; }

        /// <summary>Empties the macro under edit, keeping its trigger metadata.</summary>
        public IRelayCommand ClearCommand { get; }

        /// <summary>Drops one arbitrary keystroke of the macro under edit.</summary>
        public IRelayCommand<MacroStepViewModel> RemoveStepCommand { get; }

        /// <summary>Turns one co-trigger on or off, enforcing the device's cap (06 §5).</summary>
        public IRelayCommand<MacroCoTriggerViewModel> ToggleCoTriggerCommand { get; }

        /// <summary>Opens one of the edited key's macro slots for editing (06 §1).</summary>
        public IRelayCommand<MacroSlotViewModel> SelectSlotCommand { get; }

        /// <summary>Loads a macro of the profile's list for editing.</summary>
        public IRelayCommand<MacroListEntryViewModel> SelectMacroCommand { get; }

        /// <summary>Puts the macro under edit onto its key (06 §2.2).</summary>
        public IRelayCommand AssignCommand { get; }

        /// <summary>Takes the macro under edit off the profile and starts a fresh draft.</summary>
        public IRelayCommand DeleteCommand { get; }

        /// <summary>Copies the edited key's macros to the panel's internal clipboard (05 §1.3).</summary>
        public IRelayCommand CopyCommand { get; }

        /// <summary>Pastes the clipboard's macros onto the edited key (<see cref="MacroClipboard"/>).</summary>
        public IRelayCommand PasteCommand { get; }

        /// <summary>Raised whenever <see cref="IsRecording"/> moves; the editor owns capture.</summary>
        public event EventHandler? RecordingChanged;

        /// <summary>Raised after any write to the profile's macros, so the editor can recount.</summary>
        public event EventHandler? MacrosChanged;

        private readonly TokenDialect _dialect;
        private readonly List<IRelayCommand> _commands = [];
        private readonly MacroClipboard _clipboard = new();
        private IReadOnlyList<MacroListEntryViewModel> _macros = [];
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private MacroListEntryViewModel? _selectedMacro;
        private MacroSlotViewModel? _selectedSlot;
        private KeyboardKeyViewModel? _triggerKey;
        private KeyboardLayer? _layer;
        private MacroEditTarget _target = MacroEditTarget.None;
        private string? _message;
        private int _speed;
        private int _repeat;
        private bool _isRecording;

        /// <summary>
        /// Creates the panel for <paramref name="device"/>'s <paramref name="layout"/>. The
        /// firmware gate is evaluated once here, from the snapshot the shell already carries.
        /// </summary>
        public MacroPanelViewModel(DeviceSnapshot device, KeyboardLayout layout)
        {
            ArgumentNullException.ThrowIfNull(device);

            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
            Capability = device.Device.Macros;
            MaxMacroCount = ResolveMaxMacroCount(device);
            MaxCoTriggers = Capability.PersistedCoTriggersPerMacro ?? Capability.MaxCoTriggersPerMacro ?? 0;

            // The strip offers the slots the dialect writes, not the five the model owns: a macro
            // put in slot 4 of a Freestyle Edge is dropped by the very next save (06 §1).
            UsesMacroSlots = Capability.PersistedSlotsPerKey is > 0 && !Capability.UsesFlatMacroList;

            _dialect = layout.Dialect;

            Slots = UsesMacroSlots ? MacroSlotViewModel.CreateAll(Capability.PersistedSlotsPerKey!.Value) : [];
            Steps = new MacroStepListViewModel(_dialect);
            CoTriggers = MacroCoTriggerViewModel.CreateAll(_dialect);
            Budget = new MacroBudgetViewModel(Capability, MaxMacroCount);

            RecordCommand = Register(new RelayCommand(Record, CanRecord));
            StopRecordingCommand = Register(new RelayCommand(StopRecording, () => IsRecording));
            BackspaceCommand = Register(new RelayCommand(Backspace, () => Steps.HasItems));
            ClearCommand = Register(new RelayCommand(Clear, () => Steps.HasItems));
            RemoveStepCommand = Register(new RelayCommand<MacroStepViewModel>(RemoveStep, step => step is not null));
            ToggleCoTriggerCommand = Register(
                new RelayCommand<MacroCoTriggerViewModel>(ToggleCoTrigger, _ => _target.IsOpen));
            SelectSlotCommand = Register(new RelayCommand<MacroSlotViewModel>(SelectSlot, _ => CanEditMacro));
            SelectMacroCommand = Register(new RelayCommand<MacroListEntryViewModel>(SelectMacro, entry => entry is not null));
            AssignCommand = Register(new RelayCommand(Assign, CanAssign));
            DeleteCommand = Register(new RelayCommand(Delete, () => IsEditedMacroAssigned));
            CopyCommand = Register(new RelayCommand(Copy, () => UsesMacroSlots && _target.Key?.IsMacro == true));
            PasteCommand = Register(new RelayCommand(Paste, () => UsesMacroSlots && HasClipboard && CanEditMacro));

            ReloadTrigger();
        }

        /// <summary>
        /// Points the panel at the board's current selection. Anything half-recorded belongs to
        /// the key it was started on, so the switch stops recording first.
        /// </summary>
        public void SetTrigger(KeyboardKeyViewModel? key, KeyboardLayer? layer)
        {
            StopRecording();

            _layer = layer;
            TriggerKey = key;

            ReloadTrigger();
        }

        /// <summary>Leaves recording state; the editor stops capture off <see cref="RecordingChanged"/>.</summary>
        public void StopRecording()
        {
            IsRecording = false;
        }

        /// <summary>
        /// Appends one captured keystroke to the macro under edit, folding the modifiers held at
        /// that moment into the step's <see cref="MacroModifiers"/> (05 §5.1). A captured keystroke
        /// whose key is itself a modifier carries no held modifiers by design, and
        /// <see cref="Keystroke"/> drops any that were offered anyway.
        /// </summary>
        public void ReceiveKeystroke(CapturedKeystroke keystroke)
        {
            if (keystroke is null || !IsRecording || !_target.IsOpen)
            {
                return;
            }

            Steps.Append(BuildKeystroke(keystroke));

            AfterMacroChanged();
        }

        /// <summary>
        /// Puts <paramref name="key"/> into the macro under edit as a plain keystroke, at the end
        /// of what is recorded so far — the insertion hook the Macro Timing Delays and Search Keys
        /// overlays of spec 11 use to drop a token into the recording (spec 10: "insert 1-999 ms or
        /// random delays into macros"). The budget readouts refresh with it, so an insertion that
        /// takes the macro past its cap shows the 06 §6 warning like a recorded keystroke does.
        /// Returns false when no macro is being edited.
        /// </summary>
        public bool InsertKeystroke(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (!_target.IsOpen || !Steps.Append(new Keystroke(key)))
            {
                return false;
            }

            AfterMacroChanged();

            return true;
        }

        /// <summary>
        /// Drops the keystroke at <paramref name="index"/> (0-based) from the macro under edit;
        /// false when there is none there.
        /// </summary>
        public bool RemoveKeystrokeAt(int index)
        {
            if (!Steps.RemoveAt(index))
            {
                return false;
            }

            AfterMacroChanged();

            return true;
        }

        /// <summary>
        /// Re-reads everything after something outside the panel wrote to the profile — the
        /// editor's Reset Key / Reset Layer / Reset Layout all empty macro slots.
        /// </summary>
        public void RefreshFromModel()
        {
            ReloadTrigger();
        }

        /// <summary>Re-seeds the edit target from the board's selection.</summary>
        private void ReloadTrigger()
        {
            Message = ResolveTriggerMessage();

            var key = _triggerKey?.Key;

            if (!IsSupported || key is null || !key.CanAssignMacro)
            {
                SetTarget(MacroEditTarget.None);

                return;
            }

            OpenSlot(key, _layer, key.ActiveMacroIndex);
        }

        private string? ResolveTriggerMessage()
        {
            if (!IsSupported)
            {
                return NotSupportedMessage;
            }

            if (_triggerKey is null)
            {
                return NoTriggerKeyMessage;
            }

            // 05 §5.3 marks exactly the modifier positions as unable to carry a macro, which is
            // what the legacy message names.
            return _triggerKey.CanAssignMacro ? null : RestrictedKeyMessage;
        }

        /// <summary>
        /// Opens slot <paramref name="slot"/> of <paramref name="key"/>, or a fresh draft when it
        /// is empty. On the flat-list families there is no slot to open: the panel starts a draft
        /// and Assign appends it with its trigger and layer (06 §1).
        /// </summary>
        private void OpenSlot(KeyboardKey key, KeyboardLayer? layer, int slot)
        {
            if (!UsesMacroSlots)
            {
                SetTarget(new MacroEditTarget(Layout.CreateMacro(), key, layer, 0));

                return;
            }

            if (slot < Macro.MinMacroIndex || slot > Slots.Count)
            {
                slot = Macro.MinMacroIndex;
            }

            key.ActiveMacroIndex = slot;

            SetTarget(new MacroEditTarget(key.GetMacro(slot) ?? Layout.CreateMacro(), key, layer, slot));
        }

        private void SetTarget(MacroEditTarget target)
        {
            _target = target;

            Steps.Load(target.Macro);
            LoadSpeedAndRepeat(target.Macro);
            RefreshCoTriggers();

            OnPropertyChanged(nameof(EditedMacro));

            RaiseTriggerChanged();
            RefreshReadouts();
        }

        private void RaiseTriggerChanged()
        {
            OnPropertyChanged(nameof(HasTriggerKey));
            OnPropertyChanged(nameof(TriggerCaption));
            OnPropertyChanged(nameof(CanEditMacro));
        }

        private void LoadSpeedAndRepeat(Macro? macro)
        {
            // Loading must not write back: the fields move, the model does not.
            SetProperty(ref _speed, macro?.Speed ?? Capability.Speed?.Default ?? 0, nameof(Speed));
            SetProperty(ref _repeat, macro?.RepeatFrequency ?? Capability.Repeat?.Default ?? 0, nameof(Repeat));
        }

        private void ApplySpeed(int value)
        {
            var clamped = Capability.Speed is { } range ? Math.Clamp(value, range.Minimum, range.Maximum) : value;

            if (!SetProperty(ref _speed, clamped, nameof(Speed)) || _target.Macro is not { } macro)
            {
                return;
            }

            macro.Speed = clamped;

            AfterMacroChanged();
        }

        private void ApplyRepeat(int value)
        {
            var clamped = Capability.Repeat is { } range ? Math.Clamp(value, range.Minimum, range.Maximum) : value;

            if (!SetProperty(ref _repeat, clamped, nameof(Repeat)) || _target.Macro is not { } macro)
            {
                return;
            }

            macro.RepeatFrequency = clamped;

            AfterMacroChanged();
        }

        private bool CanRecord()
        {
            return CanEditMacro && _target.IsOpen && !IsRecording;
        }

        private void Record()
        {
            if (!CanRecord())
            {
                return;
            }

            Message = null;
            IsRecording = true;
        }

        private void Backspace()
        {
            if (Steps.RemoveLast())
            {
                AfterMacroChanged();
            }
        }

        private void Clear()
        {
            if (Steps.Clear())
            {
                AfterMacroChanged();
            }
        }

        private void RemoveStep(MacroStepViewModel? step)
        {
            if (step is not null)
            {
                RemoveKeystrokeAt(step.Position - 1);
            }
        }

        private void ToggleCoTrigger(MacroCoTriggerViewModel? toggle)
        {
            if (toggle is null || _target.Macro is not { } macro)
            {
                return;
            }

            if (toggle.IsOn)
            {
                RemoveCoTrigger(macro, toggle.Key);
            }
            else
            {
                // Macro.AddCoTrigger deliberately neither de-duplicates nor refuses (06 §5), so
                // the cap is the panel's to hold; Validate() stays the backstop.
                if (macro.CoTriggerCount >= MaxCoTriggers)
                {
                    Message = BuildCoTriggerLimitMessage(MaxCoTriggers);

                    return;
                }

                macro.AddCoTrigger(toggle.Key);
                Message = null;
            }

            RefreshCoTriggers();
            AfterMacroChanged();
        }

        private void SelectSlot(MacroSlotViewModel? slot)
        {
            if (slot is null || _target.Key is not { } key || !CanEditMacro)
            {
                return;
            }

            StopRecording();

            Message = null;

            OpenSlot(key, _target.Layer, slot.Slot);
        }

        /// <summary>
        /// Loads a row of the profile's macro list. The board's selection is the editor's and does
        /// not move, but the panel's own trigger, slot and every key-scoped action follow the macro
        /// that is now open — a row from another key must not leave the header naming one key while
        /// Record and Clear write to another.
        /// </summary>
        private void SelectMacro(MacroListEntryViewModel? entry)
        {
            if (entry is null)
            {
                return;
            }

            StopRecording();

            Message = null;

            if (UsesMacroSlots
                && entry.Key is { } key
                && entry.Slot >= Macro.MinMacroIndex
                && entry.Slot <= Slots.Count)
            {
                key.ActiveMacroIndex = entry.Slot;
            }

            SetTarget(new MacroEditTarget(entry.Macro, entry.Key, entry.Layer, entry.Slot));
        }

        private bool CanAssign()
        {
            return CanEditMacro && _target.IsOpen && !IsEditedMacroAssigned;
        }

        private void Assign()
        {
            if (!CanAssign())
            {
                return;
            }

            var macro = _target.Macro!;
            var key = _target.Key!;

            // Replacing an occupied slot does not grow the profile, so only an assignment that
            // really adds a macro is measured against the count limit (06 §6).
            if (AddsMacro(key) && !TryFitMacroCount(1))
            {
                return;
            }

            if (!AssignToStore(macro, key))
            {
                return;
            }

            Message = BuildAssignedMessage(BuildTriggerDescription());

            AfterMacroChanged();
        }

        private bool AddsMacro(KeyboardKey key)
        {
            return !UsesMacroSlots || !HasTargetSlot || key.GetMacro(_target.Slot) is null;
        }

        /// <summary>
        /// Whether the profile can take <paramref name="added"/> more macros (06 §6). A device
        /// stating no count — the Advantage2 — has no limit to reach; a refusal reports itself.
        /// </summary>
        private bool TryFitMacroCount(int added)
        {
            if (MaxMacroCount is not int limit || Layout.MacroCount + added <= limit)
            {
                return true;
            }

            Message = BuildMacroCountLimitMessage(limit);

            return false;
        }

        private bool AssignToStore(Macro macro, KeyboardKey key)
        {
            // Both stores stamp the trigger and layer, exactly as MacroLineParser does before it
            // routes a parsed macro to one or the other (04 §4.2 step 5): the Gen2 list needs them
            // to serialize at all, and on the slot families they are what lets the macro render
            // its own 06 §3 line in the panel's list.
            macro.TriggerKey = key.TriggerKey.Code;
            macro.LayerIndex = _target.Layer?.Index ?? Macro.UnassignedIndex;

            if (!UsesMacroSlots)
            {
                Layout.AddMacro(macro);

                return true;
            }

            // SetMacro rather than AssignMacro because the user picked the slot, and AssignMacro
            // always lands in the *first* free one. The two guards it applies are held above it:
            // CanAssignMacro is the panel's verbatim refusal, and UsesMacroSlots picked this
            // branch. With no slot targeted there is nothing to contradict, so the guarded editor
            // path runs instead.
            if (HasTargetSlot)
            {
                key.SetMacro(_target.Slot, macro);

                return true;
            }

            var slot = key.AssignMacro(macro);

            if (slot == 0)
            {
                Message = NoFreeSlotMessage;

                return false;
            }

            _target = new MacroEditTarget(macro, key, _target.Layer, slot);

            return true;
        }

        private void Delete()
        {
            if (_target.Macro is not { } macro || !IsEditedMacroAssigned)
            {
                return;
            }

            StopRecording();

            if (UsesMacroSlots && _target.Key is { } owner && _target.Slot >= Macro.MinMacroIndex)
            {
                owner.SetMacro(_target.Slot, null);
            }
            else
            {
                Layout.RemoveMacro(macro);
            }

            Message = null;

            // Deleting leaves the same slot open with a fresh draft, so the panel never sits on a
            // macro that is no longer in the profile.
            if (_target.Key is { } key)
            {
                OpenSlot(key, _target.Layer, _target.Slot);
            }
            else
            {
                SetTarget(MacroEditTarget.None);
            }

            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Copy()
        {
            if (!UsesMacroSlots || _target.Key is not { } key)
            {
                return;
            }

            _clipboard.Copy(key);

            Message = null;

            OnPropertyChanged(nameof(HasClipboard));
            NotifyCommands();
        }

        private void Paste()
        {
            if (!UsesMacroSlots || _target.Key is not { } key)
            {
                return;
            }

            if (!_clipboard.HasContent)
            {
                Message = NothingToPasteMessage;

                return;
            }

            if (!CanEditMacro)
            {
                Message = RestrictedKeyMessage;

                return;
            }

            // A paste replaces every slot of the target, so it grows the profile by the difference
            // between what is on the clipboard and what the key holds today (06 §6). Assign is held
            // to the same count and Paste must not be the way around it.
            if (!TryFitMacroCount(_clipboard.MacroCount - key.MacroCount))
            {
                return;
            }

            StopRecording();

            _clipboard.PasteInto(key);

            Message = null;

            OpenSlot(key, _target.Layer, key.ActiveMacroIndex);

            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AfterMacroChanged()
        {
            RefreshReadouts();

            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshReadouts()
        {
            RefreshSlots();
            RefreshMacros();

            Budget.RefreshFromModel(_target.Macro, Layout);

            OnPropertyChanged(nameof(IsEditedMacroAssigned));
            NotifyCommands();
        }

        private void RefreshSlots()
        {
            var key = UsesMacroSlots ? _target.Key : null;

            MacroSlotViewModel? selected = null;

            foreach (var slot in Slots)
            {
                slot.RefreshFromModel(key?.GetMacro(slot.Slot), _dialect);
                slot.IsSelected = key is not null && slot.Slot == _target.Slot;

                if (slot.IsSelected)
                {
                    selected = slot;
                }
            }

            SelectedSlot = selected;
        }

        private void RefreshMacros()
        {
            var entries = MacroListEntryViewModel.BuildAll(Layout);

            MacroListEntryViewModel? selected = null;

            foreach (var entry in entries)
            {
                entry.IsSelected = _target.Macro is not null && ReferenceEquals(entry.Macro, _target.Macro);

                if (entry.IsSelected)
                {
                    selected = entry;
                }
            }

            Macros = entries;
            SelectedMacro = selected;

            ApplyAdvisories();
        }

        /// <summary>
        /// Marks the rows the editor's advisory set anchors to. Nothing is disabled or hidden by
        /// it: an over-budget macro is still edited, still assigned and still saved.
        /// </summary>
        private void ApplyAdvisories()
        {
            foreach (var entry in _macros)
            {
                entry.HasAdvisory = _advisories.HasAdvisoryForMacro(entry.Layer?.Index, entry.Key?.Index, entry.Slot);
            }
        }

        private void RefreshCoTriggers()
        {
            foreach (var toggle in CoTriggers)
            {
                toggle.IsOn = _target.Macro?.ContainsCoTrigger(toggle.Key.Code) == true;
            }
        }

        private bool IsInModel(Macro macro)
        {
            foreach (var candidate in Layout.EnumerateMacros())
            {
                if (ReferenceEquals(candidate, macro))
                {
                    return true;
                }
            }

            return false;
        }

        private string BuildTriggerDescription()
        {
            var parts = new List<string>();

            if (_target.Macro is { } macro)
            {
                foreach (var coTrigger in macro.CoTriggers)
                {
                    parts.Add(KeyCaption.For(coTrigger, _dialect, KeyCaption.IsMacOs, EmbeddedFontGlyphCoverage.Instance));
                }
            }

            parts.Add(TriggerCaption);

            return string.Join(TriggerDescriptionSeparator, parts);
        }

        private void NotifyCommands()
        {
            foreach (var command in _commands)
            {
                command.NotifyCanExecuteChanged();
            }
        }

        private TCommand Register<TCommand>(TCommand command)
            where TCommand : IRelayCommand
        {
            _commands.Add(command);

            return command;
        }

        private static Keystroke BuildKeystroke(CapturedKeystroke captured)
        {
            var modifiers = MacroModifiers.None;

            foreach (var held in captured.HeldModifiers)
            {
                if (MacroModifierCodes.TryFromKeyCode(held.Code, out var modifier))
                {
                    modifiers |= modifier;
                }
            }

            return new Keystroke(captured.Key, modifiers);
        }

        private static void RemoveCoTrigger(Macro macro, KeyDefinition key)
        {
            // AddCoTrigger does not de-duplicate (06 §5), so a toggle turned off has to clear
            // every slot carrying that key, not just the first.
            for (var index = macro.CoTriggers.Count - 1; index >= 0; index--)
            {
                if (macro.CoTriggers[index].Code == key.Code)
                {
                    macro.RemoveCoTriggerAt(index);
                }
            }
        }
    }
}
