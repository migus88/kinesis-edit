using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One entry of the Special Actions menu (specs/12-savant-elite.md §6): its stable id, the
    /// caption the menu shows, the editor modes it is offered in, and what applying it does.
    /// Instances are catalog data — created once by <see cref="PedalSpecialActions"/> and shared —
    /// so the keystrokes they carry are handed out as fresh clones and never as the shared
    /// prototypes (<see cref="Keystroke"/> is mutable).
    /// </summary>
    public sealed class PedalSpecialAction
    {
        /// <summary>Stable kebab-case identifier, e.g. <c>left-mouse-double-click</c>; the key <see cref="PedalSpecialActions.Find"/> takes.</summary>
        public string Id { get; }

        /// <summary>The §6 menu caption.</summary>
        public string Caption { get; }

        /// <summary>The editor modes the item is offered in (§6 "Mode" column).</summary>
        public PedalSpecialActionModes Modes { get; }

        /// <summary>What applying the item does to the entry being edited.</summary>
        public PedalSpecialActionKind Kind { get; }

        /// <summary>Number of keystrokes <see cref="CreateKeystrokes"/> produces; 0 for the two editor-mode items.</summary>
        public int KeystrokeCount => _prototypes.Count;

        private readonly IReadOnlyList<Keystroke> _prototypes;

        internal PedalSpecialAction(
            string id,
            string caption,
            PedalSpecialActionModes modes,
            PedalSpecialActionKind kind,
            IReadOnlyList<Keystroke> prototypes)
        {
            Id = id;
            Caption = caption;
            Modes = modes;
            Kind = kind;
            _prototypes = prototypes;
        }

        /// <summary>
        /// The keystrokes this item emits (§6 "Emits" column), as a fresh, independently mutable
        /// copy on every call — the caller owns them and the catalog stays pristine. Empty for
        /// <see cref="PedalSpecialActionKind.DifferentPressRelease"/> and
        /// <see cref="PedalSpecialActionKind.ToggleWinModifier"/>, which carry no content.
        /// </summary>
        public IReadOnlyList<Keystroke> CreateKeystrokes()
        {
            var keystrokes = new Keystroke[_prototypes.Count];

            for (var i = 0; i < _prototypes.Count; i++)
            {
                keystrokes[i] = _prototypes[i].Clone();
            }

            return keystrokes;
        }

        /// <summary>Whether the item is offered while the editor is in <paramref name="mode"/> (§6).</summary>
        public bool SupportsMode(PedalInputMode mode)
        {
            return mode switch
            {
                PedalInputMode.Single => (Modes & PedalSpecialActionModes.Single) != 0,
                PedalInputMode.Macro => (Modes & PedalSpecialActionModes.Macro) != 0,
                _ => false
            };
        }
    }
}
