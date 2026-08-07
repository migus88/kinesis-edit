using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The macros-per-profile limit of specs/06-macros.md §6 and the firmware gate of
    /// specs/09-firmware.md §2 that raises it. These facts lived on the old <c>MacroPanelViewModel</c>
    /// until issue #93 decomposed it; the rule they guard is unchanged and is now asked for by two
    /// surfaces — the key inspector's Macro panel, which refuses a macro past it, and the Macros
    /// tab's count meter.
    /// </summary>
    public sealed class MacroLimitsTests
    {
        [Fact]
        public void ResolveMaxMacroCount_OnAFreestyleEdgeBelowTheGate_IsTheBaseline()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.339"));

            // specs/09-firmware.md §2: FS Edge/Pro reach 100 macros only from firmware 1.0.340.
            Assert.Equal(24, MacroLimits.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_OnAFreestyleEdgeAtTheGate_IsRaised()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.340"));

            Assert.Equal(100, MacroLimits.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_InDemoMode_PassesTheGate()
        {
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdge,
                VDriveConnectionStatus.NotDetected,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdge, "1.0.339"));

            Assert.True(snapshot.IsDemoMode);
            Assert.Equal(100, MacroLimits.ResolveMaxMacroCount(snapshot));
        }

        [Fact]
        public void ResolveMaxMacroCount_OnAnUngatedDevice_IsTheCatalogBaseline()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);

            // The RGB has no ExpandedMacroCount gate and no raised value: 100 either way.
            Assert.Equal(100, MacroLimits.ResolveMaxMacroCount(snapshot));
        }

        /// <summary>
        /// specs/06-macros.md §6 states a macros-per-layout figure for the Freestyle, RGB and
        /// Advantage360 families and <b>none</b> for the Advantage2, which the catalog carries as a
        /// null. Null is "no limit", never "limit 0".
        /// </summary>
        [Fact]
        public void ResolveMaxMacroCount_OnADeviceThatStatesNoCount_IsNoLimitAtAll()
        {
            var macros = DeviceCatalog.GetById(DeviceId.Advantage2).Macros;

            Assert.Null(macros.MaxMacroCount);
            Assert.Null(macros.GatedMaxMacroCount);
            Assert.Null(MacroLimits.ResolveMaxMacroCount(TestDevices.CreateSnapshot(DeviceId.Advantage2)));
        }
    }
}
