using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Factory for the platform-appropriate <see cref="IReduceMotionDetector"/>, mirroring
    /// <see cref="VDriveEject.CreateForCurrentPlatform"/>.
    /// </summary>
    public static class ReduceMotionDetector
    {
        /// <summary>
        /// Returns a <see cref="MacOsReduceMotionDetector"/> on macOS — the primary platform —
        /// and an <see cref="UnsupportedReduceMotionDetector"/> everywhere else.
        /// </summary>
        public static IReduceMotionDetector CreateForCurrentPlatform()
        {
            if (OperatingSystem.IsMacOS())
            {
                return new MacOsReduceMotionDetector(new SystemProcessRunner());
            }

            return new UnsupportedReduceMotionDetector();
        }
    }
}
