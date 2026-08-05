using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled <see cref="IFirmwareUpdatePresenter"/> recording every device it was asked to check.</summary>
    internal sealed class FakeFirmwareUpdatePresenter : IFirmwareUpdatePresenter
    {
        public List<DeviceSnapshot> PresentedDevices { get; } = [];

        public Task PresentAsync(DeviceSnapshot device)
        {
            PresentedDevices.Add(device);

            return Task.CompletedTask;
        }
    }
}
