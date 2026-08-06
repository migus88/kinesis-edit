using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The duplicate-key advisory: a token on two positions of a layer is reported only when a
    /// user remap created or joined the duplication, so a stock board stays silent.
    /// </summary>
    public class DuplicateKeyScanTests
    {
        [Fact]
        public void Find_WhenARemapDuplicatesAnotherPosition_ReportsBothAnchors()
        {
            var layer = Layer("a", "b", "c");
            layer.Keys[2].Remap(ModelTokens.Key("a"));

            var findings = DuplicateKeyScan.Find(layer, TokenDialect.Gen1);

            var finding = Assert.Single(findings);
            Assert.Equal("a", finding.Token);
            Assert.Equal(0, finding.LayerIndex);
            Assert.Equal(new[] { 0, 2 }, finding.KeyIndexes);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Tko)]
        public void Find_ForAFactoryLayout_ReportsNothing(DeviceId deviceId)
        {
            // The trap: scanning every position's factory action would put advisories on a board
            // the user has not touched, which is the noise that gets an advisory system ignored.
            var layout = KeyboardLayout.Create(deviceId);

            Assert.Empty(DuplicateKeyScan.Find(layout));

            foreach (var layer in layout.Layers)
            {
                Assert.Empty(DuplicateKeyScan.Find(layer, layout.Dialect));
            }
        }

        [Fact]
        public void Find_ForAFactoryLayerThatRepeatsATokenByDesign_StaysSilentUntilAUserJoinsIt()
        {
            // The Advantage360's Fn layers really do put "defs" on both thumb Fn positions
            // (05 §4.7), so the silence above is the user-assignment gate at work and not the
            // authored boards happening to be duplicate-free.
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var fn1 = layout.Layers[2];
            var repeated = fn1.Keys
                .GroupBy(key => key.ModifiedOrOriginalKey.GetToken(layout.Dialect))
                .First(group => group.Key.Length > 0 && group.Count() > 1);

            Assert.Empty(DuplicateKeyScan.Find(fn1, layout.Dialect));

            var joining = fn1.Keys.First(key => key.CanEdit && !repeated.Contains(key));
            Assert.True(joining.Remap(repeated.First().OriginalKey));

            var finding = Assert.Single(DuplicateKeyScan.Find(fn1, layout.Dialect));

            Assert.Equal(repeated.Key, finding.Token);
            Assert.Equal(
                repeated.Select(key => key.Index).Append(joining.Index).Order(),
                finding.KeyIndexes);
        }

        [Fact]
        public void Find_ForAFactoryDuplicateNobodyEdited_ReportsNothing()
        {
            // Same rule stated without relying on the authored board being duplicate-free.
            var layer = Layer("a", "b", "a");

            Assert.Empty(DuplicateKeyScan.Find(layer, TokenDialect.Gen1));
        }

        [Fact]
        public void Find_WhenARemapJoinsAFactoryDuplicate_ReportsEveryPositionCarryingTheToken()
        {
            var layer = Layer("a", "b", "a", "c");
            layer.Keys[3].Remap(ModelTokens.Key("a"));

            var finding = Assert.Single(DuplicateKeyScan.Find(layer, TokenDialect.Gen1));

            Assert.Equal("a", finding.Token);
            Assert.Equal(new[] { 0, 2, 3 }, finding.KeyIndexes);
        }

        [Fact]
        public void Find_WhenThreePositionsCarryOneToken_ReportsOneFindingWithThreeIndexes()
        {
            var layer = Layer("a", "b", "c", "d");
            layer.Keys[1].Remap(ModelTokens.Key("a"));
            layer.Keys[3].Remap(ModelTokens.Key("a"));

            var finding = Assert.Single(DuplicateKeyScan.Find(layer, TokenDialect.Gen1));

            Assert.Equal("a", finding.Token);
            Assert.Equal(new[] { 0, 1, 3 }, finding.KeyIndexes);
        }

        [Fact]
        public void Find_WithSeveralDuplications_OrdersThemByFirstKeyIndex()
        {
            var layer = Layer("a", "b", "c", "d", "e");
            layer.Keys[4].Remap(ModelTokens.Key("b"));
            layer.Keys[3].Remap(ModelTokens.Key("a"));

            var findings = DuplicateKeyScan.Find(layer, TokenDialect.Gen1);

            Assert.Equal(new[] { "a", "b" }, findings.Select(finding => finding.Token));
            Assert.Equal(new[] { 0, 3 }, findings[0].KeyIndexes);
            Assert.Equal(new[] { 1, 4 }, findings[1].KeyIndexes);
        }

        [Fact]
        public void Find_WhenARemapReturnsAKeyToItsFactoryAction_ReportsNothing()
        {
            var layer = Layer("a", "b", "c");
            layer.Keys[2].Remap(ModelTokens.Key("a"));
            layer.Keys[2].Remap(ModelTokens.Key("c"));

            Assert.Empty(DuplicateKeyScan.Find(layer, TokenDialect.Gen1));
        }

        [Fact]
        public void Find_ForALayout_OrdersFindingsByLayerThenFirstKeyIndex()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            var topSource = FindByToken(layout.Layers[0], "q");
            var topTarget = FindByToken(layout.Layers[0], "a");
            var bottomSource = FindByToken(layout.Layers[1], "w");
            var bottomTarget = FindByToken(layout.Layers[1], "s");

            Assert.True(topSource.Remap(topTarget.OriginalKey));
            Assert.True(bottomSource.Remap(bottomTarget.OriginalKey));

            var findings = DuplicateKeyScan.Find(layout);

            Assert.Equal(2, findings.Count);
            Assert.Equal(0, findings[0].LayerIndex);
            Assert.Equal("a", findings[0].Token);
            Assert.Equal(Ascending(topSource.Index, topTarget.Index), findings[0].KeyIndexes);
            Assert.Equal(1, findings[1].LayerIndex);
            Assert.Equal("s", findings[1].Token);
            Assert.Equal(Ascending(bottomSource.Index, bottomTarget.Index), findings[1].KeyIndexes);
        }

        [Fact]
        public void Find_WithoutALayerOrLayout_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => DuplicateKeyScan.Find((KeyboardLayer)null!));
            Assert.Throws<ArgumentNullException>(() => DuplicateKeyScan.Find((KeyboardLayout)null!));
        }

        private static KeyboardLayer Layer(params string[] tokens)
        {
            var keys = tokens
                .Select((token, index) => ModelTokens.CreateKey(token, index: index))
                .ToArray();

            return new KeyboardLayer("Scan", 0, 0, keys);
        }

        private static KeyboardKey FindByToken(KeyboardLayer layer, string token)
        {
            return layer.Keys.First(key => key.OriginalKey.GetToken(TokenDialect.Gen1) == token);
        }

        private static int[] Ascending(int first, int second)
        {
            return first < second ? [first, second] : [second, first];
        }
    }
}
