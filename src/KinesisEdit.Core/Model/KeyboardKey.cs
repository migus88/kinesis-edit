using System.Collections.ObjectModel;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// One physical key position of a layer and everything the user can put on it — the legacy
    /// <c>TKBKey</c> (specs/05-key-model.md §1.3): the factory default action, the remap, the
    /// position token, the tap-and-hold assignment, the multi-modifier code, five macro slots, and
    /// the per-key LED colour. Mutable POCO: layer content is immutable after construction, so
    /// every edit is stored *on* this object and never by replacing it in the layer (§7.2).
    /// <para>
    /// Two kinds of write path exist. The editor paths (<see cref="Remap"/>,
    /// <see cref="SetTapAndHold(KeyDefinition, KeyDefinition, int)"/>,
    /// <see cref="TrySetMultiModifiers"/>, <see cref="AssignMacro"/>, <see cref="CopyFrom"/>) are
    /// what a user action goes through and refuse what the position cannot hold (§5.3). The
    /// tolerant load paths (<see cref="ApplyRemap"/>, <see cref="ApplyTapAndHold"/>,
    /// <see cref="ApplyMultiModifiers"/>, <see cref="SetMacro"/>) store whatever a field file
    /// carries and leave the reporting to <see cref="KeyboardLayout.Validate"/> — device limits are
    /// reported, never enforced (limits: 04 §5.3, 06 §6; the read-tolerantly rule of the project's
    /// CLAUDE.md).
    /// </para>
    /// </summary>
    public sealed partial class KeyboardKey
    {
        /// <summary>Key code of the Fn1 layer-shift key <c>fn1s</c> (§3.10) — a trigger-identity exception (§1.3).</summary>
        public const int Fn1ShiftKeyCode = 11167;

        /// <summary>Key code of the keypad layer-toggle key <c>keyt</c> (§3.10) — a trigger-identity exception (§1.3).</summary>
        public const int KeypadToggleKeyCode = 11166;

        /// <summary>Macro slots every key position owns (<c>Macro1..Macro5</c>, §1.3, 06 §1).</summary>
        public const int MacroSlotCount = 5;

        /// <summary>Ordinal of the position inside its layer — dense, unique per layer, stable across layers (§1.3, §7.4).</summary>
        public int Index { get; }

        /// <summary>False for positions that can never be remapped (§5.3, the "†" marker of §4).</summary>
        public bool CanEdit { get; }

        /// <summary>False for positions that cannot carry a macro (§5.3, the "*" marker of §4).</summary>
        public bool CanAssignMacro { get; }

        /// <summary>
        /// Whether the device this position belongs to offers multi-modifier codes at all
        /// (<see cref="DeviceDefinition.SupportsMultiModifiers"/> — Advantage360 only, §5.7;
        /// 11 §11.2). Unlike <see cref="CanEdit"/>/<see cref="CanAssignMacro"/> this is a device
        /// fact, not a geometry fact; it is projected onto the key so the editor path can refuse.
        /// </summary>
        public bool SupportsMultiModifiers { get; }

        /// <summary>Factory-default action of the position — what the key does unmodified (§1.3).</summary>
        public KeyDefinition OriginalKey { get; }

        /// <summary>
        /// The key naming the *physical position* in layout files (§1.3). Defaults to
        /// <see cref="OriginalKey"/> and differs from it only on the Advantage360, whose Keypad/Fn
        /// layers, thumb Fn keys, and pedal jack carry explicit position tokens (§4.7, §7.3).
        /// </summary>
        public KeyDefinition PositionKey { get; }

        /// <summary>The remapped action while <see cref="IsModified"/> is true, otherwise null (§1.3).</summary>
        public KeyDefinition? ModifiedKey { get; private set; }

        /// <summary>Whether a remap is in place (§1.3).</summary>
        public bool IsModified { get; private set; }

        /// <summary>
        /// The identity macro triggers match against (§1.3, §1.5; 06 §1, 04 §4.2): normally
        /// <see cref="PositionKey"/>, but <see cref="OriginalKey"/> for the Fn1 layer-shift key
        /// <c>fn1s</c> and the keypad layer-toggle key <c>keyt</c>.
        /// </summary>
        public KeyDefinition TriggerKey => IsLayerSwitchKey ? OriginalKey : PositionKey;

        /// <summary>
        /// The action the key currently performs (§1.3): <see cref="ModifiedKey"/> when modified —
        /// except the Fn1 layer-shift and keypad layer-toggle keys, which always report their
        /// original layer-switch action even when "modified" (§5.3).
        /// </summary>
        public KeyDefinition ModifiedOrOriginalKey =>
            IsModified && ModifiedKey is not null && !IsLayerSwitchKey ? ModifiedKey : OriginalKey;

        /// <summary>Action performed on a tap while <see cref="IsTapAndHold"/> is true (§1.3, §5.6).</summary>
        public KeyDefinition? TapAction { get; private set; }

        /// <summary>Action performed on a hold while <see cref="IsTapAndHold"/> is true (§1.3, §5.6).</summary>
        public KeyDefinition? HoldAction { get; private set; }

        /// <summary>
        /// Hold threshold in milliseconds (§1.3, §5.6). The default and the 1-999 bounds come from
        /// the device's <see cref="TapAndHoldCapability"/>; out-of-range values are stored and
        /// reported by <see cref="KeyboardLayout.Validate"/>, never refused (11 §11.1, 04 §5.3).
        /// </summary>
        public int TimingDelay { get; private set; }

        /// <summary>Whether a tap-and-hold assignment is in place (§1.3, §5.6).</summary>
        public bool IsTapAndHold { get; private set; }

        /// <summary>
        /// The raw 4-character multi-modifier code assigned to the position, or null (§1.3, §5.7;
        /// 04 §2.3). Stored verbatim in canonical lowercase, exactly as it is written back.
        /// </summary>
        public string? MultiModifiers { get; private set; }

        /// <summary>Whether a multi-modifier code is assigned (§5.7).</summary>
        public bool HasMultiModifiers => MultiModifiers is not null;

        /// <summary>
        /// Per-key LED colour (§1.3 <c>KeyColor</c>, specs/07-lighting.md §4); null means the key
        /// has no colour assigned. Lighting state survives <see cref="Reset"/>, which §1.3 defines
        /// as clearing the remap, tap-and-hold, multi-modifiers, and macros only.
        /// </summary>
        public KeyColor? KeyColor { get; set; }

        private bool IsLayerSwitchKey => OriginalKey.Code is Fn1ShiftKeyCode or KeypadToggleKeyCode;

        /// <summary>
        /// Creates the runtime key for a geometry position (§1.3, §1.5: layer builders work on
        /// copies of key-table entries, never the shared instances — the entries themselves are
        /// immutable records here).
        /// </summary>
        /// <param name="position">The geometry position supplying index and edit/macro permissions.</param>
        /// <param name="originalKey">The layer's factory-default action for the position.</param>
        /// <param name="positionKey">The key naming the position; null means <paramref name="originalKey"/> (§1.3).</param>
        /// <param name="supportsMultiModifiers">The device's <see cref="DeviceDefinition.SupportsMultiModifiers"/> (§5.7).</param>
        /// <param name="usesMacroSlots">Whether the device stores macros in per-key slots (06 §1); false on the Advantage360.</param>
        public KeyboardKey(
            KeyPosition position,
            KeyDefinition originalKey,
            KeyDefinition? positionKey = null,
            bool supportsMultiModifiers = false,
            bool usesMacroSlots = true)
        {
            ArgumentNullException.ThrowIfNull(position);
            ArgumentNullException.ThrowIfNull(originalKey);

            Index = position.Index;
            CanEdit = position.CanEdit;
            CanAssignMacro = position.CanAssignMacro;
            SupportsMultiModifiers = supportsMultiModifiers;
            UsesMacroSlots = usesMacroSlots;
            OriginalKey = originalKey;
            PositionKey = positionKey ?? originalKey;
            _macrosView = new ReadOnlyCollection<Macro?>(_macros);
        }

        /// <summary>
        /// The editor remap path (§1.3, §1.5; 04 §2.1): sets <see cref="ModifiedKey"/> and
        /// <see cref="IsModified"/> when <paramref name="newKey"/>'s code differs from the original,
        /// and clears them when it equals the original. A locked position accepts nothing and always
        /// returns false, including for a remap back to its own original key (§5.3).
        /// <para>
        /// Assigning it clears the tap-and-hold assignment and the multi-modifier code, per the
        /// one-rule-per-position rule described on <see cref="ClearSingleKeyRules"/>; 11 §11.1
        /// states this leg outright ("a plain remap of the key likewise clears its tap-and-hold
        /// configuration").
        /// </para>
        /// <para>
        /// The Fn1 layer-shift and keypad layer-toggle keys are remapped like any other position:
        /// §5.3 says they "always act/report as their original layer-switch action even when
        /// 'modified'", so the remap is stored — <see cref="ModifiedOrOriginalKey"/> is what ignores
        /// it — and 04 §4.3 still writes the line, which 04 §4.2 reads back into the same state.
        /// </para>
        /// </summary>
        public bool Remap(KeyDefinition newKey)
        {
            ArgumentNullException.ThrowIfNull(newKey);

            if (!CanEdit)
            {
                return false;
            }

            ClearSingleKeyRules();

            if (newKey.Code == OriginalKey.Code)
            {
                return true;
            }

            ModifiedKey = newKey;
            IsModified = true;

            return true;
        }

        /// <summary>
        /// The tolerant load path for a remap line (04 §4.2: "rules are applied to the addressed
        /// key"). Unlike <see cref="Remap"/> it stores the remap even on a locked position, so a
        /// field file that carries such a line still loads and
        /// <see cref="KeyboardLayout.Validate"/> reports
        /// <see cref="ModelViolationKind.EditOnLockedKey"/> instead of the line being dropped
        /// silently. A remap to the original key still clears the remap (04 §2.1).
        /// </summary>
        public void ApplyRemap(KeyDefinition newKey)
        {
            ArgumentNullException.ThrowIfNull(newKey);

            if (newKey.Code == OriginalKey.Code)
            {
                ClearRemap();

                return;
            }

            ModifiedKey = newKey;
            IsModified = true;
        }

        /// <summary>Clears the remap, returning the position to its factory default (§1.3).</summary>
        public void ClearRemap()
        {
            ModifiedKey = null;
            IsModified = false;
        }

        /// <summary>
        /// The editor tap-and-hold path with an explicit hold threshold (§1.3, §5.6; 04 §2.2). The
        /// delay is stored verbatim: values outside the device's 1-999 ms range are reported by
        /// <see cref="KeyboardLayout.Validate"/>, not refused (11 §11.1). Returns false for a locked
        /// position, which never produces a layout line (§5.3). Assigning it clears the remap and
        /// the multi-modifier code (<see cref="ClearSingleKeyRules"/>).
        /// </summary>
        public bool SetTapAndHold(KeyDefinition tapAction, KeyDefinition holdAction, int timingDelayMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(tapAction);
            ArgumentNullException.ThrowIfNull(holdAction);

            if (!CanEdit)
            {
                return false;
            }

            ClearSingleKeyRules();
            ApplyTapAndHold(tapAction, holdAction, timingDelayMilliseconds);

            return true;
        }

        /// <summary>
        /// The tolerant load path for a tap-and-hold line (04 §4.2, §2.2). Unlike
        /// <see cref="SetTapAndHold(KeyDefinition, KeyDefinition, int)"/> it stores the assignment
        /// even on a locked position, which <see cref="KeyboardLayout.Validate"/> then reports as
        /// <see cref="ModelViolationKind.EditOnLockedKey"/>.
        /// </summary>
        public void ApplyTapAndHold(KeyDefinition tapAction, KeyDefinition holdAction, int timingDelayMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(tapAction);
            ArgumentNullException.ThrowIfNull(holdAction);

            TapAction = tapAction;
            HoldAction = holdAction;
            TimingDelay = timingDelayMilliseconds;
            IsTapAndHold = true;
        }

        /// <summary>
        /// Assigns a tap-and-hold using the device's default hold threshold
        /// (<see cref="TapAndHoldCapability.DefaultDelayMilliseconds"/> — 250 ms, 150 ms on the
        /// Advantage360; specs/11-feature-dialogs.md §11.1). Devices without the feature state no
        /// default, so 0 is stored and <see cref="KeyboardLayout.Validate"/> reports the assignment.
        /// </summary>
        public bool SetTapAndHold(KeyDefinition tapAction, KeyDefinition holdAction, TapAndHoldCapability capability)
        {
            ArgumentNullException.ThrowIfNull(capability);

            return SetTapAndHold(tapAction, holdAction, capability.DefaultDelayMilliseconds ?? 0);
        }

        /// <summary>Clears the tap-and-hold assignment (§1.3, §5.6).</summary>
        public void ClearTapAndHold()
        {
            TapAction = null;
            HoldAction = null;
            TimingDelay = 0;
            IsTapAndHold = false;
        }

        /// <summary>
        /// The editor multi-modifier path (§5.7; 04 §2.3; 11 §11.2). Only the 11 accepted codes are
        /// taken, case-insensitively and normalised to lowercase; anything else — including a single
        /// modifier, which is an ordinary remap — leaves the key untouched and returns false, as
        /// does a locked position (§5.3) and a device without the feature
        /// (<see cref="SupportsMultiModifiers"/>: the dialog of 11 §11.2 is "Advantage360 only").
        /// Assigning it clears the remap and the tap-and-hold assignment
        /// (<see cref="ClearSingleKeyRules"/>) — 04 §2.3 calls a multi-modifier "a remap whose
        /// output is a combination of modifiers", so the two are one and the same rule.
        /// </summary>
        public bool TrySetMultiModifiers(string? code)
        {
            if (!CanEdit || !SupportsMultiModifiers || !MultiModifierCodes.TryNormalize(code, out var normalized))
            {
                return false;
            }

            ClearSingleKeyRules();
            MultiModifiers = normalized;

            return true;
        }

        /// <summary>
        /// The tolerant load path for a multi-modifier line (04 §4.2, §2.3). Unlike
        /// <see cref="TrySetMultiModifiers"/> it stores the code on a locked position and on a
        /// device that does not offer the feature, which <see cref="KeyboardLayout.Validate"/>
        /// reports as <see cref="ModelViolationKind.EditOnLockedKey"/> and
        /// <see cref="ModelViolationKind.MultiModifiersNotSupported"/>. Returns false only when
        /// <paramref name="code"/> is not one of the 11 codes — that is a parse fact, not a device
        /// limit, and 04 §1.3 defines a line as a multi-modifier line by exactly that test.
        /// </summary>
        public bool ApplyMultiModifiers(string? code)
        {
            if (!MultiModifierCodes.TryNormalize(code, out var normalized))
            {
                return false;
            }

            MultiModifiers = normalized;

            return true;
        }

        /// <summary>Clears the multi-modifier code (§5.7).</summary>
        public void ClearMultiModifiers()
        {
            MultiModifiers = null;
        }

        /// <summary>
        /// Clears the remap, the tap-and-hold assignment, the multi-modifier code, and all macros
        /// (§1.3). The per-key LED colour is lighting state and is left alone.
        /// </summary>
        public void Reset()
        {
            ClearRemap();
            ClearTapAndHold();
            ClearMultiModifiers();
            ClearMacros();
        }

        /// <summary>
        /// Copies key data, macros, or both from another position (§1.3). Macros are deep-copied
        /// (§1.5: assignment paths work on copies, never shared instances); geometry facts — index,
        /// original/position keys, edit and macro permissions — are never copied.
        /// <para>
        /// This is a user action ("Copying between key positions", §1.3), so the key-data half is an
        /// editor path and a locked position takes nothing (§5.3: it must never produce a layout
        /// line). The macro half deliberately keeps copying onto a position with
        /// <see cref="CanAssignMacro"/> false, because that state is already visible —
        /// <see cref="KeyboardLayout.Validate"/> reports
        /// <see cref="ModelViolationKind.MacroOnRestrictedKey"/> for it — whereas an unchecked
        /// key-data copy onto a locked position would have been silent.
        /// </para>
        /// <para>
        /// The key-data half transfers the action the source <em>performs</em>, not its stored remap
        /// fields, and re-derives the target's remap from it — see <see cref="CopyAssignmentFrom"/>
        /// for why the distinction matters and why a tap-and-hold or multi-modifier source is exempt.
        /// </para>
        /// </summary>
        public void CopyFrom(KeyboardKey other, KeyCopyScopes scope)
        {
            ArgumentNullException.ThrowIfNull(other);

            if ((scope & KeyCopyScopes.KeyData) != 0)
            {
                CopyKeyDataFrom(other);
            }

            if ((scope & KeyCopyScopes.Macros) != 0)
            {
                CopyMacrosFrom(other);
            }
        }

        /// <summary>
        /// The three single-key rules of 04 §2 — remap, tap-and-hold, multi-modifier — share one
        /// slot on a position: 04 §4.3 writes at most one line per key, "tap-and-hold line, else
        /// multi-modifier line, else remap line if the key is modified". A position holding two of
        /// them would silently lose the lower-precedence one on the next save, so every editor path
        /// clears the other two before it writes and the model always round-trips. 11 §11.1 states
        /// one leg of this outright — "a plain remap of the key likewise clears its tap-and-hold
        /// configuration" — and 04 §2.3 makes remap and multi-modifier the same rule to begin with.
        /// The tolerant load paths deliberately do not clear: a field file carrying two lines for
        /// one position keeps both, and <see cref="KeyboardLayout.Validate"/> reports
        /// <see cref="ModelViolationKind.ConflictingSingleKeyRules"/>.
        /// </summary>
        private void ClearSingleKeyRules()
        {
            ClearRemap();
            ClearTapAndHold();
            ClearMultiModifiers();
        }

        private void CopyKeyDataFrom(KeyboardKey other)
        {
            if (!CanEdit)
            {
                return;
            }

            ModifiedKey = other.ModifiedKey;
            IsModified = other.IsModified;
            TapAction = other.TapAction;
            HoldAction = other.HoldAction;
            TimingDelay = other.TimingDelay;
            IsTapAndHold = other.IsTapAndHold;

            // The copy replaces the target's key data outright, so every field is taken from the
            // source — filtered by what the target's device can hold (11 §11.2). Leaving a stale
            // code behind on a device without the feature would let this editor path manufacture
            // the conflict the others prevent (see ClearSingleKeyRules), and clearing loses nothing
            // unreported: such a code already raises MultiModifiersNotSupported.
            MultiModifiers = SupportsMultiModifiers ? other.MultiModifiers : null;

            CopyAssignmentFrom(other);
        }

        /// <summary>
        /// Re-derives the remap of a plain copy through <see cref="Remap"/>'s two rules instead of
        /// taking <see cref="ModifiedKey"/>/<see cref="IsModified"/> at face value.
        /// <para>
        /// What the user asked to copy is the action the source <em>performs</em> —
        /// <see cref="ModifiedOrOriginalKey"/>, the readout every cap caption and inspector line is
        /// drawn from — not the source's modified flag. The two coincide only when the source
        /// happens to carry a remap, so copying the raw pair writes "no remap" whenever the source
        /// is untouched and leaves the target on its own factory action: a silent no-op, or a silent
        /// erasure when the target was remapped. The same action is also a different remap relative
        /// to <em>this</em> position — none at all when the two positions share a default, which is
        /// what <see cref="Remap"/> clears and a raw copy would leave as a redundant rule inflating
        /// the modified count and writing a pointless layout line (04 §2.1).
        /// </para>
        /// <para>
        /// A source holding a tap-and-hold or a multi-modifier is left alone: that rule has just
        /// been copied above, and its resolved action is only the key's untouched original, so
        /// storing it as a remap would plant a second single-key rule beside the copied one — the
        /// <see cref="ModelViolationKind.ConflictingSingleKeyRules"/> that "one rule per position"
        /// exists to prevent (see <see cref="ClearSingleKeyRules"/>; 04 §4.3 writes one line per
        /// key).
        /// </para>
        /// </summary>
        private void CopyAssignmentFrom(KeyboardKey other)
        {
            if (other.IsTapAndHold || other.HasMultiModifiers)
            {
                return;
            }

            var assignment = other.ModifiedOrOriginalKey;

            if (assignment.Code == OriginalKey.Code)
            {
                ClearRemap();

                return;
            }

            ModifiedKey = assignment;
            IsModified = true;
        }
    }
}
