using KinesisEdit.Core.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A consumer of physically captured keystrokes. The editor owns the single subscription to
    /// <see cref="Core.Input.IKeystrokeCaptureService"/> and routes each keystroke to exactly one
    /// sink, so a pressed key becomes either a remap, a macro step, or a Tap/Hold action — never
    /// two of them at once (spec 10, "Routing").
    /// </summary>
    public interface IKeystrokeSink
    {
        /// <summary>
        /// Whether this sink currently wants keystrokes. A sink that returns <c>false</c> is
        /// skipped, letting the editor fall through to the next candidate.
        /// </summary>
        bool WantsKeystrokes { get; }

        /// <summary>Consumes one captured keystroke.</summary>
        void ReceiveKeystroke(CapturedKeystroke keystroke);
    }
}
