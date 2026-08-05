using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled <see cref="IDeviceEjectService"/> recording the volumes it was asked to eject.</summary>
    internal sealed class FakeDeviceEjectService : IDeviceEjectService
    {
        public bool IsSupported { get; set; } = true;

        public VDriveEjectResult ResultToReturn { get; set; } = new VDriveEjectResult
        {
            Succeeded = true
        };

        public List<string> EjectedPaths { get; } = [];

        /// <summary>When set, an eject stays in flight until the test completes this source.</summary>
        public TaskCompletionSource? Gate { get; set; }

        public async Task<VDriveEjectResult> EjectAsync(string volumeRootPath)
        {
            EjectedPaths.Add(volumeRootPath);

            if (Gate is not null)
            {
                await Gate.Task.ConfigureAwait(true);
            }

            return ResultToReturn;
        }
    }
}
