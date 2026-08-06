namespace KinesisEdit.Services
{
    /// <summary>
    /// Reads the operating system's "reduce motion" accessibility setting. Abstracted so the
    /// answer can be faked in tests and so each platform's way of asking is isolated: nothing
    /// outside an implementation of this interface knows how the setting is stored.
    /// </summary>
    public interface IReduceMotionDetector
    {
        /// <summary>
        /// True or false when the platform reported the setting, null when it could not be read —
        /// the platform has no such setting, the user never set it, or the read failed. A null is
        /// not an error: the caller treats "unknown" as motion-on.
        /// </summary>
        bool? Detect();
    }
}
