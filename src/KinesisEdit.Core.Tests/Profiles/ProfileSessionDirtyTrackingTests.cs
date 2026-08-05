using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// <see cref="ProfileSession.IsDirty"/>: false right after <see cref="ProfileSession.Load"/>,
    /// true after a mutation, false again once the mutation is reverted to the original value —
    /// computed by re-serializing and comparing lines (<see cref="Profiles.ProfileSession"/> doc:
    /// "reuse the serializers, that's the point"), never a bespoke model comparison.
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

        private static KeyDefinition Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");
        }
    }
}
