using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    /// <summary>
    /// Full-layer token assertions transcribed independently from
    /// specs/05-key-model.md §4.4 and §3.13 (Gen1 file tokens; the backtick position
    /// is "tilde" per spec 05 §3.2).
    /// </summary>
    public class TkoGeometryTests
    {
        private const string TopTokens =
            @"esc 1 2 3 4 5 6 7 8 9 0 hyph = bspc " +
            @"tab q w e r t y u i o p obrk cbrk \ " +
            @"caps a s d f g h j k l colon apos ent " +
            @"lshft z x c v b n m com per / rshft " +
            @"lctrl lwin lalt lspc mspc rspc ralt fnshf ss rctrl";

        private const string BottomTokens =
            @"tilde F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 del " +
            @"tab lmous play prev next LED ins calc up pause pup home prnt \ " +
            @"caps rmous mute vol- vol+ menu scrlk lft dwn rght pdn end ent " +
            @"lshft z x c v b n m com per / rshft " +
            @"lctrl lwin lalt lspc mspc rspc ralt fnshf ss rctrl";

        private const string EdgeZoneTokens =
            "L1 L2 L3 L4 L5 L6 L7 L8 L9 " +
            "B1 B2 B3 B4 B5 B6 B7 B8 B9 B10 B11 B12 B13 B14 B15 " +
            "R1 R2 R3 R4 R5 R6 R7 R8 R9";

        [Fact]
        public void Tko_TopLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(TopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Tko.Layers[0]));
        }

        [Fact]
        public void Tko_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(BottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Tko.Layers[1]));
        }

        [Fact]
        public void EdgeZones_OnBothLayers_ContainThe33ZonesInSpecOrder()
        {
            foreach (var layer in GeometryCatalog.Tko.Layers)
            {
                Assert.Equal(33, layer.EdgeZones.Count);
                Assert.Equal(EdgeZoneTokens, string.Join(" ", layer.EdgeZones.Select(zone => zone.DefaultToken)));
            }
        }

        [Fact]
        public void Keys_OnBothLayers_Count63KeyPositionsExcludingEdgeZones()
        {
            foreach (var layer in GeometryCatalog.Tko.Layers)
            {
                Assert.Equal(63, layer.Keys.Count);
            }
        }

        [Fact]
        public void SmartSetKey_AtIndex61_IsLockedOnBothLayers()
        {
            foreach (var layer in GeometryCatalog.Tko.Layers)
            {
                var smartSetKey = layer.Keys[61];

                Assert.Equal("ss", smartSetKey.DefaultToken);
                Assert.False(smartSetKey.CanEdit);
                Assert.False(smartSetKey.CanAssignMacro);
            }
        }

        [Fact]
        public void FnShiftKey_AtIndex60_IsRemappableAndMacroCapableOnBothLayers()
        {
            foreach (var layer in GeometryCatalog.Tko.Layers)
            {
                var fnShiftKey = layer.Keys[60];

                Assert.Equal("fnshf", fnShiftKey.DefaultToken);
                Assert.True(fnShiftKey.CanEdit);
                Assert.True(fnShiftKey.CanAssignMacro);
            }
        }

        [Fact]
        public void Layers_OnTko_RestrictMacrosExactlyAtModifierPositions()
        {
            var expectedRestrictedIndices = new[] { 41, 52, 53, 54, 55, 59, 62 };

            foreach (var layer in GeometryCatalog.Tko.Layers)
            {
                var restrictedIndices = layer.Keys
                    .Where(key => !key.CanAssignMacro && key.CanEdit)
                    .Select(key => key.Index)
                    .ToArray();

                Assert.Equal(expectedRestrictedIndices, restrictedIndices);
            }
        }
    }
}
