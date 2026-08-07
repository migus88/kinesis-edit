using System.Globalization;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the key inspector's step editor (mockup <c>2i</c>), drawn exactly as the mock
    /// draws it: <c>01 [lshift] press</c>, <c>02 [b] LS held</c>, <c>03 [lshift] release</c>,
    /// <c>04 [e] tap</c>, <c>07 [enter] tap · 80 ms</c>.
    ///
    /// <para><b>A row is a step plus the delay that follows it.</b> A macro's delay is itself a
    /// keystroke in the stream (a <c>{d080}</c>/<c>{dran}</c> pseudo-key, 06 §2.2) — but the mock
    /// draws it as a qualifier of the step it follows, and that is also how the user thinks about
    /// it ("wait 80 ms after Enter"). So a row anchors on a real keystroke and <em>folds in</em> the
    /// delay keystroke immediately after it. A delay with no step in front of it — a macro that
    /// opens with one, or two delays in a row — gets a row of its own
    /// (<see cref="IsDelayOnly"/>), because dropping it would be editing the file behind the user's
    /// back.</para>
    ///
    /// <para><b>It is a read-only projection.</b> Core's model announces nothing, so a changed
    /// keystroke produces a new row rather than a mutated one;
    /// <see cref="MacroInspectorStepsViewModel"/> owns every write.</para>
    ///
    /// <para><b>Held modifiers are the file's two-letter codes, not <c>⇧</c>.</b> Mockup <c>2i</c>
    /// draws <c>02 [b] ⇧ held</c>; U+21E7 is in neither embedded IBM Plex family, so the character
    /// would tofu wherever the platform substitutes nothing — the same rule that made the record
    /// dot an <c>Ellipse</c>. <c>LS</c>/<c>RC</c>/… is the modifier's own spelling in 05 §5.1, which
    /// makes it mono for the same reason the token beside it is. Recorded as a deviation in
    /// docs/app/design-system.md.</para>
    /// </summary>
    public sealed class MacroInspectorStepViewModel : ViewModelBase
    {
        /// <summary>A plain keystroke: pressed and released (mockup <c>2i</c>).</summary>
        public const string TapAction = "tap";

        /// <summary>An explicit key-down step (06 §2.2).</summary>
        public const string PressAction = "press";

        /// <summary>An explicit key-up step (06 §2.2).</summary>
        public const string ReleaseAction = "release";

        /// <summary>A keystroke struck with modifiers held (mockup <c>2i</c>: <c>[b] ⇧ held</c>).</summary>
        public const string HeldAction = "held";

        /// <summary>What a row carrying nothing but a delay is called.</summary>
        public const string DelayAction = "delay";

        /// <summary>How the random delay reads; the file's own token is <c>dran</c> (11 §11.3).</summary>
        public const string RandomDelayText = "random";

        /// <summary>Unit of a custom delay, as the mock writes it — <c>80 ms</c>.</summary>
        public const string MillisecondSuffix = " ms";

        /// <summary>Two-digit row number, as the mock numbers the steps: <c>01</c>, <c>07</c>.</summary>
        public static string FormatNumber(int position)
        {
            return position.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>The delay a row carries, spelled the way the mock spells it.</summary>
        public static string FormatDelay(int milliseconds, bool isRandom)
        {
            if (isRandom)
            {
                return RandomDelayText;
            }

            return milliseconds > 0
                ? milliseconds.ToString(CultureInfo.InvariantCulture) + MillisecondSuffix
                : string.Empty;
        }

        /// <summary>1-based position of the row in the list — what <c>01</c> counts.</summary>
        public int Position { get; }

        /// <summary>The row number as drawn: <c>01</c>.</summary>
        public string NumberText { get; }

        /// <summary>
        /// The struck key as the file spells it — <c>[lshift]</c> — or an empty string on a
        /// delay-only row. Mono, because it is literally a value in <c>layoutN.txt</c>.
        /// </summary>
        public string TokenText { get; }

        /// <summary>Whether there is a token to draw at all.</summary>
        public bool HasToken => TokenText.Length > 0;

        /// <summary>
        /// The modifiers held with the step, as 05 §5.1's two-letter codes (<c>LS</c>, <c>LS,LC</c>),
        /// or an empty string. Always empty for a step that is itself a modifier key.
        /// </summary>
        public string ModifiersText { get; }

        /// <summary>Whether the step carries held modifiers.</summary>
        public bool HasModifiers => ModifiersText.Length > 0;

        /// <summary>What the step does: <c>tap</c> / <c>press</c> / <c>release</c> / <c>held</c>.</summary>
        public string ActionText { get; }

        /// <summary>The delay that follows this step — <c>80 ms</c>, <c>random</c>, or empty.</summary>
        public string DelayText { get; }

        /// <summary>Whether a delay follows this step.</summary>
        public bool HasDelay => DelayText.Length > 0;

        /// <summary>The custom delay in milliseconds, or 0 for none and for the random delay.</summary>
        public int DelayMilliseconds { get; }

        /// <summary>Whether the delay following this step is 11 §11.3's random one.</summary>
        public bool IsRandomDelay { get; }

        /// <summary>
        /// Whether the row is a bare delay with no step in front of it — the one shape the mock does
        /// not draw, kept because the file can contain it.
        /// </summary>
        public bool IsDelayOnly { get; }

        /// <summary>
        /// Index of the anchor keystroke in <c>Macro.Keystrokes</c>. On a delay-only row this is the
        /// delay's own index, so a row always names something the model can be asked about.
        /// </summary>
        public int KeystrokeIndex { get; }

        /// <summary>How many keystrokes this row occupies — 1, or 2 when a delay is folded in.</summary>
        public int KeystrokeCount { get; }

        /// <summary>Whether this row is the one the delay editor and <c>⌥↑↓</c> are pointed at.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            internal set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>
        /// Builds one row. <paramref name="delay"/> is the keystroke folded in behind
        /// <paramref name="keystroke"/>, or null; on a delay-only row <paramref name="keystroke"/>
        /// <em>is</em> the delay and <paramref name="delay"/> is null.
        /// </summary>
        public MacroInspectorStepViewModel(
            Keystroke keystroke,
            int keystrokeIndex,
            int position,
            TokenDialect dialect,
            MacroInspectorDelay delay,
            bool isDelayOnly)
        {
            ArgumentNullException.ThrowIfNull(keystroke);
            ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
            ArgumentOutOfRangeException.ThrowIfNegative(keystrokeIndex);

            Position = position;
            KeystrokeIndex = keystrokeIndex;
            NumberText = FormatNumber(position);
            IsDelayOnly = isDelayOnly;

            TokenText = isDelayOnly ? string.Empty : KeyboardKeyViewModel.FormatToken(keystroke.Key, dialect);
            ModifiersText = isDelayOnly ? string.Empty : keystroke.FormatModifiers();
            ActionText = isDelayOnly ? DelayAction : BuildAction(keystroke, ModifiersText);

            DelayMilliseconds = delay.Milliseconds;
            IsRandomDelay = delay.IsRandom;
            DelayText = FormatDelay(delay.Milliseconds, delay.IsRandom);
            KeystrokeCount = delay.IsPresent && !isDelayOnly ? 2 : 1;
        }

        private static string BuildAction(Keystroke keystroke, string modifiersText)
        {
            return keystroke.EffectiveDirection switch
            {
                KeyDirection.Down => PressAction,
                KeyDirection.Up => ReleaseAction,
                _ => modifiersText.Length > 0 ? HeldAction : TapAction
            };
        }
    }

    /// <summary>
    /// The delay a step row carries: 11 §11.3's random one, a custom 1-999 ms one, or none. A
    /// struct rather than a nullable int because "random" and "80 ms" are two different answers and
    /// a sentinel millisecond value for the first would be a number that means something else.
    /// </summary>
    public readonly record struct MacroInspectorDelay
    {
        /// <summary>No delay follows the step.</summary>
        public static MacroInspectorDelay None => default;

        /// <summary>11 §11.3's <c>dran</c>.</summary>
        public static MacroInspectorDelay Random => new(0, isRandom: true, isPresent: true);

        /// <summary>A custom delay of <paramref name="milliseconds"/> ms (1-999).</summary>
        public static MacroInspectorDelay Custom(int milliseconds)
        {
            return new MacroInspectorDelay(milliseconds, isRandom: false, isPresent: true);
        }

        /// <summary>The custom delay in milliseconds; 0 for none and for the random delay.</summary>
        public int Milliseconds { get; }

        /// <summary>Whether the delay is the random one.</summary>
        public bool IsRandom { get; }

        /// <summary>Whether there is a delay at all.</summary>
        public bool IsPresent { get; }

        private MacroInspectorDelay(int milliseconds, bool isRandom, bool isPresent)
        {
            Milliseconds = milliseconds;
            IsRandom = isRandom;
            IsPresent = isPresent;
        }
    }
}
