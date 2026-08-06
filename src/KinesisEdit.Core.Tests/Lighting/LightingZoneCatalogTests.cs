using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Pins the Freestyle Edge RGB color zones of specs/07-lighting.md §4: the eight zone-button
    /// names, their membership as resolved against the RGB top-layer geometry, and the invariant
    /// that zone key codes are the same Gen1 codes <see cref="LayerLightingState.KeyColors"/>
    /// uses — so a zone can be painted with <see cref="LayerLightingState.SetKeyColor"/>.
    /// </summary>
    public class LightingZoneCatalogTests
    {
        [Fact]
        public void Find_ForTheRgb_ListsTheEightZonesOfSpec4InOrder()
        {
            var zoneNames = LightingZoneCatalog.Find(DeviceId.FreestyleEdgeRgb)
                .Select(zone => zone.DisplayName)
                .ToList();

            Assert.Equal(
                new[] { "All", "Number", "Function", "WASD", "Game", "Arrow", "Left Module", "Right Module" },
                zoneNames);
        }

        [Fact]
        public void All_Always_ContainsOnlyRgbRowsWhileTheTkoZonesAreDeferred()
        {
            Assert.Equal(8, LightingZoneCatalog.All.Count);
            Assert.All(LightingZoneCatalog.All, zone => Assert.Equal(DeviceId.FreestyleEdgeRgb, zone.Device));
        }

        [Theory]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.None)]
        public void Find_ForADeviceWithoutAuthoredZones_ReturnsAnEmptyList(DeviceId deviceId)
        {
            Assert.Empty(LightingZoneCatalog.Find(deviceId));
        }

        [Fact]
        public void Find_ForTheAllZone_CoversEveryAddressableKeyOfTheRgbTopLayer()
        {
            var expectedCodes = GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys
                .Select(position => KeyRegistry.FindByToken(position.DefaultToken, TokenDialect.Gen1)!.Code)
                .ToList();

            Assert.Equal(95, expectedCodes.Count);
            Assert.Equal(expectedCodes, GetZone("All").KeyCodes);
        }

        [Theory]
        [InlineData("Number", new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" })]
        [InlineData("Function", new[] { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12" })]
        [InlineData("WASD", new[] { "w", "a", "s", "d" })]
        [InlineData("Arrow", new[] { "up", "lft", "dwn", "rght" })]
        public void Find_PerZone_CarriesExactlyTheExpectedKeyTokens(string zoneName, string[] expectedTokens)
        {
            Assert.Equal(ToKeyCodes(expectedTokens), GetZone(zoneName).KeyCodes);
        }

        [Fact]
        public void Find_ForTheGameZone_IsTheLeftModuleTypingKeysWithoutTheFunctionRowAndWindowsKey()
        {
            var expectedTokens = new[]
            {
                "esc",
                "tilde", "1", "2", "3", "4", "5", "6",
                "tab", "q", "w", "e", "r", "t",
                "caps", "a", "s", "d", "f", "g",
                "lshft", "z", "x", "c", "v", "b",
                "lctrl", "lalt", "lspc"
            };

            Assert.Equal(ToKeyCodes(expectedTokens), GetZone("Game").KeyCodes);
            Assert.DoesNotContain(ToKeyCode("lwin"), GetZone("Game").KeyCodes);
            Assert.DoesNotContain(ToKeyCode("F1"), GetZone("Game").KeyCodes);
        }

        [Fact]
        public void Find_ForTheModuleZones_SplitTheBoardExactlyInTwo()
        {
            var leftModule = GetZone("Left Module").KeyCodes;
            var rightModule = GetZone("Right Module").KeyCodes;

            // 47 keys on the left half (including the hk0-hk10 hotkey column that sits on it)
            // and 48 on the right, together the whole 95-position board.
            Assert.Equal(47, leftModule.Count);
            Assert.Equal(48, rightModule.Count);
            Assert.Empty(leftModule.Intersect(rightModule));
            Assert.Equal(
                GetZone("All").KeyCodes.OrderBy(code => code),
                leftModule.Concat(rightModule).OrderBy(code => code));
        }

        [Fact]
        public void Find_ForTheLeftModuleZone_ContainsTheHotkeyColumnAndTheLeftHalfKeys()
        {
            var leftModule = GetZone("Left Module").KeyCodes;

            Assert.Contains(ToKeyCode("hk0"), leftModule);
            Assert.Contains(ToKeyCode("hk10"), leftModule);
            Assert.Contains(ToKeyCode("6"), leftModule);
            Assert.Contains(ToKeyCode("lspc"), leftModule);
            Assert.DoesNotContain(ToKeyCode("7"), leftModule);
            Assert.DoesNotContain(ToKeyCode("rspc"), leftModule);
        }

        [Fact]
        public void Find_ForTheRightModuleZone_ContainsTheNavigationAndArrowKeys()
        {
            var rightModule = GetZone("Right Module").KeyCodes;

            Assert.Contains(ToKeyCode("home"), rightModule);
            Assert.Contains(ToKeyCode("pdn"), rightModule);
            Assert.Contains(ToKeyCode("rght"), rightModule);
            Assert.DoesNotContain(ToKeyCode("hk1"), rightModule);
        }

        [Fact]
        public void Find_PerZone_UsesTheKeyCodesOfLayerLightingStateKeyColors()
        {
            var state = new LayerLightingState();
            var zone = GetZone("WASD");

            foreach (var keyCode in zone.KeyCodes)
            {
                state.SetKeyColor(keyCode, new LedColor(1, 2, 3));
            }

            Assert.Equal(zone.KeyCodes.Count, state.KeyColors.Count);
            Assert.All(zone.KeyCodes, keyCode => Assert.Equal(new LedColor(1, 2, 3), state.KeyColors[keyCode]));
        }

        [Theory]
        [InlineData("wasd")]
        [InlineData("LEFT MODULE")]
        public void FindByName_WithAKnownName_ResolvesCaseInsensitively(string displayName)
        {
            Assert.NotNull(LightingZoneCatalog.FindByName(DeviceId.FreestyleEdgeRgb, displayName));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "Hyperspace")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "")]
        [InlineData(DeviceId.FreestyleEdgeRgb, null)]
        [InlineData(DeviceId.Tko, "All")]
        public void FindByName_WithAnUnknownNameOrDevice_ReturnsNull(DeviceId deviceId, string? displayName)
        {
            Assert.Null(LightingZoneCatalog.FindByName(deviceId, displayName));
        }

        private static LightingZoneDefinition GetZone(string displayName)
        {
            return LightingZoneCatalog.FindByName(DeviceId.FreestyleEdgeRgb, displayName)!;
        }

        private static IReadOnlyList<int> ToKeyCodes(IEnumerable<string> tokens)
        {
            return tokens.Select(ToKeyCode).ToList();
        }

        private static int ToKeyCode(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)!.Code;
        }
    }
}
