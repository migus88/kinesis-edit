namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// A platform-neutral identifier of a <b>physical key position</b> on the host keyboard —
    /// the input side of the keystroke capture described in specs/10-apps-and-ui.md
    /// ("Keyboard-input capture"), which requires the left/right modifier distinction and a
    /// keypad Enter separate from the main Enter (also specs/12-savant-elite.md §"Programming
    /// a pedal" step 3).
    /// <para>
    /// Member names are deliberately identical to the W3C <c>KeyboardEvent.code</c> names
    /// (which <c>Avalonia.Input.PhysicalKey</c> also uses), so a UI adapter can bridge the two
    /// enums by name without a hand-written table. The numeric values are this project's own
    /// and intentionally do <b>not</b> mirror any toolkit's values.
    /// </para>
    /// <para>
    /// Every member except <see cref="None"/> maps to a key-table entry through
    /// <see cref="PhysicalKeyMap"/>; positions the key registry cannot express (for example the
    /// Japanese <c>IntlRo</c>/<c>IntlYen</c> keys, absent from specs/05-key-model.md §3.2) are
    /// deliberately not declared here and are passed through to the OS.
    /// </para>
    /// </summary>
    public enum PhysicalKeyCode
    {
        /// <summary>No key / an unrecognized physical position.</summary>
        None = 0,

        /// <summary>The <c>A</c> position (specs/05-key-model.md §3.1).</summary>
        A = 1,

        /// <summary>The <c>B</c> position (specs/05-key-model.md §3.1).</summary>
        B = 2,

        /// <summary>The <c>C</c> position (specs/05-key-model.md §3.1).</summary>
        C = 3,

        /// <summary>The <c>D</c> position (specs/05-key-model.md §3.1).</summary>
        D = 4,

        /// <summary>The <c>E</c> position (specs/05-key-model.md §3.1).</summary>
        E = 5,

        /// <summary>The <c>F</c> position (specs/05-key-model.md §3.1).</summary>
        F = 6,

        /// <summary>The <c>G</c> position (specs/05-key-model.md §3.1).</summary>
        G = 7,

        /// <summary>The <c>H</c> position (specs/05-key-model.md §3.1).</summary>
        H = 8,

        /// <summary>The <c>I</c> position (specs/05-key-model.md §3.1).</summary>
        I = 9,

        /// <summary>The <c>J</c> position (specs/05-key-model.md §3.1).</summary>
        J = 10,

        /// <summary>The <c>K</c> position (specs/05-key-model.md §3.1).</summary>
        K = 11,

        /// <summary>The <c>L</c> position (specs/05-key-model.md §3.1).</summary>
        L = 12,

        /// <summary>The <c>M</c> position (specs/05-key-model.md §3.1).</summary>
        M = 13,

        /// <summary>The <c>N</c> position (specs/05-key-model.md §3.1).</summary>
        N = 14,

        /// <summary>The <c>O</c> position (specs/05-key-model.md §3.1).</summary>
        O = 15,

        /// <summary>The <c>P</c> position (specs/05-key-model.md §3.1).</summary>
        P = 16,

        /// <summary>The <c>Q</c> position (specs/05-key-model.md §3.1).</summary>
        Q = 17,

        /// <summary>The <c>R</c> position (specs/05-key-model.md §3.1).</summary>
        R = 18,

        /// <summary>The <c>S</c> position (specs/05-key-model.md §3.1).</summary>
        S = 19,

        /// <summary>The <c>T</c> position (specs/05-key-model.md §3.1).</summary>
        T = 20,

        /// <summary>The <c>U</c> position (specs/05-key-model.md §3.1).</summary>
        U = 21,

        /// <summary>The <c>V</c> position (specs/05-key-model.md §3.1).</summary>
        V = 22,

        /// <summary>The <c>W</c> position (specs/05-key-model.md §3.1).</summary>
        W = 23,

        /// <summary>The <c>X</c> position (specs/05-key-model.md §3.1).</summary>
        X = 24,

        /// <summary>The <c>Y</c> position (specs/05-key-model.md §3.1).</summary>
        Y = 25,

        /// <summary>The <c>Z</c> position (specs/05-key-model.md §3.1).</summary>
        Z = 26,

        /// <summary>The main-row <c>0</c> position (specs/05-key-model.md §3.1).</summary>
        Digit0 = 30,

        /// <summary>The main-row <c>1</c> position (specs/05-key-model.md §3.1).</summary>
        Digit1 = 31,

        /// <summary>The main-row <c>2</c> position (specs/05-key-model.md §3.1).</summary>
        Digit2 = 32,

        /// <summary>The main-row <c>3</c> position (specs/05-key-model.md §3.1).</summary>
        Digit3 = 33,

        /// <summary>The main-row <c>4</c> position (specs/05-key-model.md §3.1).</summary>
        Digit4 = 34,

        /// <summary>The main-row <c>5</c> position (specs/05-key-model.md §3.1).</summary>
        Digit5 = 35,

        /// <summary>The main-row <c>6</c> position (specs/05-key-model.md §3.1).</summary>
        Digit6 = 36,

        /// <summary>The main-row <c>7</c> position (specs/05-key-model.md §3.1).</summary>
        Digit7 = 37,

        /// <summary>The main-row <c>8</c> position (specs/05-key-model.md §3.1).</summary>
        Digit8 = 38,

        /// <summary>The main-row <c>9</c> position (specs/05-key-model.md §3.1).</summary>
        Digit9 = 39,

        /// <summary>The backtick/tilde position, token <c>tilde</c>/<c>grav</c> (specs/05-key-model.md §3.2).</summary>
        Backquote = 40,

        /// <summary>The hyphen/underscore position, token <c>hyph</c> (specs/05-key-model.md §3.2).</summary>
        Minus = 41,

        /// <summary>The equals/plus position, token <c>eql</c> (specs/05-key-model.md §3.2).</summary>
        Equal = 42,

        /// <summary>The opening-bracket position, token <c>obrk</c> (specs/05-key-model.md §3.2).</summary>
        BracketLeft = 43,

        /// <summary>The closing-bracket position, token <c>cbrk</c> (specs/05-key-model.md §3.2).</summary>
        BracketRight = 44,

        /// <summary>The backslash/pipe position, token <c>bsls</c> (specs/05-key-model.md §3.2).</summary>
        Backslash = 45,

        /// <summary>The semicolon/colon position, token <c>colon</c>/<c>scol</c> (specs/05-key-model.md §3.2).</summary>
        Semicolon = 46,

        /// <summary>The apostrophe/quote position, token <c>apos</c> (specs/05-key-model.md §3.2).</summary>
        Quote = 47,

        /// <summary>The comma position, token <c>com</c>/<c>comm</c> (specs/05-key-model.md §3.2).</summary>
        Comma = 48,

        /// <summary>The period position, token <c>per</c>/<c>perd</c> (specs/05-key-model.md §3.2).</summary>
        Period = 49,

        /// <summary>The slash/question position, token <c>fsls</c> (specs/05-key-model.md §3.2).</summary>
        Slash = 50,

        /// <summary>The extra ISO key next to Left Shift, token <c>intl\</c>/<c>int#</c> (specs/05-key-model.md §3.2).</summary>
        IntlBackslash = 51,

        /// <summary>The Escape position (specs/05-key-model.md §3.3).</summary>
        Escape = 60,

        /// <summary>The Backspace position (specs/05-key-model.md §3.3).</summary>
        Backspace = 61,

        /// <summary>The Tab position (specs/05-key-model.md §3.3).</summary>
        Tab = 62,

        /// <summary>The main Enter/Return position — distinct from <see cref="NumPadEnter"/> (specs/05-key-model.md §3.3).</summary>
        Enter = 63,

        /// <summary>The space-bar position (specs/05-key-model.md §3.3).</summary>
        Space = 64,

        /// <summary>The Caps Lock position (specs/05-key-model.md §3.3).</summary>
        CapsLock = 65,

        /// <summary>The <c>F1</c> position (specs/05-key-model.md §3.4).</summary>
        F1 = 70,

        /// <summary>The <c>F2</c> position (specs/05-key-model.md §3.4).</summary>
        F2 = 71,

        /// <summary>The <c>F3</c> position (specs/05-key-model.md §3.4).</summary>
        F3 = 72,

        /// <summary>The <c>F4</c> position (specs/05-key-model.md §3.4).</summary>
        F4 = 73,

        /// <summary>The <c>F5</c> position (specs/05-key-model.md §3.4).</summary>
        F5 = 74,

        /// <summary>The <c>F6</c> position (specs/05-key-model.md §3.4).</summary>
        F6 = 75,

        /// <summary>The <c>F7</c> position (specs/05-key-model.md §3.4).</summary>
        F7 = 76,

        /// <summary>The <c>F8</c> position (specs/05-key-model.md §3.4).</summary>
        F8 = 77,

        /// <summary>The <c>F9</c> position (specs/05-key-model.md §3.4).</summary>
        F9 = 78,

        /// <summary>The <c>F10</c> position (specs/05-key-model.md §3.4).</summary>
        F10 = 79,

        /// <summary>The <c>F11</c> position (specs/05-key-model.md §3.4).</summary>
        F11 = 80,

        /// <summary>The <c>F12</c> position (specs/05-key-model.md §3.4).</summary>
        F12 = 81,

        /// <summary>The <c>F13</c> position (specs/05-key-model.md §3.4).</summary>
        F13 = 82,

        /// <summary>The <c>F14</c> position (specs/05-key-model.md §3.4).</summary>
        F14 = 83,

        /// <summary>The <c>F15</c> position (specs/05-key-model.md §3.4).</summary>
        F15 = 84,

        /// <summary>The <c>F16</c> position (specs/05-key-model.md §3.4).</summary>
        F16 = 85,

        /// <summary>The <c>F17</c> position (specs/05-key-model.md §3.4).</summary>
        F17 = 86,

        /// <summary>The <c>F18</c> position (specs/05-key-model.md §3.4).</summary>
        F18 = 87,

        /// <summary>The <c>F19</c> position (specs/05-key-model.md §3.4).</summary>
        F19 = 88,

        /// <summary>The <c>F20</c> position (specs/05-key-model.md §3.4).</summary>
        F20 = 89,

        /// <summary>The <c>F21</c> position (specs/05-key-model.md §3.4).</summary>
        F21 = 90,

        /// <summary>The <c>F22</c> position (specs/05-key-model.md §3.4).</summary>
        F22 = 91,

        /// <summary>The <c>F23</c> position (specs/05-key-model.md §3.4).</summary>
        F23 = 92,

        /// <summary>The <c>F24</c> position (specs/05-key-model.md §3.4).</summary>
        F24 = 93,

        /// <summary>
        /// The Print Screen position (specs/05-key-model.md §3.3). Recognized only on key-up
        /// during capture (specs/10-apps-and-ui.md, "Keyboard-input capture").
        /// </summary>
        PrintScreen = 100,

        /// <summary>The Scroll Lock position (specs/05-key-model.md §3.3).</summary>
        ScrollLock = 101,

        /// <summary>The Pause/Break position (specs/05-key-model.md §3.3).</summary>
        Pause = 102,

        /// <summary>The Insert position (specs/05-key-model.md §3.3).</summary>
        Insert = 110,

        /// <summary>The forward-Delete position (specs/05-key-model.md §3.3).</summary>
        Delete = 111,

        /// <summary>The Home position (specs/05-key-model.md §3.3).</summary>
        Home = 112,

        /// <summary>The End position (specs/05-key-model.md §3.3).</summary>
        End = 113,

        /// <summary>The Page Up position (specs/05-key-model.md §3.3).</summary>
        PageUp = 114,

        /// <summary>The Page Down position (specs/05-key-model.md §3.3).</summary>
        PageDown = 115,

        /// <summary>The Up-arrow position (specs/05-key-model.md §3.3).</summary>
        ArrowUp = 116,

        /// <summary>The Down-arrow position (specs/05-key-model.md §3.3).</summary>
        ArrowDown = 117,

        /// <summary>The Left-arrow position (specs/05-key-model.md §3.3).</summary>
        ArrowLeft = 118,

        /// <summary>The Right-arrow position (specs/05-key-model.md §3.3).</summary>
        ArrowRight = 119,

        /// <summary>The Num Lock position (specs/05-key-model.md §3.3).</summary>
        NumLock = 130,

        /// <summary>The keypad <c>0</c> position, token <c>kp0</c> (specs/05-key-model.md §3.6).</summary>
        NumPad0 = 131,

        /// <summary>The keypad <c>1</c> position, token <c>kp1</c> (specs/05-key-model.md §3.6).</summary>
        NumPad1 = 132,

        /// <summary>The keypad <c>2</c> position, token <c>kp2</c> (specs/05-key-model.md §3.6).</summary>
        NumPad2 = 133,

        /// <summary>The keypad <c>3</c> position, token <c>kp3</c> (specs/05-key-model.md §3.6).</summary>
        NumPad3 = 134,

        /// <summary>The keypad <c>4</c> position, token <c>kp4</c> (specs/05-key-model.md §3.6).</summary>
        NumPad4 = 135,

        /// <summary>The keypad <c>5</c> position, token <c>kp5</c> (specs/05-key-model.md §3.6).</summary>
        NumPad5 = 136,

        /// <summary>The keypad <c>6</c> position, token <c>kp6</c> (specs/05-key-model.md §3.6).</summary>
        NumPad6 = 137,

        /// <summary>The keypad <c>7</c> position, token <c>kp7</c> (specs/05-key-model.md §3.6).</summary>
        NumPad7 = 138,

        /// <summary>The keypad <c>8</c> position, token <c>kp8</c> (specs/05-key-model.md §3.6).</summary>
        NumPad8 = 139,

        /// <summary>The keypad <c>9</c> position, token <c>kp9</c> (specs/05-key-model.md §3.6).</summary>
        NumPad9 = 140,

        /// <summary>The keypad divide position, token <c>kpdiv</c>/<c>kp/</c> (specs/05-key-model.md §3.6).</summary>
        NumPadDivide = 141,

        /// <summary>The keypad multiply position, token <c>kpmult</c>/<c>kp*</c> (specs/05-key-model.md §3.6).</summary>
        NumPadMultiply = 142,

        /// <summary>The keypad minus position, token <c>kpmin</c>/<c>kp-</c> (specs/05-key-model.md §3.6).</summary>
        NumPadSubtract = 143,

        /// <summary>The keypad plus position, token <c>kpplus</c>/<c>kp+</c> (specs/05-key-model.md §3.6).</summary>
        NumPadAdd = 144,

        /// <summary>
        /// The keypad Enter position, token <c>kpenter</c>/<c>kpent</c>/<c>kpen</c>. It carries the
        /// distinct internal code 10000, never the main Enter's code — the capture layer must keep
        /// the two apart (specs/10-apps-and-ui.md; specs/12-savant-elite.md §"Programming a pedal").
        /// </summary>
        NumPadEnter = 145,

        /// <summary>The keypad decimal position, token <c>kp.</c> (specs/05-key-model.md §3.6).</summary>
        NumPadDecimal = 146,

        /// <summary>The keypad equals position, token <c>kp=</c> (specs/05-key-model.md §3.6).</summary>
        NumPadEqual = 147,

        /// <summary>The left Shift position, token <c>lshift</c>/<c>lshft</c>/<c>lshf</c> (specs/05-key-model.md §3.5).</summary>
        ShiftLeft = 160,

        /// <summary>The right Shift position, token <c>rshift</c>/<c>rshft</c>/<c>rshf</c> (specs/05-key-model.md §3.5).</summary>
        ShiftRight = 161,

        /// <summary>The left Ctrl position, token <c>lctrl</c>/<c>lctrl</c>/<c>lctr</c> (specs/05-key-model.md §3.5).</summary>
        ControlLeft = 162,

        /// <summary>The right Ctrl position, token <c>rctrl</c>/<c>rctrl</c>/<c>rctr</c> (specs/05-key-model.md §3.5).</summary>
        ControlRight = 163,

        /// <summary>The left Alt/Option position, token <c>lalt</c> (specs/05-key-model.md §3.5).</summary>
        AltLeft = 164,

        /// <summary>The right Alt/Option position, token <c>ralt</c> (specs/05-key-model.md §3.5).</summary>
        AltRight = 165,

        /// <summary>The left Meta position — Cmd on macOS, Win elsewhere; token <c>lwin</c> (specs/05-key-model.md §3.5).</summary>
        MetaLeft = 166,

        /// <summary>The right Meta position — Cmd on macOS, Win elsewhere; token <c>rwin</c> (specs/05-key-model.md §3.5).</summary>
        MetaRight = 167,

        /// <summary>The application/context-menu position, token <c>menu</c>/<c>app</c> (specs/05-key-model.md §3.9).</summary>
        ContextMenu = 170,

        /// <summary>The mute position, token <c>mute</c> (specs/05-key-model.md §3.7).</summary>
        AudioVolumeMute = 180,

        /// <summary>The volume-down position, token <c>vol-</c> (specs/05-key-model.md §3.7).</summary>
        AudioVolumeDown = 181,

        /// <summary>The volume-up position, token <c>vol+</c> (specs/05-key-model.md §3.7).</summary>
        AudioVolumeUp = 182,

        /// <summary>The next-track position, token <c>next</c> (specs/05-key-model.md §3.7).</summary>
        MediaTrackNext = 183,

        /// <summary>The previous-track position, token <c>prev</c> (specs/05-key-model.md §3.7).</summary>
        MediaTrackPrevious = 184,

        /// <summary>The media-stop position, token <c>stop</c> (specs/05-key-model.md §3.7).</summary>
        MediaStop = 185,

        /// <summary>The play/pause position, token <c>play</c>/<c>plpa</c> (specs/05-key-model.md §3.7).</summary>
        MediaPlayPause = 186,

        /// <summary>The eject position, internal code 11150, token <c>ejct</c> (specs/05-key-model.md §3.7).</summary>
        Eject = 187,
    }
}
