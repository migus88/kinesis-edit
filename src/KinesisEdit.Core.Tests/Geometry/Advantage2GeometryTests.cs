using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    /// <summary>
    /// Full-layer token assertions transcribed independently from
    /// specs/05-key-model.md §4.5/§4.6. Tokens are Legacy-dialect file tokens
    /// (spec 05 §3): the keypad operators are "kpdiv"/"kpmult"/"kpmin"/"kpplus" and
    /// the locked Keypad/Program buttons (16/17) have empty tokens, rendered "(none)".
    /// </summary>
    public class Advantage2GeometryTests
    {
        private const string QwertyTopTokens =
            @"escape F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 prtscr scroll pause (none) (none) " +
            @"= 1 2 3 4 5 6 7 8 9 0 hyphen " +
            @"tab q w e r t y u i o p \ " +
            @"caps a s d f g lctrl lalt rwin rctrl h j k l ; ' " +
            @"lshift z x c v b bspace delete home pup enter space n m , . / rshift " +
            @"` intl-\ left right end pdown up down obrack cbrack " +
            @"lp-tab mp-kpshf rp-kpent";

        private const string QwertyBottomTokens =
            @"escape lwin ralt menu play prev next calc kpshft F9 F10 F11 F12 mute vol- vol+ (none) (none) " +
            @"= 1 2 3 4 5 6 numlk kp= kpdiv kpmult hyphen " +
            @"tab q w e r t y kp7 kp8 kp9 kpmin \ " +
            @"caps a s d f g lctrl lalt rwin rctrl h kp4 kp5 kp6 kpplus ' " +
            @"lshift z x c v b bspace delete home pup enter kp0 n kp1 kp2 kp3 kpenter1 rshift " +
            @"` insert left right end pdown up down kp. kpenter2 " +
            @"lp-tab mp-kpshf rp-kpent";

        private const string DvorakTopTokens =
            @"escape F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 prtscr scroll pause (none) (none) " +
            @"= 1 2 3 4 5 6 7 8 9 0 hyphen " +
            @"tab ' , . p y f g c r l / " +
            @"caps a o e u i lctrl lalt rwin rctrl d h t n s \ " +
            @"lshift ; q j k x bspace delete home pup enter space b m w v z rshift " +
            @"` intl-\ left right end pdown up down obrack cbrack " +
            @"lp-tab mp-kpshf rp-kpent";

        private const string DvorakBottomTokens =
            @"escape lwin ralt menu play prev next calc kpshft F9 F10 F11 F12 mute vol- vol+ (none) (none) " +
            @"= 1 2 3 4 5 6 numlk kp= kpdiv kpmult hyphen " +
            @"tab ' , . p y f kp7 kp8 kp9 kpmin / " +
            @"caps a o e u i lctrl lalt rwin rctrl d kp4 kp5 kp6 kpplus \ " +
            @"lshift ; q j k x bspace delete home pup enter kp0 b kp1 kp2 kp3 kpenter1 rshift " +
            @"` insert left right end pdown up down kp. kpenter2 " +
            @"lp-tab mp-kpshf rp-kpent";

        [Fact]
        public void Qwerty_TopLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(QwertyTopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Advantage2Qwerty.Layers[0]));
        }

        [Fact]
        public void Qwerty_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(QwertyBottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Advantage2Qwerty.Layers[1]));
        }

        [Fact]
        public void Dvorak_TopLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(DvorakTopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Advantage2Dvorak.Layers[0]));
        }

        [Fact]
        public void Dvorak_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(DvorakBottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.Advantage2Dvorak.Layers[1]));
        }

        [Fact]
        public void Layers_OnBothVariants_LockExactlyTheKeypadAndProgramButtons()
        {
            foreach (var geometry in BothVariants())
            {
                foreach (var layer in geometry.Layers)
                {
                    var lockedIndices = layer.Keys
                        .Where(key => !key.CanEdit)
                        .Select(key => key.Index)
                        .ToArray();

                    Assert.Equal(new[] { 16, 17 }, lockedIndices);
                    Assert.Equal(string.Empty, layer.Keys[16].DefaultToken);
                    Assert.Equal(string.Empty, layer.Keys[17].DefaultToken);
                }
            }
        }

        [Fact]
        public void TopLayers_OnBothVariants_RestrictMacrosExactlyAtModifierPositions()
        {
            foreach (var geometry in BothVariants())
            {
                var restrictedIndices = geometry.Layers[0].Keys
                    .Where(key => !key.CanAssignMacro && key.CanEdit)
                    .Select(key => key.Index)
                    .ToArray();

                Assert.Equal(new[] { 48, 49, 50, 51, 58, 75 }, restrictedIndices);
            }
        }

        [Fact]
        public void BottomLayers_OnBothVariants_AlsoRestrictMacrosAtTheLwinAndRaltDefaults()
        {
            foreach (var geometry in BothVariants())
            {
                var restrictedIndices = geometry.Layers[1].Keys
                    .Where(key => !key.CanAssignMacro && key.CanEdit)
                    .Select(key => key.Index)
                    .ToArray();

                Assert.Equal(new[] { 1, 2, 48, 49, 50, 51, 58, 75 }, restrictedIndices);
            }
        }

        [Fact]
        public void PedalPositions_OnTopLayers_CarryTheMasterAppAlternativeTokens()
        {
            foreach (var geometry in BothVariants())
            {
                var topKeys = geometry.Layers[0].Keys;

                Assert.Equal("lp-tab", topKeys[86].DefaultToken);
                Assert.Equal("tab", topKeys[86].MasterAppDefaultToken);
                Assert.Equal("mp-kpshf", topKeys[87].DefaultToken);
                Assert.Equal("kpshft", topKeys[87].MasterAppDefaultToken);
                Assert.Equal("rp-kpent", topKeys[88].DefaultToken);
                Assert.Equal("kpenter", topKeys[88].MasterAppDefaultToken);
            }
        }

        [Fact]
        public void PedalPositions_OnBottomLayers_AlwaysUseThePedalTokens()
        {
            foreach (var geometry in BothVariants())
            {
                var bottomKeys = geometry.Layers[1].Keys;

                Assert.Equal("lp-tab", bottomKeys[86].DefaultToken);
                Assert.Equal("mp-kpshf", bottomKeys[87].DefaultToken);
                Assert.Equal("rp-kpent", bottomKeys[88].DefaultToken);
                Assert.Null(bottomKeys[86].MasterAppDefaultToken);
                Assert.Null(bottomKeys[87].MasterAppDefaultToken);
                Assert.Null(bottomKeys[88].MasterAppDefaultToken);
            }
        }

        [Fact]
        public void Layers_OnBothVariants_UseNoDistinctPositionTokens()
        {
            foreach (var geometry in BothVariants())
            {
                foreach (var layer in geometry.Layers)
                {
                    Assert.All(layer.Keys, key => Assert.Null(key.PositionToken));
                }
            }
        }

        private static IEnumerable<DeviceGeometry> BothVariants()
        {
            yield return GeometryCatalog.Advantage2Qwerty;
            yield return GeometryCatalog.Advantage2Dvorak;
        }
    }
}
