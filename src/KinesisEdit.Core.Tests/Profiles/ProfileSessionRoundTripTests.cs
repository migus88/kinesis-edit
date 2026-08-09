using System.Linq;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Profiles;

namespace KinesisEdit.Core.Tests.Profiles
{
    /// <summary>
    /// Fixture-drive round trips through <see cref="ProfileSession"/>: a Gen1 device with no
    /// lighting file at all (Freestyle Edge, whose own <c>lighting/</c> folder is deliberately
    /// out of this module's scope, <see cref="Profiles.ProfileLightingCodec"/>) and the TKO,
    /// which exercises the edge-lighting split/merge on top of the base led-file grammar.
    /// </summary>
    public sealed class ProfileSessionRoundTripTests
    {
        [Fact]
        public void Load_ThenSave_ForFreestyleEdge_WritesTheMutatedLayoutAndNeverTouchesLighting()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.FreestyleEdge, 1, "[F1]>[a]", "fn [4]>[LED]");
            drive.WriteSettingsFile(DeviceId.FreestyleEdge);

            var location = drive.CreateLocation(DeviceId.FreestyleEdge);
            var session = ProfileSession.Load(location, DeviceId.FreestyleEdge, 1);

            Assert.Null(session.Lighting);
            Assert.Empty(session.InvalidLines);

            var bKey = session.Layout.Layers[0].FindByPositionKeyCode(Key("b").Code)!;
            Assert.True(bKey.Remap(Key("c")));

            var expectedLayoutLines = LayoutFileSerializer.Serialize(session.Layout, session.InvalidLines);

            var result = session.Save();

            Assert.True(result.Success);
            Assert.Empty(result.Violations);
            Assert.Equal(
                "…use the Refresh Shortcut (SmartSet + Layout) or simply close the v-Drive (SmartSet + F8). "
                + "To load this layout to the keyboard press SmartSet + 1.",
                result.PostSaveMessage);

            Assert.Equal(
                ProfileFixtureDrive.BuildFileBytes(expectedLayoutLines.ToArray()),
                File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.FreestyleEdge, 1)));
            Assert.False(File.Exists(drive.GetLightingFilePath(DeviceId.FreestyleEdge, 1)));
        }

        [Fact]
        public void Load_ThenSave_ForTko_SplitsAndMergesEdgeLightingLines()
        {
            using var drive = new ProfileFixtureDrive();

            drive.WriteLayoutFile(DeviceId.Tko, 1, "[F1]>[a]");
            drive.WriteLightingFile(
                DeviceId.Tko,
                1,
                "[wave]>[spd4][dirright]",
                "[mono_edge]>[255][0][0]",
                "fn [pulse]>[spd2]");
            drive.WriteSettingsFile(DeviceId.Tko, "startup_file=layout1.txt", "led_mode=led1.txt");

            var location = drive.CreateLocation(DeviceId.Tko);
            var session = ProfileSession.Load(location, DeviceId.Tko, 1);

            var lighting = Assert.IsType<TkoLightingModel>(session.Lighting);
            Assert.Equal(LightingMode.Wave, lighting.KeyBacklight.TopLayer.Mode);
            Assert.Equal(LightingDirection.Right, lighting.KeyBacklight.TopLayer.Direction);
            Assert.Equal(LightingMode.Pulse, lighting.KeyBacklight.FnLayer.Mode);
            Assert.Equal(LightingMode.Monochrome, lighting.Edge.TopLayer.Mode);
            Assert.Equal(new LedColor(255, 0, 0), lighting.Edge.TopLayer.EffectColor);

            var eKey = session.Layout.Layers[0].FindByPositionKeyCode(Key("e").Code)!;
            Assert.True(eKey.Remap(Key("r")));

            var expectedLayoutLines = LayoutFileSerializer.Serialize(session.Layout, session.InvalidLines);
            var expectedLightingLines = LedFileSerializer.SerializeTko(lighting);

            var result = session.Save();

            Assert.True(result.Success);
            Assert.Equal(
                "Use the Refresh Shortcut (SmartSet + Right Shift + B) to preview your Layout and Lighting updates "
                + "or simply Eject the \"TKO\" drive in File Explorer and then disconnect the v-Drive (SmartSet + Right Shift + V).",
                result.PostSaveMessage);

            Assert.Equal(
                ProfileFixtureDrive.BuildFileBytes(expectedLayoutLines.ToArray()),
                File.ReadAllBytes(drive.GetLayoutFilePath(DeviceId.Tko, 1)));
            Assert.Equal(
                ProfileFixtureDrive.BuildFileBytes(expectedLightingLines.ToArray()),
                File.ReadAllBytes(drive.GetLightingFilePath(DeviceId.Tko, 1)));
        }

        private static KeyDefinition Key(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");
        }
    }
}
