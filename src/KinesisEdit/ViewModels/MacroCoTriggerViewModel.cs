using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One co-trigger toggle of the macro panel — the six Left/Right Shift, Ctrl and Alt buttons
    /// of specs/10-apps-and-ui.md, holding the modifier a macro's trigger must be pressed with
    /// (specs/06-macros.md §2.1, §5).
    /// <para>
    /// The toggle owns no rules: how many may be on at once is
    /// <see cref="Core.Devices.MacroCapability.MaxCoTriggersPerMacro"/> and is enforced by
    /// <see cref="MacroInspectorPanelViewModel"/>, because <see cref="Macro.AddCoTrigger"/> deliberately
    /// neither de-duplicates nor refuses (06 §5 counts populated slots, and a repeated token in a
    /// field file must survive a round trip).
    /// </para>
    /// <para>
    /// <b>It draws a mark, and keeps its caption.</b> Six two-line captions — <c>Left⏎Shift</c> …
    /// <c>Right⏎Opt</c> — in a <c>WrapPanel</c> inside a 300 px rail is what the mark spelling of
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
        /// The six co-trigger buttons of the legacy macro panel, in spec order. Left/right are
        /// distinct keys in the §5.1 modifier table and capture reports them apart, so both
        /// spellings of each modifier get their own toggle.
        /// </summary>
        public static IReadOnlyList<MacroCoTriggerViewModel> CreateAll(TokenDialect dialect)
        {
            var modifiers = new[]
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

        /// <summary>The modifier key this toggle adds as a co-trigger (§5.1).</summary>
        public KeyDefinition Key { get; }

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
            }
            else
            {
                Side = string.Empty;
                Symbol = string.Empty;
            }
        }
    }
}
