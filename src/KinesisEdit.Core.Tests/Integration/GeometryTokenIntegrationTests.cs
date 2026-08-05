using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Integration
{
    /// <summary>
    /// Cross-module fidelity check between the hand-entered geometry data (spec 05 §4
    /// layer listings) and the key-token registry (spec 05 §3 tables): every token a
    /// geometry carries must resolve through
    /// <see cref="KeyRegistry.FindByToken(string?, TokenDialect)"/> scoped to the device
    /// family's dialect — Gen1 for FS Edge/RGB/Pro/TKO, Legacy for Advantage2, Gen2 for
    /// Advantage360 (spec 05 §3 legend). A token that fails here is either a geometry
    /// typo or a missing registry entry.
    /// </summary>
    public class GeometryTokenIntegrationTests
    {
        public static IEnumerable<object[]> AllGeometryNames()
        {
            foreach (var name in CatalogByName.Keys)
            {
                yield return new object[] { name };
            }
        }

        private static IReadOnlyDictionary<string, GeometryUnderTest> CatalogByName { get; } =
            new Dictionary<string, GeometryUnderTest>
            {
                [nameof(GeometryCatalog.FreestyleEdge)] = new(GeometryCatalog.FreestyleEdge, TokenDialect.Gen1),
                [nameof(GeometryCatalog.FreestyleEdgeRgb)] = new(GeometryCatalog.FreestyleEdgeRgb, TokenDialect.Gen1),
                [nameof(GeometryCatalog.FreestylePro)] = new(GeometryCatalog.FreestylePro, TokenDialect.Gen1),
                [nameof(GeometryCatalog.Tko)] = new(GeometryCatalog.Tko, TokenDialect.Gen1),
                [nameof(GeometryCatalog.Advantage2Qwerty)] = new(GeometryCatalog.Advantage2Qwerty, TokenDialect.Legacy),
                [nameof(GeometryCatalog.Advantage2Dvorak)] = new(GeometryCatalog.Advantage2Dvorak, TokenDialect.Legacy),
                [nameof(GeometryCatalog.Advantage360)] = new(GeometryCatalog.Advantage360, TokenDialect.Gen2)
            };

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void DefaultTokens_AcrossEveryLayerAndPosition_ResolveInTheDeviceDialect(string geometryName)
        {
            var (geometry, dialect) = CatalogByName[geometryName];

            var unresolved = CollectUnresolvedTokens(
                geometry,
                dialect,
                layer => layer.Keys,
                key => key.DefaultToken);

            Assert.Empty(unresolved);
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void DefaultTokens_WhenEmpty_AreOnlyTheAdvantage2KeypadAndProgramButtons(string geometryName)
        {
            var (geometry, dialect) = CatalogByName[geometryName];

            // Spec 05 §3.9/§3.10: the Advantage2 Keypad (index 16) and Program (index 17)
            // buttons have empty tokens that are never written to files. No other position
            // on any device may have an empty default token, so the resolution test's
            // skip-empty rule cannot hide a blank geometry entry.
            var expectedEmptyIndices = dialect == TokenDialect.Legacy ? new[] { 16, 17 } : Array.Empty<int>();

            foreach (var layer in geometry.Layers)
            {
                var emptyIndices = layer.Keys
                    .Where(key => key.DefaultToken.Length == 0)
                    .Select(key => key.Index);

                Assert.Equal(expectedEmptyIndices, emptyIndices);
            }
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void MasterAppDefaultTokens_WhereDefined_ResolveInTheDeviceDialect(string geometryName)
        {
            var (geometry, dialect) = CatalogByName[geometryName];

            var unresolved = CollectUnresolvedTokens(
                geometry,
                dialect,
                layer => layer.Keys,
                key => key.MasterAppDefaultToken);

            Assert.Empty(unresolved);
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void PositionTokens_WhereDefined_ResolveInTheDeviceDialect(string geometryName)
        {
            var (geometry, dialect) = CatalogByName[geometryName];

            // Spec 05 §1.3 models PositionKey as a key-table entry, and every distinct
            // Advantage360 position token in §4.7 is a §3 entry in the Gen2 dialect:
            // "kp" (§3.9 code 10042), "lfn"/"rfn" (§3.10), "pedl" (§3.9 code 11207),
            // and the letter/digit/punctuation position names of the Keypad/Fn layers.
            var unresolved = CollectUnresolvedTokens(
                geometry,
                dialect,
                layer => layer.Keys,
                key => key.PositionToken);

            Assert.Empty(unresolved);
        }

        [Fact]
        public void EdgeZoneTokens_OnBothTkoLayers_ResolveToGen1EdgeZoneEntries()
        {
            var geometry = GeometryCatalog.Tko;

            foreach (var layer in geometry.Layers)
            {
                Assert.NotEmpty(layer.EdgeZones);

                foreach (var zone in layer.EdgeZones)
                {
                    var entry = KeyRegistry.FindByToken(zone.DefaultToken, TokenDialect.Gen1);

                    Assert.NotNull(entry);
                    Assert.Equal(KeyTable.EdgeZones, entry.Table);
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllGeometryNames))]
        public void EdgeZoneTokens_AcrossEveryGeometry_ResolveInTheDeviceDialect(string geometryName)
        {
            var (geometry, dialect) = CatalogByName[geometryName];

            var unresolved = CollectUnresolvedTokens(
                geometry,
                dialect,
                layer => layer.EdgeZones,
                zone => zone.DefaultToken);

            Assert.Empty(unresolved);
        }

        private static List<string> CollectUnresolvedTokens(
            DeviceGeometry geometry,
            TokenDialect dialect,
            Func<LayerGeometry, IReadOnlyList<KeyPosition>> positionsSelector,
            Func<KeyPosition, string?> tokenSelector)
        {
            var unresolved = new List<string>();

            foreach (var layer in geometry.Layers)
            {
                foreach (var position in positionsSelector(layer))
                {
                    var token = tokenSelector(position);

                    if (string.IsNullOrEmpty(token))
                    {
                        continue;
                    }

                    if (KeyRegistry.FindByToken(token, dialect) is null)
                    {
                        unresolved.Add($"{layer.Name}[{position.Index}] \"{token}\" ({dialect})");
                    }
                }
            }

            return unresolved;
        }

        private sealed record GeometryUnderTest(DeviceGeometry Geometry, TokenDialect Dialect);
    }
}
