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
        public void CreateAll_ForAPerKeyRgbBoardThePanelCanLight_HasAllThreeSections()
        {
            // Three since issue #140 deleted the Macros tab: the rail's Macro panel is the app's
            // one macro editor, so there is no second surface to open.
            var tabs = CreateAll(DeviceId.FreestyleEdgeRgb, isLightingSupported: true);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Lighting, EditorTab.Settings },
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
        public void CreateAll_ForTheAdvantage2_IsLayoutAndSettings()
        {
            var tabs = CreateAll(DeviceId.Advantage2, isLightingSupported: false);

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Settings },
                tabs.Select(tab => tab.Tab));
        }

        [Fact]
        public void EditorTab_CarriesNoMacrosMember_AndTheThreeThatSurviveKeepTheirNumbers()
        {
            // Issue #140 removed the member rather than leaving it declared-but-unused, and that is
            // load-bearing: two suites walk Enum.GetValues<EditorTab>() and assign SelectedTab, and
            // SelectTab refuses a tab the strip does not carry — so a stale member would make both
            // loops silently re-assert the previous tab, green and vacuous. The hole at 2 is left
            // alone: the three survivors keep the numbers they always had.
            Assert.DoesNotContain("Macros", Enum.GetNames<EditorTab>());
            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Lighting, EditorTab.Settings },
                Enum.GetValues<EditorTab>());
            Assert.Equal(1, (int)EditorTab.Keys);
            Assert.Equal(3, (int)EditorTab.Lighting);
            Assert.Equal(4, (int)EditorTab.Settings);
        }

        [Fact]
        public void CreateAll_ForEveryDevice_CarriesNoMacrosSectionAnyMore()
        {
            // The macro-less-device answer used to be the Macros tab's ("This device does not
            // support macros"); it lives in the rail's Macro panel now, which gates on the device's
            // own MacroCapability. No board gets a macro *section* at all.
            foreach (var device in DeviceCatalog.All)
            {
                var tabs = EditorTabViewModel.CreateAll(device, LightingTabViewModel.IsSupported(device));

                Assert.All(
                    tabs,
                    tab => Assert.Contains(
                        tab.Tab,
                        new[] { EditorTab.Keys, EditorTab.Lighting, EditorTab.Settings }));
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
