using Avalonia;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Turns a <see cref="MotionPreference"/> into the app's live <see cref="IMotionSettings"/>
    /// state and re-points the motion alias resources at the matching set.
    /// <para>
    /// <b>The second half is not optional.</b> <see cref="MotionResourceBinder"/>'s aliases are
    /// written into <c>Application.Resources</c> and are <i>not</i> recomputed on their own, so
    /// flipping <see cref="IMotionSettings.ReduceMotion"/> without re-running
    /// <see cref="MotionResourceBinder.Apply"/> changes nothing anybody can see. Everything that
    /// changes the motion preference goes through here, and nothing sets
    /// <see cref="IMotionSettings.ReduceMotion"/> by hand.
    /// </para>
    /// <para>
    /// It is a bridging service in the sense of app-shell.md invariant 8 — it touches Avalonia
    /// because the resource dictionary is Avalonia's — and it is as thin as that allows: the
    /// decision itself is <see cref="Resolve"/>, which is pure and toolkit-free.
    /// </para>
    /// </summary>
    public static class MotionPreferenceApplier
    {
        /// <summary>
        /// What <paramref name="preference"/> means, given the OS answer
        /// <paramref name="systemReduceMotion"/> the app captured at startup. An unrecognised
        /// preference follows the system, which is the app's behaviour before there was a choice.
        /// </summary>
        public static bool Resolve(MotionPreference preference, bool systemReduceMotion)
        {
            return preference switch
            {
                MotionPreference.AlwaysReduce => true,
                MotionPreference.NeverReduce => false,
                _ => systemReduceMotion
            };
        }

        /// <summary>
        /// Resolves <paramref name="preference"/> onto <paramref name="motionSettings"/> and
        /// re-binds every motion alias on <paramref name="application"/>. Safe to call repeatedly
        /// and safe to call live — the views bind the aliases with <c>DynamicResource</c>, so a
        /// screen already on display re-points with them.
        /// </summary>
        public static void Apply(Application application, IMotionSettings motionSettings, MotionPreference preference)
        {
            ArgumentNullException.ThrowIfNull(application);
            ArgumentNullException.ThrowIfNull(motionSettings);

            motionSettings.ReduceMotion = Resolve(preference, motionSettings.SystemReduceMotion);

            MotionResourceBinder.Apply(application, motionSettings);
        }
    }
}
