using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IProfileSessionFactory"/>: records every load, hands back the staged
    /// session (building a factory-default one on demand), and can be made to throw the way a
    /// vanished drive or an unreadable file would.
    /// </summary>
    internal sealed class FakeProfileSessionFactory : IProfileSessionFactory
    {
        /// <summary>Every load, in call order.</summary>
        public List<LoadCall> LoadCalls { get; } = [];

        /// <summary>How often <see cref="Load"/> was called.</summary>
        public int LoadCallCount => LoadCalls.Count;

        /// <summary>The session to hand back; one is built for the device on first use when unset.</summary>
        public FakeProfileSession? SessionToReturn { get; set; }

        /// <summary>When set, <see cref="Load"/> throws it instead of returning a session.</summary>
        public Exception? ExceptionToThrow { get; set; }

        public IProfileSession Load(VDriveLocation location, DeviceId device, int profileNumber)
        {
            LoadCalls.Add(new LoadCall(location, device, profileNumber));

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return SessionToReturn ??= new FakeProfileSession(KeyboardLayout.Create(device));
        }

        /// <summary>One recorded load.</summary>
        internal sealed record LoadCall(VDriveLocation Location, DeviceId Device, int ProfileNumber);
    }
}
