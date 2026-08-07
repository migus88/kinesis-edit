namespace KinesisEdit.Services
{
    /// <summary>
    /// Whether the app animates. The design's motion budget (docs/design/mockups.md, mockup 2b)
    /// ships in two flavors — the full budget and a reduced one that is fades only, with no rise,
    /// no slide and no height animation — and this is the single switch that says which one the
    /// app is using.
    /// <para>
    /// The value is resolved once at startup from the OS accessibility setting and then handed to
    /// <see cref="MotionResourceBinder"/>, which points the motion alias resources at the matching
    /// set. The setter exists so a preference or another platform's detection can flip it; re-run
    /// <see cref="MotionResourceBinder.Apply"/> after changing it, because the alias resources are
    /// not recomputed on their own. <see cref="MotionPreferenceApplier"/> is the piece that does
    /// both.
    /// </para>
    /// </summary>
    public interface IMotionSettings
    {
        /// <summary>True when the app must animate with the reduced budget.</summary>
        bool ReduceMotion { get; set; }

        /// <summary>
        /// What the OS said when it was asked, kept separate from <see cref="ReduceMotion"/> and
        /// never overwritten.
        /// <para>
        /// It exists so <see cref="MotionPreference.FollowSystem"/> can go <b>back</b> to the OS
        /// answer after the user has overridden it. Without it, the first override would destroy
        /// the only copy of the OS answer, and "follow the system" could only be honoured by
        /// re-reading the OS — which this type promises not to do, and which would shell out to a
        /// process every time a radio button moved.
        /// </para>
        /// </summary>
        bool SystemReduceMotion { get; }
    }
}
