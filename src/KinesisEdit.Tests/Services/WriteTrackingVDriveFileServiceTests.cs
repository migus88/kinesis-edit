using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The decorator wired at the composition root so every v-Drive write in the app opens a write
    /// bracket with no call-site changes. Each test asks the same question from inside the inner
    /// service: was the app reported as writing while this call was running?
    /// </summary>
    public class WriteTrackingVDriveFileServiceTests
    {
        [Fact]
        public void WriteAllLines_WhileItRuns_ReportsTheAppAsWriting()
        {
            var activity = new VDriveWriteActivity();
            var inner = new RecordingFileService();
            var service = new WriteTrackingVDriveFileService(inner, activity);
            inner.OnCall = () => Assert.True(activity.IsWriting);

            service.WriteAllLines("/fake/TKO/layouts/layout1.txt", ["[key]>[key]"], allowCreate: true);

            Assert.False(activity.IsWriting);
            Assert.Equal(1, inner.CallCount);
        }

        [Fact]
        public void UpdateSettingsFile_WhileItRuns_ReportsTheAppAsWriting()
        {
            var activity = new VDriveWriteActivity();
            var inner = new RecordingFileService();
            var service = new WriteTrackingVDriveFileService(inner, activity);
            inner.OnCall = () => Assert.True(activity.IsWriting);

            service.UpdateSettingsFile("/fake/TKO/settings/app_settings.txt", [new KeyValuePair<string, string>("v_drive", "on")]);

            Assert.False(activity.IsWriting);
            Assert.Equal(1, inner.CallCount);
        }

        /// <summary>
        /// A read opens nothing. The detection service re-reads a version file on every scan, so
        /// bracketing reads would report the app as writing for most of every pass and the watcher
        /// would skip the ticks it exists for.
        /// </summary>
        [Fact]
        public void ReadAllLines_WhileItRuns_ReportsNothing()
        {
            var activity = new VDriveWriteActivity();
            var inner = new RecordingFileService();
            var service = new WriteTrackingVDriveFileService(inner, activity);
            inner.OnCall = () => Assert.False(activity.IsWriting);

            var lines = service.ReadAllLines("/fake/TKO/version.txt");

            Assert.False(activity.IsWriting);
            Assert.Equal(1, inner.CallCount);
            Assert.Equal(RecordingFileService.ReadResult, lines);
        }

        /// <summary>
        /// A write that throws is the ordinary case on a demo drive and on a volume that went away
        /// mid-save. The bracket has to close anyway, or the watcher would be muted for the rest of
        /// the session.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AFailedWrite_Always_ClosesItsBracket(bool isSettingsMerge)
        {
            var activity = new VDriveWriteActivity();
            var inner = new RecordingFileService
            {
                Failure = new IOException("the drive went away")
            };
            var service = new WriteTrackingVDriveFileService(inner, activity);

            Assert.Throws<IOException>(() =>
            {
                if (isSettingsMerge)
                {
                    service.UpdateSettingsFile("/fake/TKO/settings/app_settings.txt", []);
                }
                else
                {
                    service.WriteAllLines("/fake/TKO/layouts/layout1.txt", []);
                }
            });

            Assert.False(activity.IsWriting);
        }

        /// <summary>Every argument reaches the inner service unchanged; this decorator alters nothing.</summary>
        [Fact]
        public void EveryCall_Always_ForwardsItsArgumentsUntouched()
        {
            var inner = new RecordingFileService();
            var service = new WriteTrackingVDriveFileService(inner, new VDriveWriteActivity());

            service.ReadAllLines("/fake/TKO/version.txt");
            service.WriteAllLines("/fake/TKO/layouts/layout1.txt", ["one", "two"], allowCreate: true);
            service.UpdateSettingsFile(
                "/fake/TKO/settings/app_settings.txt",
                [new KeyValuePair<string, string>("cust_color_1", "255 0 0")],
                ["cust_color_2"]);

            Assert.Equal(
                new[] { "/fake/TKO/version.txt", "/fake/TKO/layouts/layout1.txt", "/fake/TKO/settings/app_settings.txt" },
                inner.Paths);
            Assert.Equal(new[] { "one", "two" }, inner.WrittenLines);
            Assert.True(inner.AllowCreate);
            Assert.Equal(new[] { new KeyValuePair<string, string>("cust_color_1", "255 0 0") }, inner.Values);
            Assert.Equal(new[] { "cust_color_2" }, inner.RemovedKeys);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WithAMissingCollaborator_Throws(bool isFileServiceMissing)
        {
            Assert.Throws<ArgumentNullException>(() => new WriteTrackingVDriveFileService(
                isFileServiceMissing ? null! : new RecordingFileService(),
                isFileServiceMissing ? new VDriveWriteActivity() : null!));
        }

        /// <summary>
        /// The decorator was written against exactly these three members, and it must decorate all of
        /// them. A member added to <see cref="IVDriveFileService"/> fails here, which is the point:
        /// the decision "does this one open a bracket" has to be made deliberately rather than
        /// inherited by whatever the compiler lets through.
        /// </summary>
        [Fact]
        public void IVDriveFileService_Exposes_ExactlyTheMembersThisDecoratorCovers()
        {
            var members = typeof(IVDriveFileService)
                .GetMethods()
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "ReadAllLines", "UpdateSettingsFile", "WriteAllLines" }, members);
        }

        /// <summary>
        /// Inner file service that runs a hook <em>inside</em> the call — the only window in which
        /// "is a write bracket open right now" can be observed — and that can be told to fail.
        /// </summary>
        private sealed class RecordingFileService : IVDriveFileService
        {
            public static readonly string[] ReadResult = ["Model Name: TKO"];

            public Action? OnCall { get; set; }

            public Exception? Failure { get; set; }

            public int CallCount { get; private set; }

            public List<string> Paths { get; } = [];

            public IReadOnlyList<string> WrittenLines { get; private set; } = [];

            public bool AllowCreate { get; private set; }

            public IReadOnlyList<KeyValuePair<string, string>> Values { get; private set; } = [];

            public IReadOnlyList<string> RemovedKeys { get; private set; } = [];

            public IReadOnlyList<string> ReadAllLines(string path)
            {
                Record(path);

                return ReadResult;
            }

            public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
            {
                WrittenLines = lines;
                AllowCreate = allowCreate;

                Record(path);
            }

            public void UpdateSettingsFile(
                string path,
                IEnumerable<KeyValuePair<string, string>> values,
                IEnumerable<string>? removedKeys = null)
            {
                Values = [.. values];
                RemovedKeys = removedKeys is null ? [] : [.. removedKeys];

                Record(path);
            }

            private void Record(string path)
            {
                CallCount++;
                Paths.Add(path);

                OnCall?.Invoke();

                if (Failure is not null)
                {
                    throw Failure;
                }
            }
        }
    }
}
