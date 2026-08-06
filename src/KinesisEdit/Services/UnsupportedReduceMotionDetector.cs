namespace KinesisEdit.Services
{
    /// <summary>
    /// The <see cref="IReduceMotionDetector"/> for platforms whose accessibility setting the app
    /// does not read yet — Windows and Linux. It always answers "unknown", which the caller
    /// resolves to motion-on. Replacing it with a real reader per platform is a later issue and
    /// needs no change anywhere above this interface.
    /// </summary>
    public sealed class UnsupportedReduceMotionDetector : IReduceMotionDetector
    {
        /// <summary>Always null: this platform's setting is not read.</summary>
        public bool? Detect()
        {
            return null;
        }
    }
}
