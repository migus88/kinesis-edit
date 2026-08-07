using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// An <see cref="IVDriveEjector"/> that records the volumes it was asked to eject and returns
    /// a canned result. It replaces the platform ejector — on macOS a real <c>diskutil unmount</c>
    /// child process — in the tests of <see cref="Core.Profiles.ProfileSession"/>'s save path.
    /// </summary>
    internal sealed class RecordingVDriveEjector : IVDriveEjector
    {
        public bool IsSupported => true;

        /// <summary>Every volume root handed to <see cref="Eject"/>, in call order.</summary>
        public List<string> EjectedPaths { get; } = [];

        /// <summary>What <see cref="Eject"/> answers; success by default.</summary>
        public VDriveEjectResult Result { get; set; } = new() { Succeeded = true };

        public VDriveEjectResult Eject(string volumeRootPath)
        {
            EjectedPaths.Add(volumeRootPath);

            return Result;
        }
    }
}
