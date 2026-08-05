using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// Importing an external file over a loaded profile (specs/10-apps-and-ui.md's Import,
    /// classified by specs/07-lighting.md §1.4): the content is parsed through exactly the paths
    /// <see cref="ProfileSession.Load"/> uses, the model is replaced wholesale, nothing is
    /// written, and the session reports itself dirty until the user saves.
    /// </summary>
    public sealed class ProfileSessionImportTests
    {
        [Fact]
        public void Import_WithALayoutFile_ReplacesTheLayoutWithAFreshlyParsedOne()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");
            var originalLayout = session.Layout;

            var result = session.Import(ImportedFileKind.Layout, ["[F1]>[esc]"]);

            Assert.Equal(ImportedFileKind.Layout, result.Kind);
            Assert.NotSame(originalLayout, session.Layout);

            // The old rule is gone and the new one applied: 04 §4.2's full-model-wipe-on-load,
            // reached from a file the user picked instead of from the drive.
            Assert.Equal(1, session.Layout.ModifiedKeyCount);
            Assert.Equal("esc", FindModifiedToken(session));
        }

        [Fact]
        public void Import_WithALayoutFile_TracksItsUnparseableLinesLikeALoadDoes()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");

            var result = session.Import(ImportedFileKind.Layout, ["[F1]>[esc]", "[ZZZ]>[a]"]);

            var invalidLine = Assert.Single(result.InvalidLines);

            Assert.Equal("[ZZZ]>[a]", invalidLine.Text);
            Assert.Equal(2, invalidLine.LineNumber);
            Assert.False(invalidLine.Keep);
            Assert.Same(result.InvalidLines, session.InvalidLines);
        }

        [Fact]
        public void Import_WithALayoutFile_LeavesTheContentUnsavedAndTheSessionDirty()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");

            session.Import(ImportedFileKind.Layout, ["[F1]>[esc]"]);

            Assert.True(session.IsDirty);

            // Import writes nothing: the file on the drive still holds what was loaded.
            var onDrive = File.ReadAllLines(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1));

            Assert.Contains("[caps]>[a]", onDrive);
            Assert.DoesNotContain("[F1]>[esc]", onDrive);
        }

        [Fact]
        public void Save_AfterALayoutImport_WritesTheImportedContent()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");

            session.Import(ImportedFileKind.Layout, ["[F1]>[esc]"]);

            Assert.True(session.Save().Success);

            var onDrive = File.ReadAllLines(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1));

            Assert.Contains("[F1]>[esc]", onDrive);
            Assert.DoesNotContain("[caps]>[a]", onDrive);
        }

        [Fact]
        public void Import_WithALedFile_ReplacesTheLightingAndLeavesTheLayoutAlone()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");
            var originalLayout = session.Layout;

            var result = session.Import(ImportedFileKind.Lighting, ["[mono]>[10][20][30]"]);

            Assert.Equal(ImportedFileKind.Lighting, result.Kind);
            Assert.Same(originalLayout, session.Layout);
            Assert.Empty(result.InvalidLines);

            var lighting = Assert.IsType<LightingModel>(session.Lighting);

            Assert.Equal(LightingMode.Monochrome, lighting.TopLayer.Mode);
            Assert.True(session.IsDirty);
        }

        [Fact]
        public void Import_WithALedFileOnADeviceWithoutOne_Throws()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[caps]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var session = ProfileSession.Load(
                drive.CreateLocation(DeviceId.FreestyleEdge), DeviceId.FreestyleEdge, 1);

            Assert.Throws<NotSupportedException>(() => session.Import(ImportedFileKind.Lighting, ["[mono]>[10][20][30]"]));
        }

        [Fact]
        public void Import_WithAnUnknownKindOrNoLines_Throws()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");

            Assert.Throws<ArgumentNullException>(() => session.Import(ImportedFileKind.Layout, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Import((ImportedFileKind)7, ["[F1]>[esc]"]));
        }

        [Fact]
        public void Import_WithAnEmptyLayoutFile_ResetsTheProfileToItsFactoryState()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive, "[caps]>[a]");

            // 07 §1.4 sends everything the led heuristic rejects down the layout path, an empty
            // file included; 04 §4.2 then makes it a full wipe. Destructive, and the spec's.
            session.Import(ImportedFileKind.Layout, []);

            Assert.Equal(0, session.Layout.ModifiedKeyCount);
        }

        private static ProfileSession LoadRgbSession(ProfileFixtureDrive drive, params string[] layoutLines)
        {
            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, layoutLines);
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[spectrum]>[spd3]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            return ProfileSession.Load(drive.CreateLocation(DeviceId.FreestyleEdgeRgb), DeviceId.FreestyleEdgeRgb, 1);
        }

        private static string? FindModifiedToken(ProfileSession session)
        {
            foreach (var layer in session.Layout.Layers)
            {
                foreach (var key in layer.Keys)
                {
                    if (key.IsModified)
                    {
                        return key.ModifiedKey?.GetToken(TokenDialect.Gen1);
                    }
                }
            }

            return null;
        }
    }
}
