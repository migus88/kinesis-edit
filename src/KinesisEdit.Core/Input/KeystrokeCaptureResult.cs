namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// What the host must do with a key event it fed to <see cref="KeystrokeRecorder"/>:
    /// whether to swallow it so the OS never sees it, and the keystroke it produced (if any),
    /// per specs/10-apps-and-ui.md ("Keyboard-input capture").
    /// </summary>
    public sealed record KeystrokeCaptureResult
    {
        /// <summary>The key is not part of a recording — let it reach the OS, nothing was captured.</summary>
        public static KeystrokeCaptureResult PassThrough { get; } = new();

        /// <summary>The key belongs to the recording — swallow it, but it produced no keystroke yet.</summary>
        public static KeystrokeCaptureResult Swallowed { get; } = new() { ShouldSwallow = true };

        /// <summary>The key is swallowed and produced <paramref name="keystroke"/>.</summary>
        public static KeystrokeCaptureResult Captured(CapturedKeystroke keystroke)
        {
            return new KeystrokeCaptureResult
            {
                ShouldSwallow = true,
                Keystroke = keystroke
            };
        }

        /// <summary>
        /// Whether the host must swallow the key event. True for every key that is part of the
        /// recorded combination — specs/10-apps-and-ui.md: "the keystroke is swallowed so the OS
        /// never sees it". False for keys the recorder does not understand, which must reach the
        /// OS unchanged.
        /// </summary>
        public bool ShouldSwallow { get; init; }

        /// <summary>The keystroke this event completed, or null when the event only updated state.</summary>
        public CapturedKeystroke? Keystroke { get; init; }
    }
}
