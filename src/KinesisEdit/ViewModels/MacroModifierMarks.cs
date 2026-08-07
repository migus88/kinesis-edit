using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The <b>display</b> spelling of a macro keystroke's held modifiers: <c>⇧</c>, <c>⌃</c>,
    /// <c>R⌥</c>, <c>⌘</c> — the marks mockup <c>2i</c> draws in a macro step row, in place of the
    /// file format's two-character codes.
    ///
    /// <para><b>This is not a replacement for
    /// <see cref="Keystroke.FormatModifiers"/> or <see cref="MacroModifierCodes"/>.</b> Those are
    /// file-format-shaped — <c>"S "</c> carries a load-bearing trailing space, the set is joined
    /// with <c>","</c>, and a modifier string read from a file round-trips through them verbatim
    /// (specs/05-key-model.md §5.1). They are untouched. This class is display only, and nothing it
    /// produces is ever written to a drive.</para>
    ///
    /// <para><b>Left is unmarked; only the right side is spelled.</b> <c>RS</c> draws <c>R⇧</c>,
    /// and <c>LS</c> and the generic <c>S </c> both draw a bare <c>⇧</c> — so the marks carry
    /// <em>less</em> than the two-character codes did, deliberately. See <see cref="LeftSide"/> for
    /// why, and <see cref="Mark.Description"/> for what keeps the lost distinction reachable.</para>
    ///
    /// <para><b>Rendering: the parts, not the joined string.</b> The marks live in the key-symbol
    /// face (<c>FontKeySymbols</c>, class <c>.keySymbol</c>), which is a 19-codepoint subset and
    /// carries <b>no Latin letters</b> — a whole <c>"R⇧"</c> set in that class falls back to a
    /// system font for its <c>R</c>. So a view draws <see cref="Mark.Side"/> in the ordinary
    /// sans/mono face and <see cref="Mark.Symbol"/> in <c>.keySymbol</c>, two runs side by side (one
    /// run, for a left or a generic modifier, whose side is empty — bind the side run's visibility
    /// to <see cref="Mark.HasSide"/>), and gets both from <see cref="Split"/>.
    /// <see cref="Format"/>'s joined string is for the single-face contexts — a tooltip, an
    /// automation name, a log line — where one face has to carry the lot; it is not what a step row
    /// should bind to.</para>
    ///
    /// <para><b>Order is delegated, not re-declared.</b> <see cref="Split"/> and
    /// <see cref="Format"/> walk <see cref="MacroModifierCodes.Enumerate"/>, which is the §5.1
    /// table order <see cref="MacroModifierCodes.Format"/> writes in — so the display and the file
    /// can never disagree about which modifier comes first. Bits outside
    /// <see cref="MacroModifierCodes.Known"/> name no modifier and are dropped, exactly as they are
    /// when the set is written.</para>
    ///
    /// <para><b>Display does not canonicalize, deliberately.</b>
    /// <see cref="MacroModifierCodes.Canonicalize"/> folds <c>W ,LW</c> into one flag, because both
    /// spellings map to the Left Win key and keystroke <em>accounting</em> must count one held key.
    /// A step row is a view of what the file actually says, so a set that spells that key twice is
    /// drawn twice — <c>⌘ ⌘</c> — rather than silently collapsed. Hiding it would show the user a
    /// macro that is not the one on the drive, and it is the same reason
    /// <see cref="MacroModifierCodes.Format"/> stays on the raw set. A caller that wants the
    /// physical-key reading canonicalizes first and passes the result in.</para>
    /// </summary>
    public static class MacroModifierMarks
    {
        /// <summary>U+21E7 UPWARDS WHITE ARROW — Shift.</summary>
        public const string ShiftMark = "⇧";

        /// <summary>U+2303 UP ARROWHEAD — Control.</summary>
        public const string ControlMark = "⌃";

        /// <summary>U+2325 OPTION KEY — Alt.</summary>
        public const string AltMark = "⌥";

        /// <summary>
        /// U+2318 PLACE OF INTEREST SIGN — the Command mark. The flag is
        /// <see cref="MacroModifiers.Win"/>, named for the key that writes <c>"W "</c> into the
        /// file; <c>⌘</c> is the mark that key wears, and the one mockup <c>2i</c> draws.
        /// </summary>
        public const string WinMark = "⌘";

        /// <summary>
        /// Prefix of a right-hand modifier: <c>R⇧</c>. <b>The only side that is spelled.</b>
        /// </summary>
        public const string RightSide = "R";

        /// <summary>
        /// No prefix — the bare mark. Worn by the <b>left</b> modifiers and by the <b>generic</b>
        /// ones alike; see <see cref="LeftSide"/> for why, and for what that costs.
        /// </summary>
        public const string NoSide = "";

        /// <summary>
        /// Prefix of a left-hand modifier — <b>deliberately empty</b>, so <c>LS</c> draws as a bare
        /// <c>⇧</c> and only <c>RS</c> is spelled <c>R⇧</c>.
        /// <para>
        /// Left is the unmarked case because it is the overwhelmingly common one and because that
        /// is how a keyboard is spoken about: nobody calls it "left shift" until there is a right
        /// one in the sentence. Prefixing both sides made every row carry a letter that
        /// distinguished nothing in the common case, which is what the marks were meant to remove.
        /// </para>
        /// <para>
        /// <b>The cost, stated plainly: a generic modifier and a left one now draw the same.</b>
        /// The file distinguishes them — <c>"S "</c> is §5.1's generic code and <c>"LS"</c> the
        /// left-specific one — and this display no longer does. It is a <b>presentation</b>
        /// collision only: nothing here is a write path, <c>Keystroke.FormatModifiers</c> is
        /// untouched, and a macro read from a file with generic codes is re-serialized with the
        /// codes it arrived with. Capture always produces sided modifiers, so the generic codes
        /// only ever come from a file an older app or firmware wrote. The exact spelling stays
        /// reachable through <see cref="Mark.Description"/>, which a call site hands to a tooltip.
        /// </para>
        /// </summary>
        public const string LeftSide = NoSide;

        /// <summary>
        /// U+2009 THIN SPACE, what <see cref="Format"/> joins a set with.
        /// <para>
        /// Not the file format's <c>","</c>: a comma between glyph marks reads as punctuation in a
        /// sentence, while <c>⌃⇧</c> is one chord struck at once. Not a word space either, which
        /// spaces marks as far apart as words. A thin space is the typographic answer for a run of
        /// symbols, all six probed IBM Plex faces carry it, and — the part that matters to the
        /// glyph gate — it is <b>whitespace</b>, so it is skipped by every coverage probe in the
        /// app and can never tofu. (In the monospace face it takes the monospace advance like
        /// everything else; that is one more reason a step row uses <see cref="Split"/>.)
        /// </para>
        /// </summary>
        public const string Separator = " ";

        /// <summary>
        /// The four distinct marks, in §5.1 table order. What a font-coverage gate walks: these are
        /// the runes the key-symbol family has to be able to draw.
        /// </summary>
        public static IReadOnlyList<string> Marks { get; } = [ShiftMark, ControlMark, AltMark, WinMark];

        private static readonly Mark[] _marks =
        [
            new(MacroModifiers.Shift, NoSide, ShiftMark),
            new(MacroModifiers.LeftShift, LeftSide, ShiftMark),
            new(MacroModifiers.RightShift, RightSide, ShiftMark),
            new(MacroModifiers.Control, NoSide, ControlMark),
            new(MacroModifiers.LeftControl, LeftSide, ControlMark),
            new(MacroModifiers.RightControl, RightSide, ControlMark),
            new(MacroModifiers.Alt, NoSide, AltMark),
            new(MacroModifiers.LeftAlt, LeftSide, AltMark),
            new(MacroModifiers.RightAlt, RightSide, AltMark),
            new(MacroModifiers.Win, NoSide, WinMark),
            new(MacroModifiers.LeftWin, LeftSide, WinMark),
            new(MacroModifiers.RightWin, RightSide, WinMark)
        ];

        /// <summary>
        /// The mark of a single modifier flag. Throws for <see cref="MacroModifiers.None"/> and for
        /// combinations — use <see cref="Split"/> for a set — mirroring
        /// <see cref="MacroModifierCodes.GetCode"/>, whose contract this one shadows.
        /// </summary>
        public static Mark For(MacroModifiers modifier)
        {
            foreach (var mark in _marks)
            {
                if (mark.Modifier == modifier)
                {
                    return mark;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(modifier), modifier, "Not a single modifier flag.");
        }

        /// <summary>
        /// The set as its parts, in §5.1 table order — one <see cref="Mark"/> per flag present, each
        /// carrying its side letter and its mark separately so a view can set the two in different
        /// faces. Empty for <see cref="MacroModifiers.None"/>.
        /// </summary>
        public static IReadOnlyList<Mark> Split(MacroModifiers modifiers)
        {
            var parts = new List<Mark>(_marks.Length);

            foreach (var modifier in MacroModifierCodes.Enumerate(modifiers))
            {
                parts.Add(For(modifier));
            }

            return parts;
        }

        /// <summary>
        /// The set as one string — the marks in §5.1 table order joined with
        /// <see cref="Separator"/>: <c>"R⌃ R⌥"</c>. Empty for <see cref="MacroModifiers.None"/>.
        /// <para>
        /// <b>For a single-face context only</b> (tooltip, automation name, log). A step row binds
        /// to <see cref="Split"/> instead: this string mixes Latin side letters with marks, and no
        /// one face in the app carries both.
        /// </para>
        /// </summary>
        public static string Format(MacroModifiers modifiers)
        {
            var parts = new List<string>(_marks.Length);

            foreach (var mark in Split(modifiers))
            {
                parts.Add(mark.Text);
            }

            return string.Join(Separator, parts);
        }

        /// <summary>
        /// One modifier, drawn. <see cref="Side"/> is the <c>R</c> prefix — <b>empty for a left or
        /// a generic modifier alike</b> (see <see cref="LeftSide"/>) — and belongs in the ordinary
        /// sans/mono face; <see cref="Symbol"/> is the mark and belongs in <c>.keySymbol</c>.
        /// <see cref="Text"/> is the two concatenated, for a context that can only take one string.
        /// </summary>
        /// <param name="Modifier">The flag this mark stands for.</param>
        /// <param name="Side">The side prefix, or empty.</param>
        /// <param name="Symbol">The key-symbol mark.</param>
        public readonly record struct Mark(MacroModifiers Modifier, string Side, string Symbol)
        {
            /// <summary>Whether this modifier draws a prefix letter — true only for a right one.</summary>
            public bool HasSide => Side.Length > 0;

            /// <summary>The side and the mark as one string: <c>R⇧</c>, or a bare <c>⇧</c>.</summary>
            public string Text => Side + Symbol;

            /// <summary>
            /// The modifier named in words — <c>Left Shift</c>, <c>Shift</c>, <c>Right Ctrl</c>.
            /// <para>
            /// <b>This is what makes the unmarked left side recoverable.</b> A bare <c>⇧</c> is
            /// worn by both <see cref="MacroModifiers.LeftShift"/> and
            /// <see cref="MacroModifiers.Shift"/>, so the drawn row cannot tell them apart; a call
            /// site hands this to a <c>ToolTip.Tip</c> and the distinction the file really carries
            /// is one hover away. Derived from the flag name rather than from a second table, so it
            /// cannot drift out of step with the marks beside it.
            /// </para>
            /// </summary>
            public string Description => Describe(Modifier);
        }

        /// <summary>Spells one modifier flag in words; see <see cref="Mark.Description"/>.</summary>
        private static string Describe(MacroModifiers modifier)
        {
            return modifier switch
            {
                MacroModifiers.Shift => "Shift",
                MacroModifiers.LeftShift => "Left Shift",
                MacroModifiers.RightShift => "Right Shift",
                MacroModifiers.Control => "Ctrl",
                MacroModifiers.LeftControl => "Left Ctrl",
                MacroModifiers.RightControl => "Right Ctrl",
                MacroModifiers.Alt => "Alt",
                MacroModifiers.LeftAlt => "Left Alt",
                MacroModifiers.RightAlt => "Right Alt",
                MacroModifiers.Win => "Win",
                MacroModifiers.LeftWin => "Left Win",
                MacroModifiers.RightWin => "Right Win",
                _ => modifier.ToString()
            };
        }
    }
}
