using Avalonia.Input;
using KinesisEdit.Core.Geometry.Visual;

// Avalonia has a NavigationDirection of its own (Next/Previous/First/Last/…, the tab-order one).
// This grammar is about the board's physical geometry, so the alias pins the Core type — and the
// clash is a useful reminder that "not tab order" is the whole point of the arrow keys here.
using NavigationDirection = KinesisEdit.Core.Geometry.Visual.NavigationDirection;

namespace KinesisEdit.Input
{
    /// <summary>
    /// The editor's keyboard grammar as a pure lookup: a key plus its modifiers in, an
    /// <see cref="EditorShortcut"/> out. Transcribed from docs/design/mockups.md <c>2b</c>
    /// ("↑↓←→ move key selection across the physical grid · ⌥1–5 jump to layer n · ⌘F focus the
    /// token search · ⌘S / ⌘W save · return to the dashboard").
    /// <para>
    /// <b>It decides nothing and touches nothing</b> — no view, no view model, no capture service.
    /// That is what makes the whole table testable as data, on any host, which matters because the
    /// ⌘ half of it differs per platform and a test that only passes on a Mac is a test that fails
    /// in CI.
    /// </para>
    /// </summary>
    public static class EditorShortcuts
    {
        /// <summary>How many layers the ⌥n table reaches (<c>2b</c>: "⌥1–5").</summary>
        public const int MaxLayerNumber = 5;

        /// <summary>
        /// Which modifier ⌘ is on this platform: <see cref="KeyModifiers.Meta"/> on macOS,
        /// <see cref="KeyModifiers.Control"/> everywhere else (docs/design/handoff.md
        /// § "Interactions": "map ⌘→Ctrl and ⌥→Alt on Windows/Linux").
        /// </summary>
        public static KeyModifiers CommandModifier(bool isMacOs)
        {
            return isMacOs ? KeyModifiers.Meta : KeyModifiers.Control;
        }

        /// <summary>
        /// Maps one key event to the intent it expresses, or <see cref="EditorShortcut.None"/>.
        /// <para>
        /// <b>The modifier set is matched exactly</b>, never tested with a flag mask: <c>⌘⇧S</c> is
        /// not <c>⌘S</c>, and an arrow with any modifier held is not a plain arrow. A shortcut that
        /// fires on a superset silently steals combinations the user meant for something else.
        /// </para>
        /// <para>
        /// ⌥ is <see cref="KeyModifiers.Alt"/> on every platform — Alt <em>is</em> Option on macOS —
        /// so only ⌘ has a platform branch, and <paramref name="isMacOs"/> is a parameter rather
        /// than a read of <c>OperatingSystem.IsMacOS()</c> for the same reason
        /// <see cref="ViewModels.KeyCaption.For"/> takes one.
        /// </para>
        /// </summary>
        public static EditorShortcut Map(Key key, KeyModifiers modifiers, bool isMacOs)
        {
            if (modifiers == KeyModifiers.None)
            {
                return key switch
                {
                    Key.Up => EditorShortcut.MoveUp,
                    Key.Down => EditorShortcut.MoveDown,
                    Key.Left => EditorShortcut.MoveLeft,
                    Key.Right => EditorShortcut.MoveRight,
                    _ => EditorShortcut.None
                };
            }

            if (modifiers == KeyModifiers.Alt)
            {
                // The digit row only. A keypad digit is a different physical key and the design
                // names the row it draws on the layer switcher ("⌥1–5", mockup 1f).
                return key switch
                {
                    Key.D1 => EditorShortcut.SelectLayer1,
                    Key.D2 => EditorShortcut.SelectLayer2,
                    Key.D3 => EditorShortcut.SelectLayer3,
                    Key.D4 => EditorShortcut.SelectLayer4,
                    Key.D5 => EditorShortcut.SelectLayer5,
                    _ => EditorShortcut.None
                };
            }

            if (modifiers == CommandModifier(isMacOs))
            {
                return key switch
                {
                    Key.F => EditorShortcut.FocusSearch,
                    Key.S => EditorShortcut.Save,
                    Key.W => EditorShortcut.GoHome,
                    _ => EditorShortcut.None
                };
            }

            return EditorShortcut.None;
        }

        /// <summary>
        /// The board direction one of the four move intents names, or
        /// <see cref="NavigationDirection.None"/> for anything else.
        /// <para>
        /// It exists so no caller has to spell the four-way <c>switch</c> itself and risk passing
        /// <see cref="NavigationDirection.None"/> — which <see cref="KeyAdjacency.Next"/> throws on
        /// (docs/app/domain-data.md, "Spatial navigation").
        /// </para>
        /// </summary>
        public static NavigationDirection ToDirection(EditorShortcut shortcut)
        {
            return shortcut switch
            {
                EditorShortcut.MoveUp => NavigationDirection.Up,
                EditorShortcut.MoveDown => NavigationDirection.Down,
                EditorShortcut.MoveLeft => NavigationDirection.Left,
                EditorShortcut.MoveRight => NavigationDirection.Right,
                _ => NavigationDirection.None
            };
        }

        /// <summary>
        /// The 1-based layer number one of the five ⌥n intents names, or 0 for anything else.
        /// A number past the open device's layer count is a no-op, not an error — the grammar is
        /// the same on a two-layer Freestyle Edge as on a five-layer Advantage 360.
        /// </summary>
        public static int ToLayerNumber(EditorShortcut shortcut)
        {
            return shortcut switch
            {
                EditorShortcut.SelectLayer1 => 1,
                EditorShortcut.SelectLayer2 => 2,
                EditorShortcut.SelectLayer3 => 3,
                EditorShortcut.SelectLayer4 => 4,
                EditorShortcut.SelectLayer5 => 5,
                _ => 0
            };
        }
    }
}
