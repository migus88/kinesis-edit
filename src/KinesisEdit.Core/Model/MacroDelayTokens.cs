using System.Globalization;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// The delay keystrokes the Macro Timing Delays dialog inserts into a macro
    /// (specs/11-feature-dialogs.md §11.3, specs/06-macros.md §2.2): the random delay
    /// <c>dran</c> and the custom delays <c>d001</c>..<c>d999</c>, whose token is always the
    /// millisecond count zero-padded to three digits. Token text and key-table resolution only —
    /// the dialog, its validation wording, and the actual insertion into a
    /// <see cref="Macro"/> belong to the editor UI.
    /// </summary>
    public static class MacroDelayTokens
    {
        /// <summary>File token of the random timing delay (11 §11.3; 06 §2.2 <c>{dran}</c>).</summary>
        public const string RandomToken = "dran";

        /// <summary>Shortest custom delay the dialog accepts, in milliseconds (11 §11.3).</summary>
        public const int MinDelayMilliseconds = 1;

        /// <summary>Longest custom delay the dialog accepts, in milliseconds (11 §11.3).</summary>
        public const int MaxDelayMilliseconds = 999;

        private const string CustomTokenPrefix = "d";

        /// <summary>Mandatory three-digit zero padding of a custom delay token (06 §2.2: <c>d050</c> = 50 ms).</summary>
        private const string CustomTokenDigitFormat = "000";

        /// <summary>
        /// Returns the file token of a custom delay: <c>d001</c>..<c>d999</c>, the millisecond
        /// count zero-padded to three digits (11 §11.3, 06 §2.2). Throws for a value outside
        /// 1-999 — 11 §11.3 clamps the dialog's own field, so anything else is a caller error,
        /// not a field-file fact.
        /// </summary>
        public static string BuildCustomToken(int milliseconds)
        {
            if (!IsValidDelay(milliseconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(milliseconds),
                    milliseconds,
                    $"A custom macro delay must be between {MinDelayMilliseconds} and {MaxDelayMilliseconds} milliseconds.");
            }

            return CustomTokenPrefix + milliseconds.ToString(CustomTokenDigitFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>Whether <paramref name="milliseconds"/> is inside the 1-999 ms range of 11 §11.3.</summary>
        public static bool IsValidDelay(int milliseconds)
        {
            return milliseconds is >= MinDelayMilliseconds and <= MaxDelayMilliseconds;
        }

        /// <summary>
        /// Resolves the random-delay key of <paramref name="dialect"/> (<see cref="RandomToken"/>),
        /// or null when the dialect does not name it.
        /// </summary>
        public static KeyDefinition? ResolveRandom(TokenDialect dialect)
        {
            return KeyRegistry.FindByToken(RandomToken, dialect);
        }

        /// <summary>
        /// Resolves the custom-delay key for <paramref name="milliseconds"/>, or null when the
        /// dialect does not name it. Throws for a delay outside 1-999
        /// (<see cref="BuildCustomToken"/>).
        /// <para>
        /// Resolution goes through the <em>token</em>, never the code: <c>dran</c> and the
        /// generated <c>d002</c> share code 10087 and
        /// <see cref="KeyRegistry.FindByCode"/> answers <c>dran</c> by first match
        /// (specs/05-key-model.md §7), so a code lookup would silently turn a 2 ms delay into a
        /// random one.
        /// </para>
        /// <para>
        /// 125 ms and 500 ms resolve to the distinct legacy delay keys 10007/10008, which shadow
        /// their generated twins in registration order (05 §3.12). The token written is
        /// <c>d125</c>/<c>d500</c> either way, so the file text is identical; only on the RGB/TKO
        /// families does the parser read those two back as the generated codes 10085 + N
        /// (06 §2.2), a code-identity difference the serialized macro cannot show.
        /// </para>
        /// </summary>
        public static KeyDefinition? ResolveCustom(int milliseconds, TokenDialect dialect)
        {
            return KeyRegistry.FindByToken(BuildCustomToken(milliseconds), dialect);
        }
    }
}
