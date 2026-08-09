using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>Trigger strip</b> (issue #137): what the key has to be pressed <em>with</em>
    /// for this macro to fire, and whether anything else on the key already claims that combination.
    ///
    /// <code>
    /// TRIGGER
    /// [⇧] [⌃] [⌥]  +  [hk7]
    /// bare press · no collision
    /// </code>
    ///
    /// <para><b>Three latches, not six, and not four — a deliberate deviation</b> recorded in
    /// docs/app/design-system.md. The six the footer used to carry are specs/10-apps-and-ui.md's own
    /// ("six co-trigger buttons — Left/Right Shift, Ctrl, Alt"); issue #137's rule is that authoring
    /// is left-only, so the right-hand three are folded onto their left latch through
    /// <see cref="MacroCoTriggerViewModel.Family"/> rather than dropped from the model. A fourth,
    /// <c>⌘</c>, is <b>not</b> offered: no co-trigger anywhere in specs 06 or 10 names one (06 §5's
    /// examples are <c>{lshft}</c>/<c>{rctrl}</c>), so a <c>⌘</c> latch would author a token no
    /// firmware is documented to honour — a macro that silently never fires.</para>
    ///
    /// <para><b>Preserve on load, normalize on edit.</b> A file that carries <c>{rshft}</c> or the
    /// generic <c>{shft}</c> keeps it and lights the <c>⇧</c> latch, so the user can see the
    /// co-trigger exists; opening a key must not rewrite a co-trigger the user only looked at, and
    /// must not dirty the profile. Touching <b>any</b> latch rewrites the whole set to the left-hand
    /// spellings of the latches that are lit — which is the only moment the panel is entitled to
    /// change what the file says.</para>
    ///
    /// <para><b>The status is an advisory and never a refusal.</b> A collision is what
    /// <c>KeyboardLayout.Validate</c> reports as <c>MacroTriggerCollision</c> and it reports rather
    /// than blocks (docs/app/keyboard-model.md, invariant 1); the reserved-trigger rule of 06 §5 is
    /// the same. Both are amber, never red, and neither is reachable from a <c>CanExecute</c>.</para>
    ///
    /// <para>Split into a partial for the reason <c>MacroInspectorPanelViewModel.Composer.cs</c> was:
    /// the main file is already long and docs/guides/Coding Conventions.md forbids growing it into a
    /// god class.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>The section label over the strip. This app's wording.</summary>
        public const string TriggerSectionLabel = "TRIGGER";

        /// <summary>Between the latches and the trigger token, as 06 §5's own examples read.</summary>
        public const string TriggerJoin = "+";

        /// <summary>Between the two halves of the status readout. U+00B7; both embedded families carry it.</summary>
        public const string StatusSeparator = " · ";

        /// <summary>The status's first half when the macro carries no co-trigger at all.</summary>
        public const string BarePressStatus = "bare press";

        /// <summary>Its first half when the macro does carry one (06 §5).</summary>
        public const string CoTriggeredStatus = "co-triggered";

        /// <summary>Its second half when nothing else on the key claims the same combination.</summary>
        public const string NoCollisionStatus = "no collision";

        /// <summary>Its second half when another <b>slot</b> of this key claims it (06 §5).</summary>
        public const string CollisionSlotFormat = "collides with slot {0}";

        /// <summary>
        /// The same, where there are no slots to name: on a flat-list board (06 §1) two macros can
        /// share a trigger and a layer just as readily, and there is no slot number to point at.
        /// </summary>
        public const string CollisionStatus = "collides with another macro on this key";

        /// <summary>
        /// 06 §5's reserved-trigger rule: <c>fn1s</c> and <c>keyt</c> require at least one
        /// co-trigger, so a bare macro on either can never fire. Amber, and it blocks nothing —
        /// <c>Validate()</c> reports it as <c>ReservedMacroTriggerWithoutCoTrigger</c>.
        /// </summary>
        public const string ReservedTriggerStatus = "needs a co-trigger to fire";

        /// <summary>Refusal when the co-trigger cap of 06 §5 is already reached.</summary>
        public const string CoTriggerLimitMessageFormat = "A macro can hold at most {0} co-triggers.";

        /// <summary>Builds the co-trigger refusal for <paramref name="limit"/> slots (06 §5).</summary>
        public static string BuildCoTriggerLimitMessage(int limit)
        {
            return string.Format(CultureInfo.InvariantCulture, CoTriggerLimitMessageFormat, limit);
        }

        /// <summary>Builds the collision half of the status, naming the slot it collides with.</summary>
        public static string BuildCollisionStatus(int slot)
        {
            return string.Format(CultureInfo.InvariantCulture, CollisionSlotFormat, slot);
        }

        /// <summary>Joins the two halves of the readout: <c>bare press · no collision</c>.</summary>
        public static string BuildTriggerStatus(string trigger, string collision)
        {
            return trigger + StatusSeparator + collision;
        }

        /// <summary>The three left-hand co-trigger latches of 06 §5 — see the type's remarks.</summary>
        public IReadOnlyList<MacroCoTriggerViewModel> CoTriggers { get; private set; } = [];

        /// <summary>How many co-triggers the dialect actually writes (06 §2.1, §3).</summary>
        public int MaxCoTriggers { get; private set; }

        /// <summary>Whether the device has co-triggers at all.</summary>
        public bool HasCoTriggers => MaxCoTriggers > 0;

        /// <summary>
        /// The position's trigger key as the open board's dialect spells it — <c>[hk7]</c>. Mono:
        /// it is literally what the layout file carries on the trigger side of the macro line
        /// (06 §3). Empty while the panel has nothing to edit.
        /// </summary>
        public string TriggerTokenText
        {
            get => _triggerTokenText;
            private set => SetProperty(ref _triggerTokenText, value);
        }

        /// <summary>
        /// The readout right of the latches: <c>bare press · no collision</c>, or the advisory that
        /// replaces its second half. Empty while the panel has nothing to edit.
        /// </summary>
        public string TriggerStatus
        {
            get => _triggerStatus;
            private set => SetProperty(ref _triggerStatus, value);
        }

        /// <summary>
        /// Whether the readout is an advisory, and so amber. It is a colour <em>role</em> and never
        /// a brush (docs/app/design-system.md, invariant 10), and it gates nothing: a colliding or
        /// reserved trigger still saves.
        /// </summary>
        public bool IsTriggerAdvisory
        {
            get => _isTriggerAdvisory;
            private set => SetProperty(ref _isTriggerAdvisory, value);
        }

        /// <summary>Turns one co-trigger on or off, enforcing the device's cap (06 §5).</summary>
        public IRelayCommand<MacroCoTriggerViewModel> ToggleCoTriggerCommand { get; private set; } = null!;

        private string _triggerTokenText = string.Empty;
        private string _triggerStatus = string.Empty;
        private bool _isTriggerAdvisory;

        /// <summary>
        /// Builds the strip. Called from the constructor, which is why the properties above carry a
        /// private setter rather than being get-only — the same shape <c>CreateComposer</c> uses,
        /// and for the same reason: the assignments belong beside the code that owns them.
        /// </summary>
        private void CreateTriggerStrip(TokenDialect dialect)
        {
            MaxCoTriggers = _capability.PersistedCoTriggersPerMacro ?? 0;

            CoTriggers = MacroCoTriggerViewModel.CreateAll(dialect, leftOnly: true);

            ToggleCoTriggerCommand = new RelayCommand<MacroCoTriggerViewModel>(ToggleCoTrigger, _ => IsAvailable);
        }

        /// <summary>
        /// Re-reads the whole strip off the model — the latches, the trigger token and the status.
        /// It writes nothing, which is the half of the "preserve on load" rule that matters: a file's
        /// right-hand or generic co-trigger is only ever <em>read</em> here.
        /// </summary>
        private void RefreshTrigger()
        {
            RefreshCoTriggers();

            TriggerTokenText = IsAvailable && _key is { } key
                ? KeyboardKeyViewModel.FormatToken(key.Key.TriggerKey, _dialect)
                : string.Empty;

            RefreshTriggerStatus();
        }

        /// <summary>
        /// Lights each latch from the <b>family</b> of every co-trigger the macro carries, so a
        /// <c>{rshft}</c> or a generic <c>{shft}</c> already in the file lights <c>⇧</c> rather than
        /// nothing. Nothing is rewritten; see <see cref="ToggleCoTrigger"/> for the one path that is
        /// entitled to.
        /// </summary>
        private void RefreshCoTriggers()
        {
            var lit = ReadCoTriggerFamilies();

            foreach (var toggle in CoTriggers)
            {
                toggle.IsOn = toggle.Family != MacroModifiers.None && (lit & toggle.Family) != MacroModifiers.None;
            }
        }

        /// <summary>Every modifier family the macro under edit carries a co-trigger for (05 §5.1).</summary>
        private MacroModifiers ReadCoTriggerFamilies()
        {
            var families = MacroModifiers.None;

            if (_macro is not { } macro)
            {
                return families;
            }

            foreach (var coTrigger in macro.CoTriggers)
            {
                if (MacroModifierCodes.TryFromKeyCode(coTrigger.Code, out var modifier))
                {
                    families |= MacroCoTriggerViewModel.FamilyOf(modifier);
                }
            }

            return families;
        }

        /// <summary>
        /// Flips one latch and <b>rewrites the whole co-trigger set</b> to the left-hand spellings
        /// of the latches that end up lit. That is the deliberate half of "preserve, then normalize":
        /// a right-hand or generic co-trigger the file carried survives every load, every refresh and
        /// every other edit of the panel, and is rewritten only when the user touches the strip that
        /// is showing it.
        /// <para>
        /// The cap is the panel's to hold, because <see cref="Macro.AddCoTrigger"/> deliberately
        /// neither de-duplicates nor refuses (06 §5 counts populated slots). It refuses only a latch
        /// being switched <em>on</em>: a set that arrived over the cap from a file must always be
        /// reducible.
        /// </para>
        /// </summary>
        private void ToggleCoTrigger(MacroCoTriggerViewModel? toggle)
        {
            if (toggle is null || EnsureMacro() is not { } macro)
            {
                return;
            }

            var wanted = new List<MacroCoTriggerViewModel>(CoTriggers.Count);

            foreach (var latch in CoTriggers)
            {
                var isOn = ReferenceEquals(latch, toggle) ? !latch.IsOn : latch.IsOn;

                if (isOn)
                {
                    wanted.Add(latch);
                }
            }

            if (!toggle.IsOn && wanted.Count > MaxCoTriggers)
            {
                Message = BuildCoTriggerLimitMessage(MaxCoTriggers);

                return;
            }

            macro.ClearCoTriggers();

            foreach (var latch in wanted)
            {
                macro.AddCoTrigger(latch.Key);
            }

            Message = string.Empty;

            OnMacroWritten();
        }

        private void RefreshTriggerStatus()
        {
            if (!IsAvailable || _key is null)
            {
                TriggerStatus = string.Empty;
                IsTriggerAdvisory = false;

                return;
            }

            var coTriggerCount = _macro?.CoTriggerCount ?? 0;
            var trigger = coTriggerCount > 0 ? CoTriggeredStatus : BarePressStatus;

            // The reserved rule comes first because it is the stronger fact: such a macro cannot
            // fire at all, so naming a collision instead would answer a smaller question.
            if (coTriggerCount == 0 && IsReservedTrigger())
            {
                TriggerStatus = BuildTriggerStatus(trigger, ReservedTriggerStatus);
                IsTriggerAdvisory = true;

                return;
            }

            if (ResolveCollisionStatus() is { Length: > 0 } collision)
            {
                TriggerStatus = BuildTriggerStatus(trigger, collision);
                IsTriggerAdvisory = true;

                return;
            }

            TriggerStatus = BuildTriggerStatus(trigger, NoCollisionStatus);
            IsTriggerAdvisory = false;
        }

        /// <summary>
        /// Whether this position is one of 06 §5's reserved triggers. <b>Gen2 only</b>, exactly as
        /// <c>KeyboardLayoutValidator</c> gates it — the advisory and the validator must not disagree
        /// about what the firmware refuses.
        /// </summary>
        private bool IsReservedTrigger()
        {
            if (!_capability.UsesFlatMacroList || _key is not { } key)
            {
                return false;
            }

            return key.Key.TriggerKey.Code is KeyboardKey.Fn1ShiftKeyCode or KeyboardKey.KeypadToggleKeyCode;
        }

        /// <summary>
        /// 06 §5's duplicate-trigger rule over the macros that share this position's trigger key,
        /// walked in persisted-slot order so the slot named is the first one that collides. Empty
        /// when nothing collides. <see cref="Macro.CollidesWith"/> owns the comparison, so the
        /// readout and <c>Validate()</c> can never disagree.
        /// </summary>
        private string ResolveCollisionStatus()
        {
            if (_macro is not { } macro || _key is not { } key)
            {
                return string.Empty;
            }

            if (_usesMacroSlots)
            {
                for (var slot = Macro.MinMacroIndex; slot <= _persistedSlots; slot++)
                {
                    var other = key.Key.GetMacro(slot);

                    if (other is null || ReferenceEquals(other, macro))
                    {
                        continue;
                    }

                    if (macro.CollidesWith(other))
                    {
                        return BuildCollisionStatus(slot);
                    }
                }

                return string.Empty;
            }

            if (_layout is not { } layout)
            {
                return string.Empty;
            }

            foreach (var other in layout.FindMacros(_layer?.Index ?? Macro.UnassignedIndex, key.Key.TriggerKey.Code))
            {
                if (!ReferenceEquals(other, macro) && macro.CollidesWith(other))
                {
                    return CollisionStatus;
                }
            }

            return string.Empty;
        }
    }
}
