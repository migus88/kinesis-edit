using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Services
{
    /// <summary>
    /// macOS <see cref="IReduceMotionDetector"/>. It runs
    /// <c>defaults read com.apple.universalaccess reduceMotion</c>, which prints <c>1</c> or
    /// <c>0</c> for the "Reduce motion" switch in System Settings &gt; Accessibility &gt; Display.
    /// <para>
    /// Shelling out is not elegant, but the alternative is Objective-C interop into
    /// <c>NSWorkspace.accessibilityDisplayShouldReduceMotion</c>, which costs far more than the
    /// question is worth. It is confined to this class and nothing above
    /// <see cref="IReduceMotionDetector"/> knows about it. The command exits non-zero when the
    /// user has never touched the switch, which is not a failure — it means "not reduced" — and
    /// is reported as an unknown answer for the caller to default.
    /// </para>
    /// </summary>
    public sealed class MacOsReduceMotionDetector : IReduceMotionDetector
    {
        private const string DefaultsFileName = "defaults";

        private const string ReadVerb = "read";

        private const string UniversalAccessDomain = "com.apple.universalaccess";

        private const string ReduceMotionKey = "reduceMotion";

        private readonly IProcessRunner _processRunner;

        /// <summary>Creates the detector on top of the given process runner.</summary>
        public MacOsReduceMotionDetector(IProcessRunner processRunner)
        {
            ArgumentNullException.ThrowIfNull(processRunner);

            _processRunner = processRunner;
        }

        /// <summary>
        /// Reads the setting. Returns null when <c>defaults</c> exited non-zero (the key is not
        /// set) or printed something this does not recognize.
        /// </summary>
        public bool? Detect()
        {
            var result = _processRunner.Run(DefaultsFileName, [ReadVerb, UniversalAccessDomain, ReduceMotionKey]);

            if (result.ExitCode != 0)
            {
                return null;
            }

            return ParseFlag(result.StandardOutput);
        }

        private static bool? ParseFlag(string standardOutput)
        {
            var value = standardOutput.Trim();

            if (value.Equals("1", StringComparison.Ordinal)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Equals("0", StringComparison.Ordinal)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return null;
        }
    }
}
