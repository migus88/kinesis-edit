using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// One layer of the runtime model per specs/05-key-model.md §1.4 and the four lookup paths of
    /// §1.5: by index, by original key code, by position key code, and by trigger identity.
    /// </summary>
    public class KeyboardLayerTests
    {
        [Fact]
        public void Constructor_WithNonDenseIndices_Throws()
        {
            var keys = new[]
            {
                ModelTokens.CreateKey("a", index: 0),
                ModelTokens.CreateKey("b", index: 2)
            };

            Assert.Throws<ArgumentException>(() => new KeyboardLayer("Broken", 0, 0, keys));
        }

        [Fact]
        public void Constructor_ForAValidLayer_ExposesKeysInIndexOrder()
        {
            var keys = new[]
            {
                ModelTokens.CreateKey("a", index: 0),
                ModelTokens.CreateKey("b", index: 1)
            };

            var layer = new KeyboardLayer("Pair", 1, 0, keys);

            Assert.Equal("Pair", layer.Name);
            Assert.Equal(1, layer.Index);
            Assert.Equal(2, layer.Keys.Count);
            Assert.Equal(new[] { 0, 1 }, layer.Keys.Select(key => key.Index));
            Assert.Empty(layer.EdgeKeys);
        }

        [Fact]
        public void FindByIndex_ForEveryPosition_ReturnsTheKeyAtThatOrdinal()
        {
            var layer = BaseLayer();

            for (var index = 0; index < layer.Keys.Count; index++)
            {
                Assert.Same(layer.Keys[index], layer.FindByIndex(index));
            }

            Assert.Null(layer.FindByIndex(-1));
            Assert.Null(layer.FindByIndex(layer.Keys.Count));
        }

        [Fact]
        public void FindByOriginalKeyCode_ForTheKeypadToggle_ReturnsTheBaseLayerPosition()
        {
            var key = BaseLayer().FindByOriginalKeyCode(KeyboardKey.KeypadToggleKeyCode);

            Assert.NotNull(key);
            Assert.Equal(6, key.Index);
        }

        [Fact]
        public void FindByPositionKeyCode_ForTheKeypadPositionToken_ReturnsTheSamePositionOnEveryLayer()
        {
            var keypadPositionCode = ModelTokens.Key("kp", TokenDialect.Gen2).Code;

            foreach (var layer in Advantage360().Layers)
            {
                var key = layer.FindByPositionKeyCode(keypadPositionCode);

                Assert.NotNull(key);
                Assert.Equal(6, key.Index);
            }
        }

        [Fact]
        public void FindByTriggerKeyCode_OnTheBaseLayer_ResolvesTheLayerSwitchException()
        {
            var layer = BaseLayer();

            var byTrigger = layer.FindByTriggerKeyCode(KeyboardKey.KeypadToggleKeyCode);
            var byPosition = layer.FindByPositionKeyCode(ModelTokens.Key("kp", TokenDialect.Gen2).Code);

            Assert.NotNull(byTrigger);
            Assert.Equal(6, byTrigger.Index);
            Assert.Same(byTrigger, byPosition);
        }

        [Fact]
        public void FindByTriggerKeyCode_OnTheKeypadLayer_UsesThePositionKeyInstead()
        {
            var layer = Advantage360().Layers[1];

            Assert.Null(layer.FindByTriggerKeyCode(KeyboardKey.KeypadToggleKeyCode));

            var byTrigger = layer.FindByTriggerKeyCode(ModelTokens.Key("kp", TokenDialect.Gen2).Code);

            Assert.NotNull(byTrigger);
            Assert.Equal(6, byTrigger.Index);
            Assert.Equal(ModelTokens.Key("deft", TokenDialect.Gen2), byTrigger.OriginalKey);
        }

        [Fact]
        public void FindByTriggerKeyCode_ForTheFnShiftKeys_ReturnsTheLeftmostOfThePair()
        {
            var key = BaseLayer().FindByTriggerKeyCode(KeyboardKey.Fn1ShiftKeyCode);

            Assert.NotNull(key);
            Assert.Equal(54, key.Index);
        }

        [Fact]
        public void FindByKeyCode_ForACodeNoPositionCarries_ReturnsNullOnEveryLookupPath()
        {
            var layer = BaseLayer();

            Assert.Null(layer.FindByOriginalKeyCode(-1));
            Assert.Null(layer.FindByPositionKeyCode(-1));
            Assert.Null(layer.FindByTriggerKeyCode(-1));
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, LayoutVariant.Qwerty, 0, 0)]
        [InlineData(DeviceId.Advantage2, LayoutVariant.Dvorak, 1, 1)]
        [InlineData(DeviceId.FreestyleEdge, LayoutVariant.None, 0, 0)]
        [InlineData(DeviceId.Tko, LayoutVariant.None, 0, 0)]
        public void LayerType_OnTheTwoLayerDevices_IsTheLayoutVariant(
            DeviceId deviceId,
            LayoutVariant variant,
            int expectedTopType,
            int expectedBottomType)
        {
            var layout = KeyboardLayout.Create(deviceId, variant);

            Assert.Equal(expectedTopType, layout.Layers[0].LayerType);
            Assert.Equal(expectedBottomType, layout.Layers[1].LayerType);
        }

        [Fact]
        public void LayerType_OnTheAdvantage360_MirrorsTheLayerIndex()
        {
            foreach (var layer in Advantage360().Layers)
            {
                Assert.Equal(layer.Index, layer.LayerType);
            }
        }

        [Fact]
        public void EdgeKeys_OnTheTko_HoldTheThirtyThreeLightingZones()
        {
            foreach (var layer in KeyboardLayout.Create(DeviceId.Tko).Layers)
            {
                Assert.Equal(33, layer.EdgeKeys.Count);
                Assert.Equal("L1", layer.EdgeKeys[0].OriginalKey.Gen1Token);
                Assert.Equal("R9", layer.EdgeKeys[32].OriginalKey.Gen1Token);
                Assert.Same(layer.EdgeKeys[5], layer.FindEdgeKeyByIndex(5));
            }
        }

        [Fact]
        public void EdgeKeys_OnTheTko_AreLightingZonesThatAcceptNoRemapOrMacro()
        {
            var zone = KeyboardLayout.Create(DeviceId.Tko).Layers[0].EdgeKeys[0];

            // 04 §3.4: edge-lighting lines live in led*.txt and never in a layout file, and 05 §4.4
            // lists no remap or macro for the zones — the only state they hold is their colour.
            Assert.False(zone.CanEdit);
            Assert.False(zone.CanAssignMacro);
            Assert.False(zone.Remap(ModelTokens.Key("a")));
            Assert.False(zone.SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250));
            Assert.Equal(0, zone.AssignMacro(new Macro()));

            zone.KeyColor = new KeyColor(255, 0, 0);

            Assert.Equal(new KeyColor(255, 0, 0), zone.KeyColor);
        }

        [Fact]
        public void EdgeKeys_OnTheTko_AreOutsideTheCountersButNotOutsideValidation()
        {
            var layout = KeyboardLayout.Create(DeviceId.Tko);
            var zone = layout.Layers[0].EdgeKeys[0];

            zone.ApplyRemap(ModelTokens.Key("a"));
            zone.ApplyTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250);
            zone.ApplyMultiModifiers("caws");
            zone.SetMacro(1, new Macro());

            var violations = layout.Validate();

            // The zones are not editable positions, so they stay out of the counters — but state a
            // tolerant load put on one must not become invisible because of that.
            Assert.Equal(0, layout.MacroCount);
            Assert.Equal(0, layout.ModifiedKeyCount);
            Assert.Equal(0, layout.TapAndHoldCount);
            Assert.Contains(violations, violation => violation.Kind == ModelViolationKind.EditOnLockedKey && violation.KeyIndex == 0);
            Assert.Contains(violations, violation => violation.Kind == ModelViolationKind.MacroOnRestrictedKey && violation.KeyIndex == 0);
        }

        [Fact]
        public void EdgeKeys_OnATkoWithoutStrayState_ReportNothing()
        {
            Assert.Empty(KeyboardLayout.Create(DeviceId.Tko).Validate());
        }

        [Fact]
        public void EdgeKeys_OnEveryOtherDevice_AreEmpty()
        {
            foreach (var layer in Advantage360().Layers)
            {
                Assert.Empty(layer.EdgeKeys);
                Assert.Null(layer.FindEdgeKeyByIndex(0));
            }
        }

        [Fact]
        public void Reset_OnALayer_ClearsEveryKey()
        {
            var layer = BaseLayer();

            layer.Keys[10].Remap(ModelTokens.Key("a", TokenDialect.Gen2));
            layer.Keys[11].SetMacro(1, new Macro());

            layer.Reset();

            Assert.False(layer.Keys[10].IsModified);
            Assert.False(layer.Keys[11].IsMacro);
        }

        private static KeyboardLayout Advantage360()
        {
            return KeyboardLayout.Create(DeviceId.Advantage360);
        }

        private static KeyboardLayer BaseLayer()
        {
            return Advantage360().Layers[0];
        }
    }
}
