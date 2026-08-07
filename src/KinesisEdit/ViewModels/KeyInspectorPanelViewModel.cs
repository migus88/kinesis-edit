using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One mode panel of the key inspector rail — the body under the mode tabs (mockups
    /// <c>1e</c>/<c>2a</c>/<c>2h</c>). <b>This type is the contract between the rail and the
    /// panels</b>: <see cref="KeyInspectorViewModel"/> knows nothing about any concrete panel, and
    /// a panel knows nothing about the rail beyond what is written here.
    ///
    /// <para><b>What a panel owns.</b> Exactly one <see cref="KeyInspectorMode"/> — the one slot
    /// the position's rule lives in — and everything the user does inside it. It owns its own
    /// fields, its own commands and its own writes into <c>KeyboardKey</c>. It does <b>not</b> own
    /// the header, the assignment line, the mode tabs, the exclusivity sentence or the footer:
    /// those are the rail's and are drawn once, above and below whichever panel is showing.</para>
    ///
    /// <para><b>How a panel learns the selected key changed.</b> It is <em>pushed</em>, never
    /// polled and never subscribed. Core's model raises no change notification at all
    /// (docs/app/keyboard-model.md — plain mutable POCOs), so a panel that watched a
    /// <c>KeyboardKey</c> would watch it forever in silence. The editor's one refresh funnel —
    /// <c>KeyboardEditorViewModel.RefreshCounters()</c>, which every path that can write the layout
    /// already ends in — reaches the rail, and the rail calls
    /// <see cref="Refresh(KeyboardKeyViewModel?, KeyboardLayerViewModel?, KeyboardLayout?, EditorAdvisories)"/>
    /// on the active panel. <b>Every panel therefore has to survive being refreshed with a null
    /// key</b> (nothing selected, nothing loaded, a device with no board picture) and with the same
    /// key it already had (a refresh caused by somebody else's edit).</para>
    ///
    /// <para><b>Refresh re-reads; it never writes.</b> It is called after somebody else's mutation,
    /// so a panel that wrote back from it would write on every counter refresh — and, because the
    /// write would end in another refresh, forever. Read the model, raise what moved, stop.</para>
    ///
    /// <para><b>How a panel participates in mode exclusivity.</b> The modes are one slot: assigning
    /// a tap-and-hold clears the position's remap and multi-modifier, and a remap clears the
    /// tap-and-hold. The rail states that <em>before</em> it happens
    /// (<see cref="KeyInspectorViewModel.ModeSwitchWarning"/>, mockup <c>2h</c>: "the panel says so
    /// at the point of switching rather than after"), so a panel neither repeats the warning nor
    /// asks for confirmation of its own — it just writes, and lets Core clear what Core clears.</para>
    ///
    /// <para><b>What a panel must do about keystroke capture.</b> The editor owns the <em>single</em>
    /// subscription to <c>IKeystrokeCaptureService</c> and routes each keystroke to exactly one
    /// consumer (docs/app/keyboard-editor.md, "Keystroke routing"). A panel therefore never starts,
    /// stops or subscribes to capture itself. It says whether it is waiting for one through
    /// <see cref="IsRecording"/>, raises <see cref="RecordingChanged"/> whenever that answer moves,
    /// and takes the keystroke through <see cref="IKeystrokeSink"/> when it is armed. Two rules
    /// follow, and both have already cost time in this codebase:
    /// <list type="bullet">
    /// <item><see cref="IsRecording"/> must be honest, because the editor's <c>IsCaptureActive</c>
    /// is what suppresses ⌘S and the rest of the grammar — a panel that records without saying so
    /// gets its keystrokes eaten by an accelerator.</item>
    /// <item>A field that records a keystroke is <b>never a <c>TextBox</c></b>: focus inside one
    /// auto-suspends capture, so the field would swallow the very keypress it exists to record. Use
    /// the <c>TokenField</c> control theme (<c>Classes="actionField"</c>) — a <c>Border</c> with an
    /// <c>.armed</c> state written for exactly this.</item>
    /// </list></para>
    ///
    /// <para><b><see cref="Deactivate"/> is how a panel is put down.</b> The rail calls it when the
    /// user switches to another mode, when the rail closes and when the selection leaves. Whatever
    /// the panel has armed stops there; nothing is written.</para>
    ///
    /// <para><b>An unavailable panel refuses politely rather than disappearing.</b> The design's
    /// capability law is "absent features are not shown, not disabled" — but the two sanctioned
    /// exceptions both live here: a firmware gate (spec 09 §2) and a locked position (05 §5.3).
    /// A panel whose <see cref="IsAvailable"/> is false is still rendered, with
    /// <see cref="UnavailableReason"/> saying why, because "your firmware is too old" is
    /// information the user needs and a tab that vanished would be a question with no answer.</para>
    /// </summary>
    public abstract class KeyInspectorPanelViewModel : ViewModelBase
    {
        /// <summary>The one slot this panel edits. Fixed for the life of the panel.</summary>
        public abstract KeyInspectorMode Mode { get; }

        /// <summary>
        /// The panel's own section title, as its mode tab reads it — the rail draws the tab from
        /// <see cref="KeyInspectorTabViewModel"/>'s caption, and this is what the panel body itself
        /// calls the thing it is editing.
        /// </summary>
        public abstract string Title { get; }

        /// <summary>
        /// Whether the panel can be used at all right now. False for a firmware gate or a position
        /// that cannot carry this rule; the panel is still drawn — see the type's remarks.
        /// </summary>
        public virtual bool IsAvailable => true;

        /// <summary>
        /// Why it cannot, in the app's own voice, or an empty string while
        /// <see cref="IsAvailable"/> is true. Never a stack trace and never a refusal to explain.
        /// </summary>
        public virtual string UnavailableReason => string.Empty;

        /// <summary>
        /// Whether the panel is waiting for the next physical keystroke. The editor reads it into
        /// <c>IsCaptureActive</c>, which is what stops ⌘S firing mid-recording — see the type's
        /// remarks.
        /// </summary>
        public virtual bool IsRecording => false;

        /// <summary>Raised whenever <see cref="IsRecording"/> moves, in either direction.</summary>
        public event EventHandler? RecordingChanged;

        /// <summary>
        /// Re-reads the model for the position the inspector is showing. Called by the rail on
        /// every editor refresh and on every selection change; must tolerate a null key and must
        /// not write. See the type's remarks.
        /// </summary>
        /// <param name="key">The selected cap, or null when nothing is selected.</param>
        /// <param name="layer">The layer it sits on, or null.</param>
        /// <param name="layout">The open profile's layout, or null when nothing is loaded.</param>
        /// <param name="advisories">
        /// What the editor has already noticed about the profile. Read it
        /// (<see cref="EditorAdvisories.ForKey"/>); never rescan — <c>DuplicateKeyScan</c> walks
        /// every key of every layer, and two places deriving the same finding is two places to
        /// disagree.
        /// </param>
        public abstract void Refresh(
            KeyboardKeyViewModel? key,
            KeyboardLayerViewModel? layer,
            KeyboardLayout? layout,
            EditorAdvisories advisories);

        /// <summary>
        /// Stands the panel down: whatever it has armed stops, and nothing is written. Called when
        /// the mode switches away from this panel, when the rail closes, and when the selection
        /// leaves. The default does nothing, which is right for a panel that arms nothing.
        /// </summary>
        public virtual void Deactivate()
        {
        }

        /// <summary>Raises <see cref="RecordingChanged"/>; call it from wherever the flag moves.</summary>
        protected void OnRecordingChanged()
        {
            RecordingChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
