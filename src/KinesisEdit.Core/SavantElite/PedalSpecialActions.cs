using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The Special Actions menu of specs/12-savant-elite.md §6 as domain data: five ordered groups
    /// of ordered items, each resolving its keys through <see cref="PedalTokenMap"/> at type-load
    /// time so a registry regression fails loudly instead of silently emitting nothing.
    /// <para>
    /// Two deliberate departures from the legacy menu. First, <b>no OS gating</b>: §6 marks several
    /// items "Windows only" / "macOS only", but the file tokens are identical cross-platform (§4.4)
    /// and a pedal programmed on one machine is used on another, so every entry is offered
    /// everywhere and the ones that differ per platform appear twice with the platform named in the
    /// caption (Cut (Ctrl + X) *and* Cut (Cmd + X)). Second, <c>shutdn</c> is not here — §4.4 calls
    /// it "defined but not exposed in the menu", and it parses like any other token.
    /// </para>
    /// </summary>
    public static class PedalSpecialActions
    {
        private const string MouseActionsGroupName = "MOUSE ACTIONS";
        private const string EditingToolsGroupName = "EDITING TOOLS";
        private const string MediaControlsGroupName = "MEDIA CONTROLS";
        private const string ShortcutsGroupName = "COMMONLY USED SHORTCUTS";
        private const string PedalResponseGroupName = "PEDAL RESPONSE";

        /// <summary>The generic Ctrl modifier of the Windows editing shortcuts (§6).</summary>
        private const MacroModifiers Ctrl = MacroModifiers.Control;

        /// <summary>
        /// The macOS Cmd modifier (§6). It is the Win key: §4.4 lists <c>win</c> as the pedal's only
        /// Win/Cmd token and notes that macOS "displays cmd but saves win".
        /// </summary>
        private const MacroModifiers Cmd = MacroModifiers.LeftWin;

        private const MacroModifiers Alt = MacroModifiers.Alt;
        private const MacroModifiers Shift = MacroModifiers.Shift;

        private const PedalSpecialActionModes BothModes =
            PedalSpecialActionModes.Single | PedalSpecialActionModes.Macro;

        private const PedalSpecialActionModes SingleOnly = PedalSpecialActionModes.Single;
        private const PedalSpecialActionModes MacroOnly = PedalSpecialActionModes.Macro;

        /// <summary>The §6 menu: every group in menu order, every item in menu order.</summary>
        public static IReadOnlyList<PedalSpecialActionGroup> Groups { get; }

        private static readonly Dictionary<string, PedalSpecialAction> _actionsById;

        static PedalSpecialActions()
        {
            Groups =
            [
                BuildMouseActions(),
                BuildEditingTools(),
                BuildMediaControls(),
                BuildShortcuts(),
                BuildPedalResponse()
            ];

            _actionsById = new Dictionary<string, PedalSpecialAction>(StringComparer.Ordinal);

            foreach (var group in Groups)
            {
                foreach (var action in group.Actions)
                {
                    Validate(action);

                    if (!_actionsById.TryAdd(action.Id, action))
                    {
                        throw new InvalidOperationException($"Duplicate special-action id \"{action.Id}\".");
                    }
                }
            }
        }

        /// <summary>The item with <paramref name="id"/>, or null when the catalog has no such item.</summary>
        public static PedalSpecialAction? Find(string id)
        {
            return id is null ? null : _actionsById.GetValueOrDefault(id);
        }

        private static PedalSpecialActionGroup BuildMouseActions()
        {
            return new PedalSpecialActionGroup(
                MouseActionsGroupName,
                [
                    CreateKeyAction("left-mouse-click", "Left Mouse Click", BothModes, "lmouse"),
                    CreateKeyAction("left-mouse-double-click", "Left Mouse Double Click", MacroOnly, "lmouse", "d125", "lmouse"),
                    CreateKeyAction("middle-mouse-click", "Middle Mouse Click", BothModes, "mmouse"),
                    CreateKeyAction("right-mouse-click", "Right Mouse Click", BothModes, "rmouse")
                ]);
        }

        private static PedalSpecialActionGroup BuildEditingTools()
        {
            return new PedalSpecialActionGroup(
                EditingToolsGroupName,
                [
                    CreateChordAction("cut-ctrl", "Cut (Ctrl + X)", Ctrl, "x"),
                    CreateChordAction("copy-ctrl", "Copy (Ctrl + C)", Ctrl, "c"),
                    CreateChordAction("paste-ctrl", "Paste (Ctrl + V)", Ctrl, "v"),
                    CreateChordAction("select-all-ctrl", "Select All (Ctrl + A)", Ctrl, "a"),
                    CreateChordAction("undo-ctrl", "Undo (Ctrl + Z)", Ctrl, "z"),
                    CreateChordAction("cut-cmd", "Cut (Cmd + X)", Cmd, "x"),
                    CreateChordAction("copy-cmd", "Copy (Cmd + C)", Cmd, "c"),
                    CreateChordAction("paste-cmd", "Paste (Cmd + V)", Cmd, "v"),
                    CreateChordAction("select-all-cmd", "Select All (Cmd + A)", Cmd, "a"),
                    CreateChordAction("undo-cmd", "Undo (Cmd + Z)", Cmd, "z")
                ]);
        }

        private static PedalSpecialActionGroup BuildMediaControls()
        {
            // §6 restricts every media key to single mode.
            return new PedalSpecialActionGroup(
                MediaControlsGroupName,
                [
                    CreateKeyAction("mute", "Mute", SingleOnly, "mute"),
                    CreateKeyAction("volume-up", "Volume Up", SingleOnly, "vol+"),
                    CreateKeyAction("volume-down", "Volume Down", SingleOnly, "vol-"),
                    CreateKeyAction("play-pause", "Play / Pause", SingleOnly, "play"),
                    CreateKeyAction("previous-track", "Previous Track", SingleOnly, "prev"),
                    CreateKeyAction("next-track", "Next Track", SingleOnly, "next")
                ]);
        }

        /// <summary>
        /// §6's COMMONLY USED SHORTCUTS, in the table's own order — including "Windows Combination",
        /// which the spec lists here (between Calculator and Cmd + Tab) rather than under PEDAL
        /// RESPONSE. The only insertions are the second platform spelling of the browser items,
        /// each kept next to its spec sibling because there is no OS gating.
        /// </summary>
        private static PedalSpecialActionGroup BuildShortcuts()
        {
            return new PedalSpecialActionGroup(
                ShortcutsGroupName,
                [
                    CreateChordAction("web-browser-forward-alt", "Web Browser Forward (Alt + →)", Alt, "right"),
                    CreateChordAction("web-browser-back-alt", "Web Browser Back (Alt + ←)", Alt, "left"),
                    CreateChordAction("web-browser-forward-cmd", "Web Browser Forward (Cmd + →)", Cmd, "right"),
                    CreateChordAction("web-browser-back-cmd", "Web Browser Back (Cmd + ←)", Cmd, "left"),
                    CreateChordAction("alt-tab", "Alt + Tab", Alt, "tab"),
                    CreateChordAction("ctrl-alt-delete", "Ctrl + Alt + Delete", Ctrl | Alt, "delete"),
                    CreateKeyAction("calculator", "Calculator", SingleOnly, "calc"),
                    CreateModeAction(
                        "hold-win-modifier",
                        "Windows Combination (Win + …)",
                        PedalSpecialActionKind.ToggleWinModifier),
                    CreateChordAction("cmd-tab", "Cmd + Tab", Cmd, "tab"),
                    CreateChordAction("snip-to-file", "Snip to File (Shift + Cmd + 4)", Shift | Cmd, "4"),
                    CreateChordAction("snip-to-clipboard", "Snip to Clipboard (Shift + Ctrl + Cmd + 4)", Shift | Ctrl | Cmd, "4"),
                    CreateChordAction("force-quit", "Force Quit (Cmd + Alt + Esc)", Cmd | Alt, "escape")
                ]);
        }

        private static PedalSpecialActionGroup BuildPedalResponse()
        {
            return new PedalSpecialActionGroup(
                PedalResponseGroupName,
                [
                    CreateKeyAction("slow-output", "Slow Output (speed1)", MacroOnly, "speed1"),
                    CreateKeyAction("default-output", "Default Output (speed3)", MacroOnly, "speed3"),
                    CreateKeyAction("fast-output", "Fast Output (speed5)", MacroOnly, "speed5"),
                    CreateKeyAction("short-delay", "Short Delay (125 ms)", MacroOnly, "d125"),
                    CreateKeyAction("long-delay", "Long Delay (500 ms)", MacroOnly, "d500"),
                    CreateModeAction(
                        "different-press-release",
                        "Different Press & Release",
                        PedalSpecialActionKind.DifferentPressRelease)
                ]);
        }

        /// <summary>An item that emits unmodified keys — one for a plain key, three for the double click.</summary>
        private static PedalSpecialAction CreateKeyAction(
            string id,
            string caption,
            PedalSpecialActionModes modes,
            params string[] tokens)
        {
            return new PedalSpecialAction(
                id,
                caption,
                modes,
                PedalSpecialActionKind.Keystrokes,
                ResolveKeystrokes(MacroModifiers.None, tokens));
        }

        /// <summary>An item that emits one modified key — every §6 shortcut is macro-only.</summary>
        private static PedalSpecialAction CreateChordAction(
            string id,
            string caption,
            MacroModifiers modifiers,
            string token)
        {
            return new PedalSpecialAction(
                id,
                caption,
                MacroOnly,
                PedalSpecialActionKind.Keystrokes,
                ResolveKeystrokes(modifiers, token));
        }

        /// <summary>An item that changes the editor rather than the entry; it carries no keystrokes.</summary>
        private static PedalSpecialAction CreateModeAction(string id, string caption, PedalSpecialActionKind kind)
        {
            return new PedalSpecialAction(id, caption, MacroOnly, kind, []);
        }

        private static IReadOnlyList<Keystroke> ResolveKeystrokes(MacroModifiers modifiers, params string[] tokens)
        {
            var keystrokes = new Keystroke[tokens.Length];

            for (var i = 0; i < tokens.Length; i++)
            {
                keystrokes[i] = new Keystroke(ResolveKey(tokens[i]), modifiers);
            }

            return keystrokes;
        }

        private static KeyDefinition ResolveKey(string token)
        {
            return PedalTokenMap.Resolve(token)
                   ?? throw new InvalidOperationException(
                       $"The Special Actions catalog needs the pedal token \"{token}\" (specs/12-savant-elite.md §6), "
                       + "which the key registry does not resolve.");
        }

        /// <summary>
        /// Catalog sanity, checked once at type load: a keystroke item must emit something, an item
        /// offered in single mode must emit exactly one key (a single-action line holds one token,
        /// §4.3), and an editor-mode item must emit nothing.
        /// </summary>
        private static void Validate(PedalSpecialAction action)
        {
            if (action.Kind != PedalSpecialActionKind.Keystrokes)
            {
                if (action.KeystrokeCount != 0)
                {
                    throw new InvalidOperationException($"Special action \"{action.Id}\" is an editor mode and must emit no keystrokes.");
                }

                return;
            }

            if (action.KeystrokeCount == 0)
            {
                throw new InvalidOperationException($"Special action \"{action.Id}\" emits no keystrokes.");
            }

            if ((action.Modes & PedalSpecialActionModes.Single) != 0 && action.KeystrokeCount != 1)
            {
                throw new InvalidOperationException($"Special action \"{action.Id}\" is offered in single mode and must emit exactly one keystroke.");
            }
        }
    }
}
