using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.RevertLayout"/> and <see cref="ProfileSession.RevertLighting"/>
    /// (issue #133): the editor's <c>Discard changes</c> is scoped to the page the user is on, so
    /// the two files a profile is made of have to be revertible <b>one at a time</b>.
    /// <para>
    /// The claim each test makes is a version of the same one: reverting a file restores exactly the
    /// bytes <see cref="ProfileSession.IsDirty"/> compares against, leaves the other file alone, and
    /// needs no drive — the baseline is in memory, so it works over a volume that has since gone
    /// away. Re-reading the drive, which is what the app would otherwise have had to do, has none of
    /// those three properties.
    /// </para>
    /// </summary>
    public sealed class ProfileSessionRevertTests
    {
        [Fact]
        public void RevertLayout_AfterEditingBothFiles_RestoresTheLayoutAndLeavesTheLightingEdited()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);
            var lighting = (LightingModel)session.Lighting!;

            RemapTheBKey(session, "c");
            lighting.TopLayer.EffectColor = new LedColor(1, 2, 3);

            Assert.True(session.IsDirty);

            session.RevertLayout();

            // The layout is back. Note the re-read: reverting builds a BRAND-NEW KeyboardLayout, so
            // a caller holding the old reference is holding the edits it just asked to lose.
            Assert.Equal(Key("b").Code, FindBKey(session).ModifiedOrOriginalKey.Code);

            // ...and the lighting edit is untouched, which is the whole point of the pair.
            Assert.Same(lighting, session.Lighting);
            Assert.Equal(new LedColor(1, 2, 3), lighting.TopLayer.EffectColor);

            // Still dirty, because half of it is still edited — and the session says so through the
            // very comparison a save would make.
            Assert.True(session.IsDirty);
        }

        [Fact]
        public void RevertLighting_AfterEditingBothFiles_RestoresTheLightingAndLeavesTheLayoutEdited()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);
            var layout = session.Layout;
            var originalColor = ((LightingModel)session.Lighting!).TopLayer.EffectColor;

            RemapTheBKey(session, "c");
            ((LightingModel)session.Lighting!).TopLayer.EffectColor = new LedColor(1, 2, 3);

            session.RevertLighting();

            Assert.Equal(originalColor, ((LightingModel)session.Lighting!).TopLayer.EffectColor);

            // The layout model is the same instance and still carries the remap.
            Assert.Same(layout, session.Layout);
            Assert.Equal(Key("c").Code, FindBKey(session).ModifiedOrOriginalKey.Code);

            Assert.True(session.IsDirty);
        }

        [Fact]
        public void RevertingBothHalves_ReturnsTheSessionToClean()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            RemapTheBKey(session, "c");
            ((LightingModel)session.Lighting!).TopLayer.EffectColor = new LedColor(1, 2, 3);

            session.RevertLayout();
            session.RevertLighting();

            // The baseline IsDirty compares against IS what a revert restores, so this is exact
            // rather than approximate: the session cannot be left "reverted but still dirty".
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void RevertLayout_ReplacesTheLayoutInstance()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);
            var before = session.Layout;

            session.RevertLayout();

            // Same contract as Load and Import: a fresh model, never a model mutated in place. It is
            // what the app-layer caller has to rebuild its layer view models against.
            Assert.NotSame(before, session.Layout);
        }

        [Fact]
        public void RevertLayout_WithTheDriveGone_StillRestoresTheProfile()
        {
            var drive = new ProfileFixtureDrive();
            ProfileSession session;

            try
            {
                session = LoadRgbSession(drive);
            }
            finally
            {
                // The volume is unmounted under the editor. A revert that re-read the drive would
                // throw here; this one reads the lines it captured at load.
                drive.Dispose();
            }

            RemapTheBKey(session, "c");

            session.RevertLayout();

            Assert.Equal(Key("b").Code, FindBKey(session).ModifiedOrOriginalKey.Code);
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void RevertLayout_WithUnparseableLines_KeepsReportingThem()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]", "fn [ZZZ]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            Assert.Single(session.InvalidLines);

            RemapTheBKey(session, "c");

            session.RevertLayout();

            // The list goes back to the one captured at load, not to whatever re-parsing the
            // baseline produced (which is empty — an unkept invalid line is not in the baseline).
            // Otherwise a discard would quietly erase the "some lines could not be applied" report.
            Assert.Single(session.InvalidLines);
            Assert.Equal("fn [ZZZ]>[a]", session.InvalidLines[0].Text);
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void RevertLighting_OnADeviceWithNoLedFile_DoesNothingAndThrowsNothing()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            RemapTheBKey(session, "c");

            // Unlike Import, which throws for a device with no led file, a revert's postcondition
            // already holds there: no led model, nothing that could be dirty, nothing to restore.
            session.RevertLighting();

            Assert.Null(session.Lighting);
            Assert.True(session.IsDirty);
        }

        [Fact]
        public void Revert_WritesNothingToTheDrive()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);
            var layoutPath = drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1);
            var lightingPath = drive.GetLightingFilePath(DeviceId.FreestyleEdgeRgb, 1);
            var layoutStamp = File.GetLastWriteTimeUtc(layoutPath);
            var lightingStamp = File.GetLastWriteTimeUtc(lightingPath);

            RemapTheBKey(session, "c");
            ((LightingModel)session.Lighting!).TopLayer.EffectColor = new LedColor(1, 2, 3);

            session.RevertLayout();
            session.RevertLighting();

            Assert.Equal(layoutStamp, File.GetLastWriteTimeUtc(layoutPath));
            Assert.Equal(lightingStamp, File.GetLastWriteTimeUtc(lightingPath));
        }

        private static ProfileSession LoadRgbSession(ProfileFixtureDrive drive)
        {
            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            return ProfileSession.Load(drive.CreateLocation(DeviceId.FreestyleEdgeRgb), DeviceId.FreestyleEdgeRgb, 1);
        }

        private static void RemapTheBKey(ProfileSession session, string token)
        {
            Assert.True(FindBKey(session).Remap(Key(token)));
        }

        private static KeyboardKey FindBKey(ProfileSession session)
        {
            return session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!;
        }

        private static KeyDefinition Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");
        }
    }
}
