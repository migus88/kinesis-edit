using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The Advantage360 runtime model transcribed independently from specs/05-key-model.md §4.7:
    /// for all 77 positions of all 5 layers, the factory default action (§1.3 OriginalKey), the
    /// token naming the physical position (PositionKey, the "(pos:x)" mechanism of §4.7/§7.3), and
    /// the identity macro triggers match (TriggerKey — the original key for the Fn1 layer-shift
    /// and keypad layer-toggle keys, the position key otherwise; §1.3, §1.5, 06 §1).
    /// </summary>
    public class Advantage360ModelTests
    {
        private const int PositionCount = 77;
        private const int LayerCount = 5;

        private const string BaseOriginals =
            "eql 1 2 3 4 5 keyt ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "fn1s grav caps left rght up down obrk cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn null";

        // §4.7: the physical position names — identical on every layer (§7.4) — differ from the
        // Base defaults only at 6 (kp), 54/63 (lfn/rfn) and 76 (pedl).
        private const string SharedPositions =
            "eql 1 2 3 4 5 kp ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "lfn grav caps left rght up down obrk cbrk rfn " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn pedl";

        // Base: keyt at 6 and fn1s at 54/63 report their original action as the trigger identity.
        private const string BaseTriggers =
            "eql 1 2 3 4 5 keyt ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "fn1s grav caps left rght up down obrk cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn pedl";

        private const string KeypadOriginals =
            "eql 1 2 3 4 5 deft ss 6 nmlk kp= kp/ kp* hyph " +
            "tab q w e r t hk1 hk3 y kp7 kp8 kp9 kp- bsls " +
            "esc a s d f g hk2 hk4 h kp4 kp5 kp6 kp+ apos " +
            "lshf z x c v b n kp1 kp2 kp3 kpen rshf " +
            "fn1s grav caps left rght up down kp. cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent kp0 end pgdn null";

        // Keypad: the toggle at 6 defaults to deft, not keyt, so its trigger identity stays the
        // position token; 54/63 are still fn1s.
        private const string KeypadTriggers =
            "eql 1 2 3 4 5 kp ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "fn1s grav caps left rght up down obrk cbrk fn1s " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn pedl";

        private const string Fn1Originals =
            "F1 F2 F3 F4 F5 F6 keyt ss F7 F8 F9 F10 F11 F12 " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "defs grav caps left rght up down obrk cbrk defs " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn null";

        // Fn1: 54/63 default to defs, so their trigger identity is the position token, while the
        // keypad toggle at 6 is keyt again.
        private const string Fn1Triggers =
            "eql 1 2 3 4 5 keyt ss 6 7 8 9 0 hyph " +
            "tab q w e r t hk1 hk3 y u i o p bsls " +
            "esc a s d f g hk2 hk4 h j k l scol apos " +
            "lshf z x c v b n m comm perd fsls rshf " +
            "lfn grav caps left rght up down obrk cbrk rfn " +
            "lctr lalt rwin rctr bspc del home pgup ent spc end pgdn pedl";

        private static KeyboardLayout Layout { get; } = KeyboardLayout.Create(DeviceId.Advantage360);

        [Fact]
        public void Layout_ForTheAdvantage360_HasFiveLayersOfSeventySevenPositions()
        {
            Assert.Equal(LayerCount, Layout.Layers.Count);

            foreach (var layer in Layout.Layers)
            {
                Assert.Equal(PositionCount, layer.Keys.Count);
            }
        }

        [Theory]
        [InlineData(0, "Base", BaseOriginals)]
        [InlineData(1, "Keypad", KeypadOriginals)]
        [InlineData(2, "Fn1", Fn1Originals)]
        [InlineData(3, "Fn2", Fn1Originals)]
        [InlineData(4, "Fn3", Fn1Originals)]
        public void OriginalKeys_OnEveryLayer_MatchTheSpecDefaults(int layerIndex, string name, string expected)
        {
            var layer = Layout.Layers[layerIndex];

            Assert.Equal(name, layer.Name);
            Assert.Equal(expected, ModelTokens.RenderOriginals(layer, TokenDialect.Gen2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void PositionKeys_OnEveryLayer_NameTheSamePhysicalPositions(int layerIndex)
        {
            Assert.Equal(SharedPositions, ModelTokens.RenderPositions(Layout.Layers[layerIndex], TokenDialect.Gen2));
        }

        [Theory]
        [InlineData(0, BaseTriggers)]
        [InlineData(1, KeypadTriggers)]
        [InlineData(2, Fn1Triggers)]
        [InlineData(3, Fn1Triggers)]
        [InlineData(4, Fn1Triggers)]
        public void TriggerKeys_OnEveryLayer_ResolveTheLayerSwitchExceptions(int layerIndex, string expected)
        {
            Assert.Equal(expected, ModelTokens.RenderTriggers(Layout.Layers[layerIndex], TokenDialect.Gen2));
        }

        [Theory]
        [InlineData(0, 6, "keyt", "kp", "keyt")]
        [InlineData(0, 54, "fn1s", "lfn", "fn1s")]
        [InlineData(0, 63, "fn1s", "rfn", "fn1s")]
        [InlineData(0, 76, "null", "pedl", "pedl")]
        [InlineData(1, 6, "deft", "kp", "kp")]
        [InlineData(1, 23, "kp7", "u", "u")]
        [InlineData(1, 52, "kpen", "fsls", "fsls")]
        [InlineData(1, 73, "kp0", "spc", "spc")]
        [InlineData(2, 6, "keyt", "kp", "keyt")]
        [InlineData(2, 54, "defs", "lfn", "lfn")]
        [InlineData(2, 63, "defs", "rfn", "rfn")]
        [InlineData(2, 0, "F1", "eql", "eql")]
        public void DivergentPositions_OnTheLayersOfSpecSection47_CarryDistinctOriginalPositionAndTriggerIdentities(
            int layerIndex,
            int keyIndex,
            string original,
            string position,
            string trigger)
        {
            var key = Layout.Layers[layerIndex].Keys[keyIndex];

            Assert.Equal(original, key.OriginalKey.Gen2Token);
            Assert.Equal(position, key.PositionKey.Gen2Token);
            Assert.Equal(trigger, key.TriggerKey.Gen2Token);
        }

        [Fact]
        public void KeypadLayer_AtEveryEmbeddedKeypadPosition_KeepsTheBaseLayerPositionToken()
        {
            var basePositions = Layout.Layers[0];
            var keypadLayer = Layout.Layers[1];

            var divergentIndices = new[]
            {
                6, 9, 10, 11, 12, 23, 24, 25, 26, 37, 38, 39, 40, 49, 50, 51, 52, 61, 73
            };

            foreach (var index in divergentIndices)
            {
                var keypadKey = keypadLayer.Keys[index];

                Assert.NotEqual(keypadKey.OriginalKey.Code, keypadKey.PositionKey.Code);
                Assert.Equal(basePositions.Keys[index].PositionKey.Code, keypadKey.PositionKey.Code);
            }
        }

        [Fact]
        public void PositionIdentities_AcrossAllLayers_ShareIndexAndPositionKey()
        {
            var baseLayer = Layout.Layers[0];

            foreach (var layer in Layout.Layers)
            {
                for (var index = 0; index < PositionCount; index++)
                {
                    Assert.Equal(index, layer.Keys[index].Index);
                    Assert.Equal(baseLayer.Keys[index].PositionKey, layer.Keys[index].PositionKey);
                }
            }
        }

        [Fact]
        public void SmartSetKey_OnEveryLayer_IsTheOnlyLockedPosition()
        {
            foreach (var layer in Layout.Layers)
            {
                var lockedIndices = layer.Keys.Where(key => !key.CanEdit).Select(key => key.Index).ToArray();

                Assert.Equal(new[] { 7 }, lockedIndices);
                Assert.Equal("ss", layer.Keys[7].OriginalKey.Gen2Token);
            }
        }

        [Fact]
        public void ModifierPositions_OnEveryLayer_AcceptRemapsAndMacros()
        {
            foreach (var layer in Layout.Layers)
            {
                foreach (var index in new[] { 42, 53, 64, 65, 66, 67 })
                {
                    Assert.True(layer.Keys[index].CanEdit);
                    Assert.True(layer.Keys[index].CanAssignMacro);
                }
            }
        }

        [Fact]
        public void TriggerLookup_ForTheKeypadToggle_HitsTheBaseAndFnLayersOnly()
        {
            var layersWithKeypadToggleTrigger = Layout.Layers
                .Where(layer => layer.FindByTriggerKeyCode(KeyboardKey.KeypadToggleKeyCode) is not null)
                .Select(layer => layer.Index)
                .ToArray();

            Assert.Equal(new[] { 0, 2, 3, 4 }, layersWithKeypadToggleTrigger);
        }

        [Fact]
        public void TriggerLookup_ForTheFn1Shift_HitsTheBaseAndKeypadLayersOnly()
        {
            var layersWithFn1ShiftTrigger = Layout.Layers
                .Where(layer => layer.FindByTriggerKeyCode(KeyboardKey.Fn1ShiftKeyCode) is not null)
                .Select(layer => layer.Index)
                .ToArray();

            Assert.Equal(new[] { 0, 1 }, layersWithFn1ShiftTrigger);
        }
    }
}
