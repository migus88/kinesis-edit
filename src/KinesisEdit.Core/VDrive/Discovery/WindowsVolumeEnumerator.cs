namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Windows volume enumerator implementing the specs/03-vdrive-and-files.md §3.2 drive scan:
    /// all drives from <see cref="DriveInfo.GetDrives"/> of type removable, fixed, network,
    /// CD-ROM, or RAM disk are candidates, labeled with their volume label. Drives that are not
    /// ready, or that fail with an I/O or access error, are skipped. The legacy app's
    /// critical-error-dialog suppression during the scan is a Windows-API-level concern
    /// (<c>SetErrorMode</c>); <see cref="DriveInfo"/> raises no such dialogs, so no equivalent
    /// is needed here.
    /// </summary>
    public sealed class WindowsVolumeEnumerator : IVolumeEnumerator
    {
        /// <summary>Yields one candidate per ready drive of an accepted type; per-drive errors skip that drive.</summary>
        public IEnumerable<VolumeCandidate> EnumerateVolumes()
        {
            DriveInfo[] drives;

            try
            {
                drives = DriveInfo.GetDrives();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var drive in drives)
            {
                var candidate = TryDescribeDrive(drive);

                if (candidate is not null)
                {
                    yield return candidate.Value;
                }
            }
        }

        private static VolumeCandidate? TryDescribeDrive(DriveInfo drive)
        {
            try
            {
                if (!IsAcceptedDriveType(drive.DriveType))
                {
                    return null;
                }

                if (!drive.IsReady)
                {
                    return null;
                }

                return new VolumeCandidate(drive.RootDirectory.FullName, drive.VolumeLabel);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static bool IsAcceptedDriveType(DriveType driveType)
        {
            return driveType
                is DriveType.Removable
                or DriveType.Fixed
                or DriveType.Network
                or DriveType.CDRom
                or DriveType.Ram;
        }
    }
}
