using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    /// <summary>
    /// Assertions transcribed independently from specs/05-key-model.md §4.7 (Gen2 file
    /// tokens), including the position-token divergences that only exist on the
    /// Advantage360 (spec 05 §1.3, §7.3).
    /// </summary>
    public class Advantage360GeometryTests
    {
        private const string BaseTokens =
            "eql 1 2 3 4 5 keyt ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "fn1s grav caps left rght up down obrk cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn null";

        private const string KeypadTokens =
            "eql 1 2 3 4 5 deft ss 6 nmlk kp= kp/ kp* hyph " +
            "tab q w e r t hk1 hk3 y kp7 kp8 kp9 kp- bsls " +
            "esc a s d f g hk2 hk4 h kp4 kp5 kp6 kp+ apos " +
            "lshf z x c v b n kp1 kp2 kp3 kpen rshf " +
            "fn1s grav caps left rght up down kp. cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent kp0 end pgdn null";

        private const string Fn1Tokens =
            "F1 F2 F3 F4 F5 F6 keyt ss F7 F8 F9 F10 F11 F12 " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "defs grav caps left rght up down obrk cbrk defs " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn null";

        private static LayerGeometry BaseLayer => GeometryCatalog.Advantage360.Layers[0];

        private static LayerGeometry KeypadLayer => GeometryCatalog.Advantage360.Layers[1];

        private static LayerGeometry Fn1Layer => GeometryCatalog.Advantage360.Layers[2];

        [Fact]
        public void BaseLayer_DefaultTokens_MatchSpecTokenSequence()
        {
            Assert.Equal(BaseTokens, GeometryTokens.RenderDefaults(BaseLayer));
        }

        [Fact]
        public void KeypadLayer_DefaultTokens_MatchSpecTokenSequence()
        {
            Assert.Equal(KeypadTokens, GeometryTokens.RenderDefaults(KeypadLayer));
        }

        [Fact]
        public void Fn1Layer_DefaultTokens_MatchSpecTokenSequence()
        {
            Assert.Equal(Fn1Tokens, GeometryTokens.RenderDefaults(Fn1Layer));
        }

        [Theory]
        [InlineData(6, "keyt", "kp")]
        [InlineData(54, "fn1s", "lfn")]
        [InlineData(63, "fn1s", "rfn")]
        [InlineData(76, "null", "pedl")]
        public void BaseLayer_AtDivergentIndices_UsesDistinctPositionTokens(int index, string defaultToken, string positionToken)
        {
            var key = BaseLayer.Keys[index];

            Assert.Equal(defaultToken, key.DefaultToken);
            Assert.Equal(positionToken, key.PositionToken);
        }

        [Fact]
        public void BaseLayer_OutsideDivergentIndices_UsesNoPositionTokens()
        {
            var divergentIndices = BaseLayer.Keys
                .Where(key => key.PositionToken is not null)
                .Select(key => key.Index)
                .ToArray();

            Assert.Equal(new[] { 6, 54, 63, 76 }, divergentIndices);
        }

        [Theory]
        [InlineData(6, "deft", "kp")]
        [InlineData(9, "nmlk", "7")]
        [InlineData(10, "kp=", "8")]
        [InlineData(11, "kp/", "9")]
        [InlineData(12, "kp*", "0")]
        [InlineData(23, "kp7", "u")]
        [InlineData(24, "kp8", "i")]
        [InlineData(25, "kp9", "o")]
        [InlineData(26, "kp-", "p")]
        [InlineData(37, "kp4", "j")]
        [InlineData(38, "kp5", "k")]
        [InlineData(39, "kp6", "l")]
        [InlineData(40, "kp+", "scol")]
        [InlineData(49, "kp1", "m")]
        [InlineData(50, "kp2", "comm")]
        [InlineData(51, "kp3", "perd")]
        [InlineData(52, "kpen", "fsls")]
        [InlineData(61, "kp.", "obrk")]
        [InlineData(73, "kp0", "spc")]
        [InlineData(54, "fn1s", "lfn")]
        [InlineData(63, "fn1s", "rfn")]
        [InlineData(76, "null", "pedl")]
        public void KeypadLayer_AtDivergentIndices_UsesDistinctPositionTokens(int index, string defaultToken, string positionToken)
        {
            var key = KeypadLayer.Keys[index];

            Assert.Equal(defaultToken, key.DefaultToken);
            Assert.Equal(positionToken, key.PositionToken);
        }

        [Fact]
        public void KeypadLayer_OutsideDivergentIndices_UsesNoPositionTokens()
        {
            var divergentIndices = KeypadLayer.Keys
                .Where(key => key.PositionToken is not null)
                .Select(key => key.Index)
                .ToArray();

            var expectedIndices = new[]
            {
                6, 9, 10, 11, 12, 23, 24, 25, 26, 37, 38, 39, 40, 49, 50, 51, 52, 54, 61, 63, 73, 76
            };

            Assert.Equal(expectedIndices, divergentIndices);
        }

        [Theory]
        [InlineData(0, "F1", "eql")]
        [InlineData(1, "F2", "1")]
        [InlineData(2, "F3", "2")]
        [InlineData(3, "F4", "3")]
        [InlineData(4, "F5", "4")]
        [InlineData(5, "F6", "5")]
        [InlineData(8, "F7", "6")]
        [InlineData(9, "F8", "7")]
        [InlineData(10, "F9", "8")]
        [InlineData(11, "F10", "9")]
        [InlineData(12, "F11", "0")]
        [InlineData(13, "F12", "hyph")]
        [InlineData(54, "defs", "lfn")]
        [InlineData(63, "defs", "rfn")]
        public void Fn1Layer_AtDivergentIndices_UsesDistinctPositionTokens(int index, string defaultToken, string positionToken)
        {
            var key = Fn1Layer.Keys[index];

            Assert.Equal(defaultToken, key.DefaultToken);
            Assert.Equal(positionToken, key.PositionToken);
        }

        [Fact]
        public void Fn1Layer_OutsideOverriddenIndices_MatchesBaseLayer()
        {
            var overriddenIndices = new HashSet<int> { 0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 54, 63 };

            for (var i = 0; i < BaseLayer.Keys.Count; i++)
            {
                if (overriddenIndices.Contains(i))
                {
                    continue;
                }

                Assert.Equal(BaseLayer.Keys[i], Fn1Layer.Keys[i]);
            }
        }

        [Theory]
        [InlineData(3, "Fn2")]
        [InlineData(4, "Fn3")]
        public void Fn2AndFn3Layers_ExceptForNameAndIndex_AreIdenticalToFn1(int layerIndex, string expectedName)
        {
            var layer = GeometryCatalog.Advantage360.Layers[layerIndex];

            Assert.Equal(expectedName, layer.Name);
            Assert.Equal(layerIndex, layer.Index);
            Assert.Equal(Fn1Layer.Keys, layer.Keys);
        }

        [Fact]
        public void SmartSetKey_AtIndex7_IsTheOnlyLockedPositionOnEveryLayer()
        {
            foreach (var layer in GeometryCatalog.Advantage360.Layers)
            {
                var lockedIndices = layer.Keys
                    .Where(key => !key.CanEdit)
                    .Select(key => key.Index)
                    .ToArray();

                Assert.Equal(new[] { 7 }, lockedIndices);
                Assert.Equal("ss", layer.Keys[7].DefaultToken);
            }
        }

        [Fact]
        public void ModifierPositions_OnEveryLayer_AreRemappableAndMacroCapable()
        {
            var modifierIndices = new[] { 42, 53, 64, 65, 66, 67 };

            foreach (var layer in GeometryCatalog.Advantage360.Layers)
            {
                foreach (var index in modifierIndices)
                {
                    Assert.True(layer.Keys[index].CanEdit);
                    Assert.True(layer.Keys[index].CanAssignMacro);
                }
            }
        }

        [Fact]
        public void MacroRestrictions_OnEveryLayer_ApplyOnlyToTheLockedSmartSetKey()
        {
            foreach (var layer in GeometryCatalog.Advantage360.Layers)
            {
                var restrictedIndices = layer.Keys
                    .Where(key => !key.CanAssignMacro)
                    .Select(key => key.Index)
                    .ToArray();

                Assert.Equal(new[] { 7 }, restrictedIndices);
            }
        }
    }
}
