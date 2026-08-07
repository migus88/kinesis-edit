namespace KinesisEdit.Services
{
    /// <summary>
    /// Whether the user wants the design's full motion budget, the reduced one, or whatever the
    /// operating system's accessibility switch says. A <b>host</b> preference, stored in
    /// <see cref="HostPreferences"/>.
    /// <para>
    /// This is a *preference*, not the flag the styles read: <see cref="IMotionSettings.ReduceMotion"/>
    /// is still the single switch, and <see cref="MotionPreferenceApplier"/> resolves one of these
    /// plus <see cref="IMotionSettings.SystemReduceMotion"/> into it. Keeping the two apart is what
    /// lets <see cref="FollowSystem"/> go *back* to the OS answer after an override — the OS answer
    /// is remembered rather than recomputed.
    /// </para>
    /// </summary>
    public enum MotionPreference
    {
        /// <summary>Use the OS accessibility setting, as the app did before there was a choice. The default.</summary>
        FollowSystem = 0,

        /// <summary>Reduced motion — fades only — whatever the OS says.</summary>
        AlwaysReduce = 1,

        /// <summary>The full motion budget, whatever the OS says.</summary>
        NeverReduce = 2,
    }
}
