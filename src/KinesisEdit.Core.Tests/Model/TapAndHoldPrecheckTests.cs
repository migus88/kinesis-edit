using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The four pre-dialog tap-and-hold checks of specs/11-feature-dialogs.md §11.1 and their
    /// verbatim refusal wording: same key on both layers, the per-profile maximum, macro trigger
    /// keys, and A-Z / 0-9 on the top layer — evaluated in that order, so a key breaching several
    /// reports the first.
    /// </summary>
    public sealed class TapAndHoldPrecheckTests
    {
        private const string EscapeToken = "esc";
        private const string LetterToken = "a";
        private const string DigitToken = "5";

        [Fact]
        public void Evaluate_WithAnUntouchedNonAlphanumericKey_ReturnsNone()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            Assert.Equal(TapAndHoldRefusal.None, TapAndHoldPrecheck.Evaluate(layout, layer, Key(layer, EscapeToken)));
        }

        [Fact]
        public void Evaluate_WhenTheSamePositionHasTapAndHoldOnTheOtherLayer_ReturnsSameKeyInBothLayers()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var bottomLayer = layout.Layers[1];
            var key = Key(topLayer, EscapeToken);

            AssignTapAndHold(bottomLayer.FindByIndex(key.Index)!);

            Assert.Equal(
                TapAndHoldRefusal.SameKeyInBothLayers,
                TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        [Fact]
        public void Evaluate_WhenOnlyTheKeyItselfHasTapAndHold_ReturnsNone()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var key = Key(layer, EscapeToken);

            AssignTapAndHold(key);

            // Re-opening the dialog on the key being edited is not "the same key in both layers".
            Assert.Equal(TapAndHoldRefusal.None, TapAndHoldPrecheck.Evaluate(layout, layer, key));
        }

        [Fact]
        public void Evaluate_WhenTheLayoutHoldsTheMaximum_ReturnsMaximumReached()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var key = Key(topLayer, EscapeToken);

            FillTapAndHoldSlots(layout, layout.Layers[1], key.Index);

            Assert.Equal(
                layout.Device.TapAndHold.MaxPerLayout,
                layout.TapAndHoldCount);
            Assert.Equal(TapAndHoldRefusal.MaximumReached, TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        [Fact]
        public void Evaluate_WhenTheMaximumIsReachedAndTheSamePositionIsTaken_ReturnsSameKeyInBothLayers()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var bottomLayer = layout.Layers[1];
            var key = Key(topLayer, EscapeToken);

            AssignTapAndHold(bottomLayer.FindByIndex(key.Index)!);
            FillTapAndHoldSlots(layout, bottomLayer, key.Index);

            Assert.Equal(
                TapAndHoldRefusal.SameKeyInBothLayers,
                TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        [Fact]
        public void Evaluate_WithNoMaximumInTheCapability_SkipsTheMaximumCheck()
        {
            var device = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb) with
            {
                TapAndHold = TapAndHoldCapability.None
            };

            Assert.True(GeometryCatalog.TryGet(DeviceId.FreestyleEdgeRgb, out var geometry));

            var layout = new KeyboardLayout(device, geometry!);
            var topLayer = layout.Layers[0];
            var key = Key(topLayer, EscapeToken);

            FillTapAndHoldSlots(layout, layout.Layers[1], key.Index);

            Assert.Equal(TapAndHoldRefusal.None, TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        [Fact]
        public void Evaluate_WhenTheKeyHostsASlotMacro_ReturnsMacroTriggerKey()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var key = Key(layer, EscapeToken);

            Assert.NotEqual(0, key.AssignMacro(layout.CreateMacro()));
            Assert.Equal(TapAndHoldRefusal.MacroTriggerKey, TapAndHoldPrecheck.Evaluate(layout, layer, key));
        }

        [Fact]
        public void Evaluate_WhenTheKeyHostsAFlatListMacro_ReturnsMacroTriggerKey()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var layer = layout.Layers[0];
            var key = layer.Keys.First(candidate => candidate.OriginalKey.Table != KeyTable.LettersAndDigits);

            var macro = layout.CreateMacro();
            macro.TriggerKey = key.TriggerKey.Code;
            macro.LayerIndex = layer.Index;
            layout.AddMacro(macro);

            Assert.Equal(TapAndHoldRefusal.MacroTriggerKey, TapAndHoldPrecheck.Evaluate(layout, layer, key));
        }

        [Fact]
        public void Evaluate_WhenTheMaximumIsReachedAndTheKeyHostsAMacro_ReturnsMaximumReached()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var key = Key(topLayer, EscapeToken);

            Assert.NotEqual(0, key.AssignMacro(layout.CreateMacro()));
            FillTapAndHoldSlots(layout, layout.Layers[1], key.Index);

            Assert.Equal(TapAndHoldRefusal.MaximumReached, TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        [Theory]
        [InlineData(LetterToken)]
        [InlineData(DigitToken)]
        public void Evaluate_WithAnAlphanumericKeyOnTheTopLayer_ReturnsAlphanumericOnTopLayer(string token)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];

            Assert.Equal(
                TapAndHoldRefusal.AlphanumericOnTopLayer,
                TapAndHoldPrecheck.Evaluate(layout, layer, Key(layer, token)));
        }

        [Fact]
        public void Evaluate_WithAnAlphanumericKeyOnTheBottomLayer_ReturnsNone()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[1];

            Assert.Equal(TapAndHoldRefusal.None, TapAndHoldPrecheck.Evaluate(layout, layer, Key(layer, LetterToken)));
        }

        [Fact]
        public void Evaluate_WhenAnAlphanumericTopLayerKeyHostsAMacro_ReturnsMacroTriggerKey()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var key = Key(layer, LetterToken);

            Assert.NotEqual(0, key.AssignMacro(layout.CreateMacro()));
            Assert.Equal(TapAndHoldRefusal.MacroTriggerKey, TapAndHoldPrecheck.Evaluate(layout, layer, key));
        }

        [Fact]
        public void Evaluate_WithNullArguments_Throws()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = layout.Layers[0];
            var key = Key(layer, EscapeToken);

            Assert.Throws<ArgumentNullException>(() => TapAndHoldPrecheck.Evaluate(null!, layer, key));
            Assert.Throws<ArgumentNullException>(() => TapAndHoldPrecheck.Evaluate(layout, null!, key));
            Assert.Throws<ArgumentNullException>(() => TapAndHoldPrecheck.Evaluate(layout, layer, null!));
        }

        [Fact]
        public void MessageFor_ForEachRefusal_ReturnsTheSpecWording()
        {
            Assert.Equal(string.Empty, TapAndHoldPrecheck.MessageFor(TapAndHoldRefusal.None));

            Assert.Equal(
                "You cannot assign a Tap and Hold Action to the same key in both layers.",
                TapAndHoldPrecheck.MessageFor(TapAndHoldRefusal.SameKeyInBothLayers));

            Assert.Equal(
                "You have reached the maximum number of Tap and Hold actions for this Profile.",
                TapAndHoldPrecheck.MessageFor(TapAndHoldRefusal.MaximumReached));

            Assert.Equal(
                "You cannot assign a Tap and Hold Action to a macro trigger key.",
                TapAndHoldPrecheck.MessageFor(TapAndHoldRefusal.MacroTriggerKey));

            Assert.Equal(
                "You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.",
                TapAndHoldPrecheck.MessageFor(TapAndHoldRefusal.AlphanumericOnTopLayer));
        }

        [Fact]
        public void MessageFor_WithAnUndefinedRefusal_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TapAndHoldPrecheck.MessageFor((TapAndHoldRefusal)99));
        }

        /// <summary>
        /// specs/11-feature-dialogs.md §11.1 caps how many tap-and-hold actions a profile
        /// <em>has</em> ("Maximum of 10 tap-and-hold actions per profile reached"). Re-opening the
        /// dialog on a key that already carries one rewrites that assignment rather than adding an
        /// eleventh, so the key under edit is excluded from the count — otherwise the last ten
        /// assignments of a full profile could never be edited again.
        /// </summary>
        [Fact]
        public void Evaluate_ForAKeyThatAlreadyCarriesOneWithTheProfileFull_ReturnsNone()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var key = Key(topLayer, EscapeToken);

            AssignTapAndHold(key);
            FillTapAndHoldSlots(layout, layout.Layers[1], key.Index);

            Assert.Equal(layout.Device.TapAndHold.MaxPerLayout, layout.TapAndHoldCount);
            Assert.Equal(TapAndHoldRefusal.None, TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        /// <summary>
        /// The other half of the same rule: a key carrying nothing yet really would be the
        /// eleventh, so it is still refused.
        /// </summary>
        [Fact]
        public void Evaluate_ForAFreshKeyWithTheProfileFull_StillReturnsMaximumReached()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var key = Key(topLayer, EscapeToken);

            FillTapAndHoldSlots(layout, layout.Layers[1], key.Index);

            Assert.False(key.IsTapAndHold);
            Assert.Equal(TapAndHoldRefusal.MaximumReached, TapAndHoldPrecheck.Evaluate(layout, topLayer, key));
        }

        private static KeyboardKey Key(KeyboardLayer layer, string token)
        {
            var definition = KeyRegistry.FindByToken(token, TokenDialect.Gen1)
                             ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in Gen1.");

            return layer.FindByPositionKeyCode(definition.Code)
                   ?? throw new InvalidOperationException($"Layer {layer.Index} has no \"{token}\" position.");
        }

        private static void AssignTapAndHold(KeyboardKey key)
        {
            var tap = KeyRegistry.FindByToken("a", TokenDialect.Gen1)!;
            var hold = KeyRegistry.FindByToken("lctrl", TokenDialect.Gen1)!;

            Assert.True(key.SetTapAndHold(tap, hold, 250));
        }

        /// <summary>
        /// Fills <paramref name="layer"/> with tap-and-hold assignments until the device's
        /// <see cref="TapAndHoldCapability.MaxPerLayout"/> is reached, never touching
        /// <paramref name="skippedKeyIndex"/> so the same-key check stays out of the way.
        /// </summary>
        private static void FillTapAndHoldSlots(KeyboardLayout layout, KeyboardLayer layer, int skippedKeyIndex)
        {
            var maximum = layout.Device.TapAndHold.MaxPerLayout ?? 10;

            foreach (var key in layer.Keys)
            {
                if (layout.TapAndHoldCount >= maximum)
                {
                    return;
                }

                if (key.Index == skippedKeyIndex || !key.CanEdit || key.IsTapAndHold)
                {
                    continue;
                }

                AssignTapAndHold(key);
            }
        }
    }
}
