namespace KinesisEdit.Services
{
    /// <summary>
    /// The app-wide <see cref="IMotionSettings"/>. It asks an <see cref="IReduceMotionDetector"/>
    /// exactly once, in its constructor, and remembers the answer; nothing re-reads the OS while
    /// the app runs.
    /// <para>
    /// The answer is remembered <b>twice</b>: <see cref="ReduceMotion"/> is the live switch every
    /// style reads and anything may write, and <see cref="SystemReduceMotion"/> is the OS's answer,
    /// frozen. That is what makes <see cref="MotionPreference.FollowSystem"/> reversible — see
    /// <see cref="MotionPreferenceApplier"/>.
    /// </para>
    /// <para>
    /// Detection is never allowed to matter more than starting up: an unknown answer and a
    /// detector that throws both resolve to "motion on", which is the state the app was designed
    /// in. That is why the constructor swallows the exception rather than propagating it.
    /// </para>
    /// </summary>
    public sealed class MotionSettings : IMotionSettings
    {
        /// <summary>What an unreadable or failed detection resolves to: animate normally.</summary>
        private const bool FallbackReduceMotion = false;

        /// <summary>
        /// Builds the settings over the detector this platform supports — the real macOS reader,
        /// or the "don't know" detector on Windows and Linux until they get their own.
        /// </summary>
        public static MotionSettings CreateForCurrentPlatform()
        {
            return new MotionSettings(ReduceMotionDetector.CreateForCurrentPlatform());
        }

        /// <inheritdoc />
        public bool ReduceMotion { get; set; }

        /// <inheritdoc />
        public bool SystemReduceMotion { get; }

        /// <summary>
        /// Resolves the initial value from <paramref name="detector"/>, and keeps that answer
        /// separately in <see cref="SystemReduceMotion"/> so a later
        /// <see cref="MotionPreference.FollowSystem"/> can return to it without asking the OS
        /// again.
        /// </summary>
        public MotionSettings(IReduceMotionDetector detector)
        {
            ArgumentNullException.ThrowIfNull(detector);

            SystemReduceMotion = ResolveInitialValue(detector);
            ReduceMotion = SystemReduceMotion;
        }

        private static bool ResolveInitialValue(IReduceMotionDetector detector)
        {
            try
            {
                return detector.Detect() ?? FallbackReduceMotion;
            }
            catch (Exception)
            {
                // Deliberately broad: a detector shells out to an OS tool, and no failure mode of
                // an accessibility lookup — a missing binary, a permissions refusal, a hang that
                // was killed — is worth failing a launch over.
                return FallbackReduceMotion;
            }
        }
    }
}
