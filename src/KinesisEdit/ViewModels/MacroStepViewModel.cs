using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One recorded keystroke of the macro under edit (specs/06-macros.md §1): the key it types
    /// and the modifiers held while it is struck. A read-only projection —
    /// <see cref="MacroStepListViewModel"/> owns every insertion and removal, and Core's model
    /// announces nothing, so a changed keystroke produces a new row rather than a mutated one.
    /// </summary>
    public sealed class MacroStepViewModel : ViewModelBase
    {
        /// <summary>Separates the held modifiers from the key in <see cref="Caption"/>.</summary>
        public const string ModifierSeparator = " + ";

        /// <summary>Suffix marking an explicit key-down step (specs/06-macros.md §2.2).</summary>
        public const string KeyDownSuffix = " ↓";

        /// <summary>Suffix marking an explicit key-up step (specs/06-macros.md §2.2).</summary>
        public const string KeyUpSuffix = " ↑";

        /// <summary>The model keystroke this row shows.</summary>
        public Keystroke Keystroke { get; }

        /// <summary>1-based position of the step inside the macro.</summary>
        public int Position { get; }

        /// <summary>What the struck key is called, by the shared <see cref="KeyCaption"/> rule.</summary>
        public string KeyText { get; }

        /// <summary>
        /// The §5.1 two-character codes of the modifiers held with the step ("LS,LC"), or an empty
        /// string when none are. Always empty for a step that is itself a modifier key (§5.1).
        /// </summary>
        public string ModifiersText { get; }

        /// <summary>Whether the step carries held modifiers.</summary>
        public bool HasModifiers => ModifiersText.Length > 0;

        /// <summary>The whole step on one line ("LS + A"), with an arrow for an explicit up/down.</summary>
        public string Caption { get; }

        /// <summary>Projects one keystroke of a macro at <paramref name="position"/> (1-based).</summary>
        public MacroStepViewModel(Keystroke keystroke, int position, TokenDialect dialect)
        {
            ArgumentNullException.ThrowIfNull(keystroke);
            ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);

            Keystroke = keystroke;
            Position = position;
            KeyText = KeyCaption.For(keystroke.Key, dialect, KeyCaption.IsMacOs);
            ModifiersText = keystroke.FormatModifiers();
            Caption = BuildCaption(keystroke, ModifiersText, KeyText);
        }

        private static string BuildCaption(Keystroke keystroke, string modifiersText, string keyText)
        {
            var caption = modifiersText.Length > 0 ? modifiersText + ModifierSeparator + keyText : keyText;

            return keystroke.EffectiveDirection switch
            {
                KeyDirection.Down => caption + KeyDownSuffix,
                KeyDirection.Up => caption + KeyUpSuffix,
                _ => caption
            };
        }
    }
}
