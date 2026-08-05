using System.Collections.ObjectModel;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// One keystroke recorded by the capture layer: the key-table entry that was pressed, the
    /// physical position it came from, and the modifiers held at that moment
    /// (specs/10-apps-and-ui.md, "Keyboard-input capture").
    /// <para>
    /// The type enforces the two modifier rules of specs/05-key-model.md §5.1 itself, so no
    /// caller can build an invalid keystroke: the active-modifier set is deduplicated by key
    /// code, and a modifier string is <b>never</b> attached to a key that is itself a modifier —
    /// for such a key <see cref="HeldModifiers"/> is always empty. The held-modifier list is
    /// stored as a defensive copy, so later changes to the caller's list are not observed.
    /// </para>
    /// </summary>
    public sealed record CapturedKeystroke
    {
        private static readonly IReadOnlyList<KeyDefinition> _noModifiers =
            new ReadOnlyCollection<KeyDefinition>(Array.Empty<KeyDefinition>());

        private static readonly IReadOnlySet<int> _modifierKeyCodes = BuildModifierKeyCodes();

        /// <summary>The key-table entry that was captured (specs/05-key-model.md §3).</summary>
        public required KeyDefinition Key { get; init; }

        /// <summary>The physical position the keystroke came from.</summary>
        public required PhysicalKeyCode PhysicalKey { get; init; }

        /// <summary>
        /// The modifiers held while <see cref="Key"/> was pressed, in press order, deduplicated
        /// by key code (specs/05-key-model.md §5.1). Always empty when <see cref="Key"/> is
        /// itself a modifier — §5.1 forbids attaching a modifier string to a modifier key.
        /// </summary>
        public IReadOnlyList<KeyDefinition> HeldModifiers
        {
            get => IsModifierKey(Key) ? _noModifiers : _heldModifiers;
            init => _heldModifiers = Copy(value);
        }

        private readonly IReadOnlyList<KeyDefinition> _heldModifiers = _noModifiers;

        private static bool IsModifierKey(KeyDefinition key)
        {
            return _modifierKeyCodes.Contains(key.Code);
        }

        private static IReadOnlyList<KeyDefinition> Copy(IReadOnlyList<KeyDefinition>? modifiers)
        {
            if (modifiers is null || modifiers.Count == 0)
            {
                return _noModifiers;
            }

            var seenCodes = new HashSet<int>(modifiers.Count);
            var copy = new List<KeyDefinition>(modifiers.Count);

            foreach (var modifier in modifiers)
            {
                if (seenCodes.Add(modifier.Code))
                {
                    copy.Add(modifier);
                }
            }

            return new ReadOnlyCollection<KeyDefinition>(copy);
        }

        private static HashSet<int> BuildModifierKeyCodes()
        {
            // The modifier key set of specs/05-key-model.md §5.1, in the order the spec lists it.
            return new HashSet<int>
            {
                VirtualKey.Menu,
                VirtualKey.LeftMenu,
                VirtualKey.RightMenu,
                VirtualKey.Shift,
                VirtualKey.LeftShift,
                VirtualKey.RightShift,
                VirtualKey.Control,
                VirtualKey.LeftControl,
                VirtualKey.RightControl,
                VirtualKey.LeftWin,
                VirtualKey.RightWin
            };
        }
    }
}
