using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macro panel's <b>Trigger strip</b> (issue #137, redrawn by #146): what the key has to be
    /// pressed <em>with</em> for this macro to fire, and whether anything else on the key already
    /// claims that combination.
    ///
    /// <code>
    /// SLOTS [1][2][+][+][+]              TRIGGER + [⇧][⌃][⌥]
    ///                       collides with slot 1
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
    /// <para><b>The latches sit on the slot row, and they follow the slot</b> (issue #146). They are
    /// a fact about the macro in the slot under edit, not about the key, so switching chips re-reads
    /// them: <c>SelectSlot → ReadFromModel → RefreshTrigger</c>. That coupling is load bearing now
    /// that the two strips share a row — a latch left lit from the previous slot would claim a
    /// co-trigger the macro on screen does not carry.</para>
    ///
    /// <para><b>Preserve on load, normalize on edit.</b> A file that carries <c>{rshft}</c> or the
    /// generic <c>{shft}</c> keeps it and lights the <c>⇧</c> latch, so the user can see the
    /// co-trigger exists; opening a key must not rewrite a co-trigger the user only looked at, and
    /// must not dirty the profile. Touching <b>any</b> latch rewrites the whole set to the left-hand
    /// spellings of the latches that are lit — which is the only moment the panel is entitled to
    /// change what the file says.</para>
    ///
    /// <para><b>The status is an advisory, and since issue #146 it is <em>only</em> an advisory.</b>
    /// It used to read <c>bare press · no collision</c> at rest, which is a line of text under every
    /// macro on every key saying that nothing is wrong; the mock draws nothing there, and so does
    /// this. <see cref="TriggerStatus"/> is now the advisory or the empty string, and
    /// <see cref="IsTriggerAdvisory"/> is exactly "there is one". A collision is what
    /// <c>KeyboardLayout.Validate</c> reports as <c>MacroTriggerCollision</c> and it reports rather
    /// than blocks (docs/app/keyboard-model.md, invariant 1); the reserved-trigger rule of 06 §5 is
    /// the same. Both are amber, never red, and neither is reachable from a <c>CanExecute</c>.</para>
    ///
    /// <para><b>There is one collision computation, not two.</b> The slot chips need every slot
    /// involved in a clash and the status needs the first slot the macro under edit clashes with;
    /// <see cref="ResolveCollisionStatus"/> answers both at once and
    /// <see cref="RefreshSlotsAndTrigger"/> is what makes sure it is asked once. Two walks of the
    /// same five slots would be two chances to disagree about what collides.</para>
    ///
    /// <para>Split into a partial for the reason <c>MacroInspectorPanelViewModel.Composer.cs</c> was:
    /// the main file is already long and docs/guides/Coding Conventions.md forbids growing it into a
    /// god class.</para>
    /// </summary>
    public sealed partial class MacroInspectorPanelViewModel
    {
        /// <summary>The section label over the strip. This app's wording.</summary>
        public const string TriggerSectionLabel = "TRIGGER";

        /// <summary>
        /// Between the label and the latches, as 06 §5's own examples read. It used to sit between
        /// the latches and the trigger token; issue #146 dropped the token — the rail header already
        /// names the position — so the <c>+</c> moved to the label it now qualifies.
        /// </summary>
        public const string TriggerJoin = "+";

        /// <summary>The advisory when another <b>slot</b> of this key claims the same combination (06 §5).</summary>
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

        /// <summary>Builds the collision advisory, naming the slot it collides with.</summary>
        public static string BuildCollisionStatus(int slot)
        {
            return string.Format(CultureInfo.InvariantCulture, CollisionSlotFormat, slot);
        }

        /// <summary>The three left-hand co-trigger latches of 06 §5 — see the type's remarks.</summary>
        public IReadOnlyList<MacroCoTriggerViewModel> CoTriggers { get; private set; } = [];

        /// <summary>How many co-triggers the dialect actually writes (06 §2.1, §3).</summary>
        public int MaxCoTriggers { get; private set; }

        /// <summary>Whether the device has co-triggers at all.</summary>
        public bool HasCoTriggers => MaxCoTriggers > 0;

        /// <summary>
        /// The one amber line the strip can draw, or the empty string. It is <b>the advisory alone</b>
        /// since issue #146: <c>collides with slot 3</c>, <c>collides with another macro on this
        /// key</c>, or <c>needs a co-trigger to fire</c>. At rest there is nothing to say and nothing
        /// is drawn — see the type's remarks.
        /// </summary>
        public string TriggerStatus
        {
            get => _triggerStatus;
            private set
            {
                if (SetProperty(ref _triggerStatus, value))
                {
                    OnPropertyChanged(nameof(IsTriggerAdvisory));
                }
            }
        }

        /// <summary>
        /// Whether there is an advisory to draw — and so, exactly, whether
        /// <see cref="TriggerStatus"/> says anything. It is what the line's <c>IsVisible</c> binds,
        /// and the amber it is painted in is a colour <em>role</em> and never a brush
        /// (docs/app/design-system.md, invariant 10). It gates nothing else: a colliding or reserved
        /// trigger still saves.
        /// </summary>
        public bool IsTriggerAdvisory => _triggerStatus.Length > 0;

        /// <summary>Turns one co-trigger on or off, enforcing the device's cap (06 §5).</summary>
        public IRelayCommand<MacroCoTriggerViewModel> ToggleCoTriggerCommand { get; private set; } = null!;

        private string _triggerStatus = string.Empty;

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
        /// Re-reads both halves of the panel's one header row off the model, resolving the collision
        /// <b>once</b> for the two of them. Every caller goes through here rather than through
        /// <c>RefreshSlots</c> and <c>RefreshTrigger</c> in turn: the chips' rings and the status
        /// line are two renderings of one answer, and a second walk of the key's slots is a second
        /// answer waiting to differ from the first.
        /// </summary>
        private void RefreshSlotsAndTrigger()
        {
            var collision = ResolveCollisionStatus();

            RefreshSlots(collision);
            RefreshTrigger(collision);
        }

        /// <summary>
        /// Re-reads the strip off the model — the latches and the status. It writes nothing, which
        /// is the half of the "preserve on load" rule that matters: a file's right-hand or generic
        /// co-trigger is only ever <em>read</em> here.
        /// </summary>
        private void RefreshTrigger(MacroCollision collision)
        {
            RefreshCoTriggers();
            RefreshTriggerStatus(collision);
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

        /// <summary>
        /// Picks the one sentence the strip has to say, or none at all. The reserved rule comes first
        /// because it is the stronger fact: such a macro cannot fire at all, so naming a collision
        /// instead would answer a smaller question.
        /// </summary>
        private void RefreshTriggerStatus(MacroCollision collision)
        {
            if (!IsAvailable || _key is null)
            {
                TriggerStatus = string.Empty;

                return;
            }

            if ((_macro?.CoTriggerCount ?? 0) == 0 && IsReservedTrigger())
            {
                TriggerStatus = ReservedTriggerStatus;

                return;
            }

            TriggerStatus = collision.Status;
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
        /// answered <b>once</b> for the two surfaces that need it — the chips' amber rings and the
        /// strip's amber line. <see cref="Macro.CollidesWith"/> owns the comparison, so neither
        /// readout can disagree with <c>Validate()</c>.
        /// </summary>
        private MacroCollision ResolveCollisionStatus()
        {
            if (!IsAvailable || _key is not { } key)
            {
                return MacroCollision.None;
            }

            return _usesMacroSlots ? ResolveSlotCollision(key.Key) : ResolveFlatListCollision(key.Key);
        }

        /// <summary>
        /// The collision over the key's <b>persisted</b> slots, walked in slot order.
        ///
        /// <para><b>Every slot on either side of a clash is reported</b>, because the chips ring a
        /// pair: two macros that cannot be told apart are equally at fault, and marking only one of
        /// them would name a culprit where there is none. The <em>status</em> is narrower on purpose
        /// — it is about the macro under edit, and it names the first slot that one collides with,
        /// which is what makes "collides with slot 3" a sentence the user can act on.</para>
        /// </summary>
        private MacroCollision ResolveSlotCollision(KeyboardKey key)
        {
            var colliding = new List<int>();
            var status = string.Empty;

            for (var slot = Macro.MinMacroIndex; slot <= _persistedSlots; slot++)
            {
                if (key.GetMacro(slot) is not { } macro)
                {
                    continue;
                }

                for (var other = Macro.MinMacroIndex; other <= _persistedSlots; other++)
                {
                    if (other == slot || key.GetMacro(other) is not { } rival || !macro.CollidesWith(rival))
                    {
                        continue;
                    }

                    colliding.Add(slot);

                    if (status.Length == 0 && ReferenceEquals(macro, _macro))
                    {
                        status = BuildCollisionStatus(other);
                    }

                    break;
                }
            }

            return colliding.Count == 0 ? MacroCollision.None : new MacroCollision(status, colliding);
        }

        /// <summary>
        /// The same rule on a flat-list board (06 §1), where there is no slot to name and none to
        /// ring: two macros can share a trigger and a layer there just as readily, and the sentence
        /// is all the panel can offer.
        /// </summary>
        private MacroCollision ResolveFlatListCollision(KeyboardKey key)
        {
            if (_macro is not { } macro || _layout is not { } layout)
            {
                return MacroCollision.None;
            }

            foreach (var other in layout.FindMacros(_layer?.Index ?? Macro.UnassignedIndex, key.TriggerKey.Code))
            {
                if (!ReferenceEquals(other, macro) && macro.CollidesWith(other))
                {
                    return new MacroCollision(CollisionStatus, []);
                }
            }

            return MacroCollision.None;
        }

        /// <summary>
        /// One resolution of 06 §5's duplicate-trigger rule, as the two surfaces that draw it need
        /// it: the sentence for the macro <b>under edit</b>, and every slot of the key taking part
        /// in a clash. They are one object rather than two calls because the walk that finds one
        /// finds the other, and because a chip ringed amber beside a strip saying nothing — or the
        /// reverse — is exactly the disagreement a second walk would eventually produce.
        /// </summary>
        private sealed class MacroCollision
        {
            /// <summary>Nothing on this key collides with anything.</summary>
            public static MacroCollision None { get; } = new(string.Empty, []);

            /// <summary>The advisory for the macro under edit, or the empty string.</summary>
            public string Status { get; }

            private readonly IReadOnlyList<int> _slots;

            /// <summary>Builds one resolution; <paramref name="slots"/> is in persisted-slot order.</summary>
            public MacroCollision(string status, IReadOnlyList<int> slots)
            {
                Status = status;
                _slots = slots;
            }

            /// <summary>Whether <paramref name="slot"/> is one of the slots taking part.</summary>
            public bool Includes(int slot)
            {
                for (var index = 0; index < _slots.Count; index++)
                {
                    if (_slots[index] == slot)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
