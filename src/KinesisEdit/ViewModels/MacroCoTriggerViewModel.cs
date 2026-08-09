using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One co-trigger latch of the macro panel's <b>Trigger strip</b> — the modifier a macro's
    /// trigger must be pressed with (specs/06-macros.md §2.1, §5).
    /// <para>
    /// The toggle owns no rules: how many may be on at once is
    /// <see cref="Core.Devices.MacroCapability.PersistedCoTriggersPerMacro"/> and is enforced by
    /// <see cref="MacroInspectorPanelViewModel"/>, because <see cref="Macro.AddCoTrigger"/> deliberately
    /// neither de-duplicates nor refuses (06 §5 counts populated slots, and a repeated token in a
    /// field file must survive a round trip).
    /// </para>
    /// <para>
    /// <b>The strip offers the three left-hand latches, and that is a deliberate deviation</b>
    /// (issue #137, recorded in docs/app/design-system.md). specs/10-apps-and-ui.md pins the legacy
    /// set at "six co-trigger buttons — Left/Right Shift, Ctrl, Alt"; the issue's own rule is that
    /// authoring is left-only, so <see cref="CreateAll"/> is asked for <c>leftOnly</c> and the three
    /// right-hand spellings are folded onto their left latch through <see cref="Family"/>. A
    /// <c>⌘</c> latch is <b>not</b> offered: no co-trigger anywhere in specs 06 or 10 names one, and
    /// a latch that wrote a token no firmware honours would author a macro that never fires.
    /// </para>
    /// <para>
    /// <b>It draws a mark, and keeps its caption.</b> Two-line captions — <c>Left⏎Shift</c> …
    /// <c>Right⏎Opt</c> — inside a 300 px rail is what the mark spelling of
    /// <see cref="MacroModifierMarks"/> exists to fix: <see cref="Side"/> plus <see cref="Symbol"/>
    /// is <c>R⇧</c>, one line, and a bare <c>⇧</c> for the left one — left is the unmarked side.
    /// <see cref="Caption"/> stays, because a mark alone is not accessible text — it is the
    /// toggle's tooltip, it is the only thing on a left toggle that says which side it is, and it
    /// is what the view falls back to for a co-trigger key that names no modifier at all.
    /// </para>
    /// </summary>
    public sealed class MacroCoTriggerViewModel : ViewModelBase
    {
        /// <summary>
        /// The §5.1 spellings of one physical modifier, grouped: generic, left and right. What
        /// <see cref="FamilyOf"/> answers with, and what lets a <c>{rshft}</c> a file already
        /// carried light the <c>⇧</c> latch instead of nothing at all.
        /// </summary>
        private static readonly MacroModifiers[] _families =
        [
            MacroModifiers.Shift | MacroModifiers.LeftShift | MacroModifiers.RightShift,
            MacroModifiers.Control | MacroModifiers.LeftControl | MacroModifiers.RightControl,
            MacroModifiers.Alt | MacroModifiers.LeftAlt | MacroModifiers.RightAlt,
            MacroModifiers.Win | MacroModifiers.LeftWin | MacroModifiers.RightWin
        ];

        /// <summary>
        /// The co-trigger latches, in spec order. <paramref name="leftOnly"/> is what the Trigger
        /// strip asks for — the three left-hand modifiers of issue #137's left-only rule; the full
        /// set is specs/10-apps-and-ui.md's own six, where left and right are distinct keys in the
        /// §5.1 modifier table and capture reports them apart.
        /// </summary>
        public static IReadOnlyList<MacroCoTriggerViewModel> CreateAll(TokenDialect dialect, bool leftOnly = false)
        {
            var modifiers = leftOnly
                ? new[]
                {
                    MacroModifiers.LeftShift,
                    MacroModifiers.LeftControl,
                    MacroModifiers.LeftAlt
                }
                : new[]
                {
                    MacroModifiers.LeftShift,
                    MacroModifiers.RightShift,
                    MacroModifiers.LeftControl,
                    MacroModifiers.RightControl,
                    MacroModifiers.LeftAlt,
                    MacroModifiers.RightAlt
                };

            var toggles = new List<MacroCoTriggerViewModel>(modifiers.Length);

            foreach (var modifier in modifiers)
            {
                var key = KeyRegistry.FindByCode(MacroModifierCodes.GetKeyCode(modifier));

                if (key is null)
                {
                    continue;
                }

                toggles.Add(new MacroCoTriggerViewModel(key, dialect));
            }

            return toggles;
        }

        /// <summary>
        /// Every §5.1 spelling of the physical modifier <paramref name="modifier"/> names — the
        /// generic code, the left one and the right one — or <see cref="MacroModifiers.None"/> for
        /// a flag outside the table. It is what makes the strip's latches a view of <em>which key
        /// is held</em> rather than of which spelling a file happens to carry.
        /// </summary>
        public static MacroModifiers FamilyOf(MacroModifiers modifier)
        {
            foreach (var family in _families)
            {
                if ((family & modifier) != MacroModifiers.None)
                {
                    return family;
                }
            }

            return MacroModifiers.None;
        }

        /// <summary>The modifier key this toggle adds as a co-trigger (§5.1).</summary>
        public KeyDefinition Key { get; }

        /// <summary>
        /// Every §5.1 spelling of the physical modifier this latch stands for (see
        /// <see cref="FamilyOf"/>), or <see cref="MacroModifiers.None"/> for a key that names no
        /// modifier. <b>A file's right-hand or generic co-trigger lights the matching left latch</b>
        /// through this, so the user can see the co-trigger exists rather than looking at a strip
        /// that claims the macro is a bare press.
        /// </summary>
        public MacroModifiers Family { get; }

        /// <summary>
        /// The key's own caption, by the shared <see cref="KeyCaption"/> rule — two lines for every
        /// one of the six (<c>Left⏎Shift</c>). It is the toggle's <b>tooltip</b> and its fallback
        /// face, no longer its label; see <see cref="Symbol"/>.
        /// </summary>
        public string Caption { get; }

        /// <summary>
        /// The <c>R</c> prefix of the mark — empty for a <b>left</b> modifier as well as for one
        /// that names no side, because left is the unmarked case (see
        /// <see cref="MacroModifierMarks.LeftSide"/>). It is a Latin letter and belongs in the
        /// ordinary mono face: the key-symbol subset carries no letters, so a whole <c>R⇧</c> in
        /// <c>.keySymbol</c> would fall back for its <c>R</c>.
        /// </summary>
        public string Side { get; }

        /// <summary>
        /// The mark itself — <c>⇧</c>, <c>⌃</c>, <c>⌥</c> — drawn in <c>.keySymbol</c>. Empty when
        /// the key names no modifier, which is the only case <see cref="HasMark"/> is false for.
        /// </summary>
        public string Symbol { get; }

        /// <summary>Whether this key resolved to a mark at all, and so is drawn as one.</summary>
        public bool HasMark => Symbol.Length > 0;

        /// <summary>Whether the mark carries a side letter to draw beside it.</summary>
        public bool HasSide => Side.Length > 0;

        /// <summary>Whether the macro under edit currently carries this co-trigger.</summary>
        public bool IsOn
        {
            get => _isOn;
            internal set => SetProperty(ref _isOn, value);
        }

        private bool _isOn;

        /// <summary>
        /// Creates the toggle for one modifier key. The mark is resolved through the same bridge
        /// the readers use — <see cref="MacroModifierCodes.TryFromKeyCode"/> then
        /// <see cref="MacroModifierMarks.For"/> — and a key that names no modifier simply keeps its
        /// caption rather than being refused: <see cref="CreateAll"/> builds only modifiers today,
        /// and a toggle that threw on the day it did not would take the whole panel with it.
        /// </summary>
        public MacroCoTriggerViewModel(KeyDefinition key, TokenDialect dialect)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));

            Caption = KeyCaption.For(key, dialect, KeyCaption.IsMacOs, EmbeddedFontGlyphCoverage.Instance);

            if (MacroModifierCodes.TryFromKeyCode(key.Code, out var modifier))
            {
                var mark = MacroModifierMarks.For(modifier);

                Side = mark.Side;
                Symbol = mark.Symbol;
                Family = FamilyOf(modifier);
            }
            else
            {
                Side = string.Empty;
                Symbol = string.Empty;
                Family = MacroModifiers.None;
            }
        }
    }
}
