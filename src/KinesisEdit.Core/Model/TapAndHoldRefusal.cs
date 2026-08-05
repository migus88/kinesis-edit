namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Why the calling app refuses to open the Assign Tap and Hold Action dialog for a key —
    /// the four pre-dialog checks of specs/11-feature-dialogs.md §11.1, in the order they are
    /// evaluated. <see cref="None"/> means the dialog may open.
    /// </summary>
    public enum TapAndHoldRefusal
    {
        /// <summary>No refusal — the dialog may open for this key.</summary>
        None = 0,

        /// <summary>The same key position already carries a tap-and-hold on another layer.</summary>
        SameKeyInBothLayers = 1,

        /// <summary>The layout already holds the maximum number of tap-and-hold actions.</summary>
        MaximumReached = 2,

        /// <summary>The key is a macro trigger.</summary>
        MacroTriggerKey = 3,

        /// <summary>The key is A-Z or 0-9 on the top layer.</summary>
        AlphanumericOnTopLayer = 4
    }
}
