using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Integration
{
    /// <summary>
    /// Cross-module fidelity check between the lighting engine and the key-token registry
    /// (specs/07-lighting.md §4: per-key tokens are identical to layout files): every config
    /// token the serializer emits for keys and edge LEDs must resolve through
    /// <see cref="KeyRegistry.FindByToken(string?, TokenDialect)"/> in the Gen1 dialect, so a
    /// written led file always reads back. Mirrors <see cref="GeometryTokenIntegrationTests"/>.
    /// </summary>
    public class LightingTokenIntegrationTests
    {
        [Fact]
        public void SerializeRgb_ColoringEveryAddressableKeyOnBothLayers_EmitsOnlyResolvableGen1Tokens()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Freestyle;
            model.FnLayer.Mode = LightingMode.Freestyle;

            ColorEveryKey(model, GeometryCatalog.FreestyleEdgeRgb, appliesFnTranslation: true);

            AssertAllConfigTokensResolve(LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeTko_ColoringEveryKeyAndEdgeLedOnBothLayers_EmitsOnlyResolvableGen1Tokens()
        {
            var model = new TkoLightingModel();

            model.KeyBacklight.TopLayer.Mode = LightingMode.Freestyle;
            model.KeyBacklight.FnLayer.Mode = LightingMode.Freestyle;
            model.Edge.TopLayer.Mode = LightingMode.Freestyle;
            model.Edge.FnLayer.Mode = LightingMode.Freestyle;

            // Spec 05 §5.5 scopes the Fn token exceptions to the FS family — not the TKO.
            ColorEveryKey(model.KeyBacklight, GeometryCatalog.Tko, appliesFnTranslation: false);

            foreach (var zone in GeometryCatalog.Tko.Layers[0].EdgeZones)
            {
                var code = KeyRegistry.FindByToken(zone.DefaultToken, TokenDialect.Gen1)!.Code;

                model.Edge.TopLayer.SetKeyColor(code, new LedColor(1, 2, 3));
                model.Edge.FnLayer.SetKeyColor(code, new LedColor(4, 5, 6));
            }

            AssertAllConfigTokensResolve(LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void SerializeTko_WithEveryEdgeLedColored_EmitsTokensFromTheEdgeZonesTable()
        {
            var model = new TkoLightingModel();

            model.Edge.TopLayer.Mode = LightingMode.Freestyle;

            foreach (var zone in GeometryCatalog.Tko.Layers[0].EdgeZones)
            {
                var code = KeyRegistry.FindByToken(zone.DefaultToken, TokenDialect.Gen1)!.Code;

                model.Edge.TopLayer.SetKeyColor(code, new LedColor(1, 2, 3));
            }

            var lines = LedFileSerializer.SerializeTko(model);

            Assert.Equal(33, lines.Count);

            foreach (var line in lines)
            {
                var entry = KeyRegistry.FindByToken(ExtractConfigToken(line), TokenDialect.Gen1);

                Assert.NotNull(entry);
                Assert.Equal(KeyTable.EdgeZones, entry.Table);
            }
        }

        [Fact]
        public void FnLayerTokenTranslator_ForEveryPair_MapsBetweenCodesWithGen1TokensOnBothSides()
        {
            foreach (var pair in FnLayerTokenTranslator.Pairs)
            {
                var memoryEntry = KeyRegistry.FindByCode(pair.MemoryKeyCode);
                var fileEntry = KeyRegistry.FindByCode(pair.FileKeyCode);

                Assert.NotNull(memoryEntry);
                Assert.NotNull(fileEntry);
                Assert.True(memoryEntry.HasToken(TokenDialect.Gen1));
                Assert.True(fileEntry.HasToken(TokenDialect.Gen1));
            }
        }

        private static void ColorEveryKey(LightingModel model, DeviceGeometry geometry, bool appliesFnTranslation)
        {
            foreach (var position in geometry.Layers[0].Keys)
            {
                var entry = KeyRegistry.FindByToken(position.DefaultToken, TokenDialect.Gen1);

                Assert.NotNull(entry);

                model.TopLayer.SetKeyColor(entry.Code, new LedColor(10, 20, 30));

                // Fn-layer state is keyed by the in-memory key; the §2.4 item 7 exceptions
                // apply on the RGB only (05 §5.5).
                var fnLayerCode = appliesFnTranslation
                    ? FnLayerTokenTranslator.TranslateForLoad(entry.Code)
                    : entry.Code;

                model.FnLayer.SetKeyColor(fnLayerCode, new LedColor(40, 50, 60));
            }
        }

        private static void AssertAllConfigTokensResolve(IReadOnlyList<string> lines)
        {
            Assert.NotEmpty(lines);

            foreach (var line in lines)
            {
                var token = ExtractConfigToken(line);

                Assert.NotNull(KeyRegistry.FindByToken(token, TokenDialect.Gen1));
            }
        }

        private static string ExtractConfigToken(string line)
        {
            var withoutPrefix = line.StartsWith("fn ", StringComparison.Ordinal) ? line[3..] : line;
            var closeIndex = withoutPrefix.IndexOf(']');

            Assert.StartsWith("[", withoutPrefix);
            Assert.True(closeIndex > 1);

            return withoutPrefix[1..closeIndex];
        }
    }
}
