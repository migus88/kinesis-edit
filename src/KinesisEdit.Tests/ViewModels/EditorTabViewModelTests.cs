using KinesisEdit.Core.Devices;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The tab strip is device-driven: a section a board could never carry is omitted, not shown
    /// disabled (issue #16). Which sections those are comes from the catalog's
    /// <c>LightingCapability</c>/<c>SettingsCapability</c>, never from a device id here.
    /// </summary>
    public sealed class EditorTabViewModelTests
    {
        [Fact]
        public void CreateAll_ForAPerKeyRgbBoard_HasAllFourSections()
        {
            var tabs = CreateAll(DeviceId.FreestyleEdgeRgb);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Lighting, EditorTab.Settings },
                tabs.Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_ForADeviceWithoutLighting_OmitsTheLightingTab()
        {
            // Freestyle Pro and Advantage2 have no lighting hardware at all (specs/02-devices.md),
            // so there is no lighting file to edit and no tab to open.
            Assert.DoesNotContain(EditorTab.Lighting, CreateAll(DeviceId.FreestylePro).Select(tab => tab.Tab));
            Assert.DoesNotContain(EditorTab.Lighting, CreateAll(DeviceId.Advantage2).Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_ForADeviceWithoutASettingsFile_OmitsTheSettingsTab()
        {
            Assert.DoesNotContain(EditorTab.Settings, CreateAll(DeviceId.SavantElite2).Select(tab => tab.Tab));
            Assert.DoesNotContain(EditorTab.Settings, CreateAll(DeviceId.CrossfireKeypad).Select(tab => tab.Tab));
            Assert.DoesNotContain(
                EditorTab.Settings,
                CreateAll(DeviceId.Advantage360Professional).Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_ForTheAdvantage2_IsKeysMacrosAndSettings()
        {
            var tabs = CreateAll(DeviceId.Advantage2);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Settings },
                tabs.Select(tab => tab.Tab));
            Assert.True(tabs[^1].IsEnabled);
        }

        [Fact]
        public void CreateAll_TheSettingsTab_IsEnabled()
        {
            var tabs = CreateAll(DeviceId.Tko);

            Assert.True(Assert.Single(tabs, tab => tab.Tab == EditorTab.Settings).IsEnabled);
        }

        [Fact]
        public void CreateAll_TheMacrosTab_IsAlwaysPresentAndDisabled()
        {
            // Issue #15 fills it in; until then a visibly unavailable tab beats a silent one.
            foreach (var device in DeviceCatalog.All)
            {
                var macros = Assert.Single(
                    EditorTabViewModel.CreateAll(device, isLightingEnabled: false),
                    tab => tab.Tab == EditorTab.Macros);

                Assert.False(macros.IsEnabled);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateAll_TheLightingTab_FollowsTheLightingEnabledFlag(bool isLightingEnabled)
        {
            // The one switch the lighting panel flips when it lands.
            var tabs = EditorTabViewModel.CreateAll(
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb),
                isLightingEnabled);

            Assert.Equal(isLightingEnabled, Assert.Single(tabs, tab => tab.Tab == EditorTab.Lighting).IsEnabled);
        }

        [Fact]
        public void CreateAll_ForEveryDevice_EnablesTheLightingTabExactlyWhereThePanelCanEditTheLedFile()
        {
            // The RGB is the only board whose led file is the two-layer key-backlight model the
            // panel edits; the TKO adds an edge section (#40) and the Advantage 360 has indicators
            // (#41), so their tabs stay visible-but-dark rather than opening an empty editor.
            foreach (var device in DeviceCatalog.All)
            {
                var isSupported = LightingTabViewModel.IsSupported(device);
                var lighting = EditorTabViewModel
                    .CreateAll(device, isSupported)
                    .SingleOrDefault(tab => tab.Tab == EditorTab.Lighting);

                if (device.Lighting.Kind == LightingKind.None)
                {
                    Assert.Null(lighting);

                    continue;
                }

                Assert.NotNull(lighting);
                Assert.Equal(device.Id == DeviceId.FreestyleEdgeRgb, lighting.IsEnabled);
            }
        }

        [Fact]
        public void CreateAll_ForEveryDevice_ShowsTheSettingsTabExactlyWhenThereAreRowsToShow()
        {
            // The tab predicate and the row factory must agree: a tab with an empty panel behind
            // it, or a panel no tab can reach, are both bugs.
            foreach (var device in DeviceCatalog.All)
            {
                var hasTab = EditorTabViewModel
                    .CreateAll(device, isLightingEnabled: false)
                    .Any(tab => tab.Tab == EditorTab.Settings);

                Assert.Equal(KeyboardSettingsRows.Create(device.Settings).Count > 0, hasTab);
            }
        }

        [Fact]
        public void CreateAll_WithoutADevice_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => EditorTabViewModel.CreateAll(null!, isLightingEnabled: false));
        }

        private static IReadOnlyList<EditorTabViewModel> CreateAll(DeviceId deviceId)
        {
            return EditorTabViewModel.CreateAll(DeviceCatalog.GetById(deviceId), isLightingEnabled: false);
        }
    }
}
