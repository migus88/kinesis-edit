using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.IsDirty"/>: false right after <see cref="ProfileSession.Load"/>,
    /// true after a mutation, false again once the mutation is reverted to the original value —
    /// computed by re-serializing and comparing lines (<see cref="Profiles.ProfileSession"/> doc:
    /// "reuse the serializers, that's the point"), never a bespoke model comparison.
    /// <para>
    /// The second half of the file is <b>the baseline itself</b> (issue #133): what it is compared
    /// against, and when that moves. It used to be frozen at <see cref="ProfileSession.Load"/>, which
    /// was invisible while the editor held one session and simply asserted its own flag false after
    /// a save; with the editor now asking every session it holds, a frozen baseline means a saved
    /// profile reports itself dirty for ever and the next press of Save rewrites it for nothing.
    /// </para>
    /// </summary>
    public sealed class ProfileSessionDirtyTrackingTests
    {
        [Fact]
        public void IsDirty_AfterLoadThenRemapThenRevert_ReturnsToFalse()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            Assert.False(session.IsDirty);

            var bKey = session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!;
            var originalKey = bKey.OriginalKey;

            Assert.True(bKey.Remap(Key("c")));
            Assert.True(session.IsDirty);

            Assert.True(bKey.Remap(originalKey));
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void IsDirty_AfterLightingMutationThenRevert_ReturnsToFalse()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            var location = drive.CreateLocation(DeviceId.FreestyleEdgeRgb);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdgeRgb, 1);

            Assert.False(session.IsDirty);

            var lighting = (LightingModel)session.Lighting!;
            var originalColor = lighting.TopLayer.EffectColor;

            lighting.TopLayer.EffectColor = new LedColor(1, 2, 3);
            Assert.True(session.IsDirty);

            lighting.TopLayer.EffectColor = originalColor;
            Assert.False(session.IsDirty);
        }

        // ===== The baseline, and when it moves ===============================================

        [Fact]
        public void IsDirty_AfterASuccessfulSave_IsFalse()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            Remap(session, "c");
            ((LightingModel)session.Lighting!).TopLayer.EffectColor = new LedColor(1, 2, 3);

            Assert.True(session.IsDirty);
            Assert.True(session.Save().Success);

            // The lines just written ARE what is on the drive, so they are what the model is
            // compared against from here on — both halves of the profile.
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void Save_OverASessionThatIsNoLongerDirty_WritesTheSameBytesAndTheSessionStaysClean()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            Remap(session, "c");
            session.Save();

            var afterFirst = File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1));

            Assert.True(session.Save().Success);

            Assert.Equal(afterFirst, File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1)));
            Assert.False(session.IsDirty);
        }

        [Fact]
        public void IsDirty_AfterASaveAsToAnotherSlot_StaysTrue()
        {
            // A SaveAs copies the model somewhere else; layout1.txt is exactly as it was, so this
            // session's work is still unsaved and its baseline must not have moved. Getting this
            // wrong is silent: the caller is told the profile is clean and the edit is only in the
            // slot it was copied to.
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            Remap(session, "c");

            Assert.True(session.SaveAs(5, setAsStartup: false).Success);

            Assert.True(session.IsDirty);
            Assert.Equal(
                new[] { "[F1]>[a]" },
                File.ReadAllLines(drive.GetLayoutFilePath(DeviceId.FreestyleEdgeRgb, 1)));
        }

        [Fact]
        public void IsDirty_AfterASaveValidationStopped_StaysTrue()
        {
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            Remap(session, "c");

            var limit = session.Layout.Device.Macros.MaxMacroCount
                        ?? throw new InvalidOperationException("The Freestyle Edge RGB is expected to cap its macro count.");

            FillMacroSlots(session.Layout, limit + 1);

            var result = session.Save();

            Assert.False(result.Success);
            Assert.NotEmpty(result.Violations);

            // Nothing was written, so nothing about what is on the drive changed.
            Assert.True(session.IsDirty);
        }

        [Fact]
        public void RevertLayout_AfterASave_RestoresTheSavedStateRatherThanTheLoadedOne()
        {
            // The user-visible half of the same rule: `Discard changes` means "go back to what is on
            // the keyboard", and after a save that is the saved state. A baseline frozen at load
            // would silently take back committed work here.
            using var drive = new ProfileFixtureDrive();

            var session = LoadRgbSession(drive);

            Remap(session, "c");
            Assert.True(session.Save().Success);

            Remap(session, "d");

            session.RevertLayout();

            Assert.Equal(
                Key("c").Code,
                session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!.ModifiedOrOriginalKey.Code);
            Assert.False(session.IsDirty);
        }

        private static ProfileSession LoadRgbSession(ProfileFixtureDrive drive)
        {
            drive.WriteLayoutFile(DeviceId.FreestyleEdgeRgb, 1, "[F1]>[a]");
            drive.WriteLightingFile(DeviceId.FreestyleEdgeRgb, 1, "[mono]>[10][20][30]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdgeRgb);

            return ProfileSession.Load(drive.CreateLocation(DeviceId.FreestyleEdgeRgb), DeviceId.FreestyleEdgeRgb, 1);
        }

        private static void Remap(ProfileSession session, string token)
        {
            Assert.True(session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!.Remap(Key(token)));
        }

        /// <summary>
        /// Drives the layout past its macro-count limit, the same way
        /// <c>KinesisEdit.Tests</c>' own helper does — this project cannot see that one.
        /// </summary>
        private static void FillMacroSlots(KeyboardLayout layout, int count)
        {
            var added = 0;

            foreach (var key in layout.Layers[0].Keys)
            {
                if (!key.CanAssignMacro)
                {
                    continue;
                }

                for (var slot = 1; slot <= key.Macros.Count && added < count; slot++)
                {
                    key.SetMacro(slot, layout.CreateMacro());

                    added++;
                }

                if (added >= count)
                {
                    return;
                }
            }
        }

        private static KeyDefinition Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");
        }
    }
}
