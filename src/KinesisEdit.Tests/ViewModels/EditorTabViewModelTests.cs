using KinesisEdit.Core.Devices;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The tab strip is capability-driven, and the rule is <b>absence, never disabling</b>: a
    /// section this app cannot open for this board is not rendered at all (docs/design/README.md,
    /// "capability-driven UI: absent features are not shown, not disabled"). Which sections those
    /// are comes from the catalog's <c>LightingCapability</c>/<c>SettingsCapability</c> plus the
    /// lighting panel's own support predicate — never from a device id here.
    /// </summary>
    public sealed class EditorTabViewModelTests
    {
        [Fact]
        public void CreateAll_ForAPerKeyRgbBoardThePanelCanLight_HasAllFourSections()
        {
            var tabs = CreateAll(DeviceId.FreestyleEdgeRgb, isLightingSupported: true);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Lighting, EditorTab.Settings },
                tabs.Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_TheFirstTab_IsCaptionedLayout()
        {
            // The mockups' caption. The EditorTab.Keys enum member deliberately keeps its name: it
            // is carried inside EnumMatch converter-parameter strings in XAML.
            var tabs = CreateAll(DeviceId.FreestyleEdgeRgb, isLightingSupported: true);

            Assert.Equal(EditorTab.Keys, tabs[0].Tab);
            Assert.Equal(EditorTabViewModel.LayoutCaption, tabs[0].Caption);
            Assert.Equal("Layout", tabs[0].Caption);
        }

        [Fact]
        public void CreateAll_ForADeviceWithoutLighting_OmitsTheLightingTab()
        {
            // Freestyle Pro and Advantage2 have no lighting hardware at all (specs/02-devices.md),
            // so there is no lighting file to edit and no tab to open. The support flag cannot
            // conjure one either.
            Assert.DoesNotContain(
                EditorTab.Lighting,
                CreateAll(DeviceId.FreestylePro, isLightingSupported: true).Select(tab => tab.Tab));
            Assert.DoesNotContain(
                EditorTab.Lighting,
                CreateAll(DeviceId.Advantage2, isLightingSupported: true).Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_ForALitBoardThisAppCannotEditYet_OmitsTheLightingTabRatherThanDimmingIt()
        {
            // The TKO's led file adds an edge section (#40) and the Advantage 360's holds six
            // indicators (#41): neither is the model LightingTabViewModel edits. The tab used to be
            // rendered and disabled; the design's law is that it is not rendered.
            foreach (var deviceId in new[] { DeviceId.Tko, DeviceId.Advantage360 })
            {
                var device = DeviceCatalog.GetById(deviceId);

                Assert.NotEqual(LightingKind.None, device.Lighting.Kind);
                Assert.False(LightingTabViewModel.IsSupported(device));
                Assert.DoesNotContain(
                    EditorTab.Lighting,
                    CreateAll(deviceId, isLightingSupported: false).Select(tab => tab.Tab));
            }
        }

        [Fact]
        public void CreateAll_ForADeviceWithoutASettingsFile_OmitsTheSettingsTab()
        {
            Assert.DoesNotContain(
                EditorTab.Settings,
                CreateAll(DeviceId.SavantElite2, isLightingSupported: false).Select(tab => tab.Tab));
            Assert.DoesNotContain(
                EditorTab.Settings,
                CreateAll(DeviceId.CrossfireKeypad, isLightingSupported: false).Select(tab => tab.Tab));
            Assert.DoesNotContain(
                EditorTab.Settings,
                CreateAll(DeviceId.Advantage360Professional, isLightingSupported: false).Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_ForTheAdvantage2_IsLayoutMacrosAndSettings()
        {
            var tabs = CreateAll(DeviceId.Advantage2, isLightingSupported: false);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Settings },
                tabs.Select(tab => tab.Tab));
        }

        [Fact]
        public void CreateAll_TheMacrosTab_IsAlwaysPresent()
        {
            // Unlike Lighting and Settings, the Macros tab is not capability-filtered: the panel
            // behind it reads the device's own MacroCapability and says "This device does not
            // support macros" itself, so the answer lives in one place.
            foreach (var device in DeviceCatalog.All)
            {
                Assert.Single(
                    EditorTabViewModel.CreateAll(device, isLightingSupported: false),
                    tab => tab.Tab == EditorTab.Macros);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CreateAll_TheLightingTab_IsPresentOnlyWhenThePanelSupportsTheBoard(bool isLightingSupported)
        {
            // The one switch the lighting panel flips. It now decides presence, not enablement.
            var tabs = EditorTabViewModel.CreateAll(
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb),
                isLightingSupported);

            Assert.Equal(
                isLightingSupported,
                tabs.Any(tab => tab.Tab == EditorTab.Lighting));
        }

        [Fact]
        public void CreateAll_ForEveryDevice_RendersTheLightingTabExactlyWhereThePanelCanEditTheLedFile()
        {
            // The RGB is the only board whose led file is the two-layer key-backlight model the
            // panel edits, so it is the only board that gets the tab at all.
            foreach (var device in DeviceCatalog.All)
            {
                var isSupported = LightingTabViewModel.IsSupported(device);
                var hasTab = EditorTabViewModel
                    .CreateAll(device, isSupported)
                    .Any(tab => tab.Tab == EditorTab.Lighting);

                Assert.Equal(device.Id == DeviceId.FreestyleEdgeRgb, hasTab);
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
                    .CreateAll(device, isLightingSupported: false)
                    .Any(tab => tab.Tab == EditorTab.Settings);

                Assert.Equal(KeyboardSettingsRows.Create(device.Settings).Count > 0, hasTab);
            }
        }

        [Fact]
        public void CreateAll_ForEveryDevice_YieldsNoSectionTheEditorWouldHaveToRefuse()
        {
            // The whole point of dropping IsEnabled: every entry the strip carries opens.
            foreach (var device in DeviceCatalog.All)
            {
                var tabs = EditorTabViewModel.CreateAll(device, LightingTabViewModel.IsSupported(device));

                Assert.All(tabs, tab => Assert.False(string.IsNullOrWhiteSpace(tab.Caption)));
                Assert.Equal(tabs.Select(tab => tab.Tab).Distinct().Count(), tabs.Count);
            }
        }

        [Fact]
        public void CreateAll_WithoutADevice_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => EditorTabViewModel.CreateAll(null!, isLightingSupported: false));
        }

        private static IReadOnlyList<EditorTabViewModel> CreateAll(DeviceId deviceId, bool isLightingSupported)
        {
            return EditorTabViewModel.CreateAll(DeviceCatalog.GetById(deviceId), isLightingSupported);
        }
    }
}
