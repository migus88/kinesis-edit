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
    /// <para>
    /// It answers <b>per profile number</b> first (<see cref="SessionsByProfile"/>), which is what a
    /// real drive does and what the editor's session cache has to be tested against — nine numbers,
    /// nine files. <see cref="SessionToReturn"/> is the fallback for the many suites that only ever
    /// open one profile: with the dictionary empty it behaves exactly as it always did, including
    /// being <i>filled in</i> on the first load so a test can reach the session it got.
    /// </para>
    /// </summary>
    internal sealed class FakeProfileSessionFactory : IProfileSessionFactory
    {
        /// <summary>Every load, in call order.</summary>
        public List<LoadCall> LoadCalls { get; } = [];

        /// <summary>How often <see cref="Load"/> was called.</summary>
        public int LoadCallCount => LoadCalls.Count;

        /// <summary>
        /// The session each profile number loads, when the test stages one. Consulted before
        /// <see cref="SessionToReturn"/>, so a suite that stages numbers never has to think about
        /// the fallback.
        /// </summary>
        public Dictionary<int, FakeProfileSession> SessionsByProfile { get; } = [];

        /// <summary>The session to hand back; one is built for the device on first use when unset.</summary>
        public FakeProfileSession? SessionToReturn { get; set; }

        /// <summary>When set, <see cref="Load"/> throws it instead of returning a session.</summary>
        public Exception? ExceptionToThrow { get; set; }

        /// <summary>
        /// Stages a factory-default session at <paramref name="profileNumber"/> and hands it back
        /// so the test can edit it. Its <see cref="FakeProfileSession.ProfileNumber"/> is the number
        /// it is filed under, because a session that reported another one would make the editor's
        /// cache disagree with its own picker.
        /// </summary>
        public FakeProfileSession Stage(int profileNumber, DeviceId device)
        {
            var session = new FakeProfileSession(KeyboardLayout.Create(device))
            {
                ProfileNumber = profileNumber
            };

            SessionsByProfile[profileNumber] = session;

            return session;
        }

        public IProfileSession Load(VDriveLocation location, DeviceId device, int profileNumber)
        {
            LoadCalls.Add(new LoadCall(location, device, profileNumber));

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (SessionsByProfile.TryGetValue(profileNumber, out var staged))
            {
                return staged;
            }

            return SessionToReturn ??= new FakeProfileSession(KeyboardLayout.Create(device));
        }

        /// <summary>One recorded load.</summary>
        internal sealed record LoadCall(VDriveLocation Location, DeviceId Device, int ProfileNumber);
    }
}
