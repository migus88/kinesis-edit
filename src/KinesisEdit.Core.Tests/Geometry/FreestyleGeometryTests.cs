using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Tests.Geometry
{
    /// <summary>
    /// Full-layer token assertions transcribed independently from
    /// specs/05-key-model.md §4.1-§4.3 (Gen1 file tokens, so the backtick position
    /// is "tilde" per spec 05 §3.2).
    /// </summary>
    public class FreestyleGeometryTests
    {
        private const string EdgeTopTokens =
            @"esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 prnt scrlk pause del hk1 hk2 " +
            @"tilde 1 2 3 4 5 6 7 8 9 0 hyph = bspc home hk3 hk4 " +
            @"tab q w e r t y u i o p obrk cbrk \ end hk5 hk6 " +
            @"caps a s d f g h j k l colon apos ent pup hk7 hk8 " +
            @"lshft z x c v b n m com per / rshft up pdn hk9 hk10 " +
            @"lctrl lwin lalt lspc rspc ralt rctrl lft dwn rght";

        private const string EdgeBottomTokens =
            @"esc mute vol- vol+ play prev next F7 F8 F9 F10 F11 F12 prnt scrlk ins del hk1 hk2 " +
            @"tilde 1 2 3 4 5 6 7 8 9 0 hyph = bspc home hk3 hk4 " +
            @"tab q w e r t y u i o p obrk cbrk \ end hk5 hk6 " +
            @"caps a s d f g h j k l colon apos ent pup hk7 hk8 " +
            @"lshft z x c v b n m com per / rshft up pdn hk9 hk10 " +
            @"lctrl lwin lalt lspc rspc ralt rctrl lft dwn rght";

        private const string RgbTopTokens =
            @"hk0 esc F1 F2 F3 F4 F5 F6 F7 F8 F9 F10 F11 F12 prnt pause del hk1 hk2 " +
            @"tilde 1 2 3 4 5 6 7 8 9 0 hyph = bspc home hk3 hk4 " +
            @"tab q w e r t y u i o p obrk cbrk \ end hk5 hk6 " +
            @"caps a s d f g h j k l colon apos ent pup hk7 hk8 " +
            @"lshft z x c v b n m com per / rshft up pdn hk9 hk10 " +
            @"lctrl lwin lalt lspc rspc ralt rctrl lft dwn rght";

        private const string RgbBottomTokens =
            @"hk0 esc mute vol- vol+ play prev next F7 F8 F9 F10 F11 F12 prnt ins scrlk hk1 hk2 " +
            @"tilde 1 2 3 4 5 6 7 8 9 0 hyph = bspc home hk3 hk4 " +
            @"tab q w e r t y u i o p obrk cbrk \ end hk5 hk6 " +
            @"caps a s d f g h j k l colon apos ent pup hk7 hk8 " +
            @"lshft z x c v b n m com per / rshft up pdn hk9 hk10 " +
            @"lctrl lwin lalt lspc rspc ralt rctrl lft dwn rght";

        private const string ProBottomTokens =
            @"esc mute vol- vol+ play prev next F7 F8 F9 F10 F11 F12 prnt numlk ins del hk1 hk2 " +
            @"tilde 1 2 3 4 5 6 kp7 kp8 kp9 0 kp* = bspc home hk3 hk4 " +
            @"tab q w e r t y kp4 kp5 kp6 kp- obrk cbrk \ end hk5 hk6 " +
            @"caps a s d f g h kp1 kp2 kp3 kp+ apos kpent pup hk7 hk8 " +
            @"lshft z x c v b n kp0 com kp. kp/ rshft up pdn hk9 hk10 " +
            @"lctrl lwin lalt lspc rspc ralt rctrl lft dwn rght";

        [Fact]
        public void FreestyleEdge_TopLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(EdgeTopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestyleEdge.Layers[0]));
        }

        [Fact]
        public void FreestyleEdge_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(EdgeBottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestyleEdge.Layers[1]));
        }

        [Fact]
        public void FreestyleEdgeRgb_TopLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(RgbTopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestyleEdgeRgb.Layers[0]));
        }

        [Fact]
        public void FreestyleEdgeRgb_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(RgbBottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestyleEdgeRgb.Layers[1]));
        }

        [Fact]
        public void FreestylePro_TopLayer_MatchesEdgeTopLayer()
        {
            Assert.Equal(EdgeTopTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestylePro.Layers[0]));
        }

        [Fact]
        public void FreestylePro_BottomLayer_MatchesSpecTokenSequence()
        {
            Assert.Equal(ProBottomTokens, GeometryTokens.RenderDefaults(GeometryCatalog.FreestylePro.Layers[1]));
        }

        [Fact]
        public void FreestyleEdgeRgb_AtIndexZero_HasHotkeyZeroWhereEdgeHasEscape()
        {
            Assert.Equal("hk0", GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys[0].DefaultToken);
            Assert.Equal("esc", GeometryCatalog.FreestyleEdge.Layers[0].Keys[0].DefaultToken);
        }

        [Fact]
        public void FreestyleEdgeRgb_TopLayer_HasNoScrollLockPosition()
        {
            Assert.DoesNotContain(GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys, key => key.DefaultToken == "scrlk");
        }

        [Fact]
        public void Layers_OnAllFreestyleDevices_RestrictMacrosExactlyAtModifierPositions()
        {
            var expectedRestrictedIndices = new[] { 69, 80, 85, 86, 87, 90, 91 };

            var geometries = new[]
            {
                GeometryCatalog.FreestyleEdge,
                GeometryCatalog.FreestyleEdgeRgb,
                GeometryCatalog.FreestylePro
            };

            foreach (var geometry in geometries)
            {
                foreach (var layer in geometry.Layers)
                {
                    var restrictedIndices = layer.Keys
                        .Where(key => !key.CanAssignMacro)
                        .Select(key => key.Index)
                        .ToArray();

                    Assert.Equal(expectedRestrictedIndices, restrictedIndices);
                }
            }
        }

        [Fact]
        public void Layers_OnAllFreestyleDevices_HaveNoLockedPositions()
        {
            var geometries = new[]
            {
                GeometryCatalog.FreestyleEdge,
                GeometryCatalog.FreestyleEdgeRgb,
                GeometryCatalog.FreestylePro
            };

            foreach (var geometry in geometries)
            {
                foreach (var layer in geometry.Layers)
                {
                    Assert.All(layer.Keys, key => Assert.True(key.CanEdit));
                }
            }
        }

        [Fact]
        public void Layers_OnAllFreestyleDevices_UseNoDistinctPositionTokens()
        {
            var geometries = new[]
            {
                GeometryCatalog.FreestyleEdge,
                GeometryCatalog.FreestyleEdgeRgb,
                GeometryCatalog.FreestylePro
            };

            foreach (var geometry in geometries)
            {
                foreach (var layer in geometry.Layers)
                {
                    Assert.All(layer.Keys, key => Assert.Null(key.PositionToken));
                }
            }
        }
    }
}
