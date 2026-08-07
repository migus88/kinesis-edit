using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class KeyboardLayerViewModelTests
    {
        private static DeviceDefinition RgbDevice => DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);

        [Fact]
        public void BuildAll_ForTheFreestyleEdgeRgb_JoinsEveryKeyToItsPlacement()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            var layers = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null);

            Assert.Equal(2, layers.Count);
            Assert.All(layers, layer => Assert.Equal(95, layer.Keys.Count));
            Assert.All(
                layers,
                layer => Assert.Equal(
                    layer.Layer.Keys.Select(key => key.Index),
                    layer.Keys.Select(key => key.Index)));
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Width, layers[0].BoardWidth);
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Height, layers[0].BoardHeight);
        }

        [Fact]
        public void BuildAll_WithAKeyThatHasNoPlacement_SkipsThatKey()
        {
            var layout = TestLayouts.CreateLayout("esc", "F1", "F2");

            var layers = KeyboardLayerViewModel.BuildAll(layout, TestLayouts.CreateVisual(0, 2), lighting: null);

            Assert.Equal(new[] { 0, 2 }, Assert.Single(layers).Keys.Select(key => key.Index));
        }

        [Fact]
        public void BuildAll_WithAPlacementThatHasNoKey_ProducesNoCapForIt()
        {
            var layout = TestLayouts.CreateLayout("esc");

            var layers = KeyboardLayerViewModel.BuildAll(layout, TestLayouts.CreateVisual(0, 1, 2), lighting: null);

            Assert.Equal(0, Assert.Single(Assert.Single(layers).Keys).Index);
        }

        [Fact]
        public void FindByIndex_ReturnsTheCapOfThatPosition()
        {
            var layer = BuildRgbLayers(lighting: null)[0];

            Assert.Equal(TestLayouts.RgbDigitOneKeyIndex, layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex)!.Index);
            Assert.Null(layer.FindByIndex(4242));
        }

        [Fact]
        public void RefreshFromModel_AfterAModelEdit_UpdatesEveryCap()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            layer.RefreshFromModel();

            Assert.Equal("Z", layer.Keys[TestLayouts.RgbDigitOneKeyIndex].Caption);
            Assert.True(layer.Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [Fact]
        public void BuildAll_WithPerKeyLighting_PutsTheColourOnTheKeyThatOwnsTheMemoryKeyCode()
        {
            var lighting = new LightingModel();

            // The map is keyed by memory key code, not by key index (specs/07-lighting.md §4).
            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));
            lighting.FnLayer.SetKeyColor(TestLayouts.Gen1Key("2").Code, new LedColor(0, 0, 255));

            var layers = BuildRgbLayers(lighting);

            Assert.Equal("#FF0000", layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
            Assert.True(layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].HasColorOverlay);
            Assert.Null(layers[0].Keys[TestLayouts.RgbDigitTwoKeyIndex].ColorOverlayHex);
            Assert.Equal("#0000FF", layers[1].Keys[TestLayouts.RgbDigitTwoKeyIndex].ColorOverlayHex);
            Assert.Null(layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ColorOverlayHex);
        }

        [Fact]
        public void BuildAll_WithABlackKeyColour_ProducesNoOverlay()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, LedColor.Black);

            var layers = BuildRgbLayers(lighting);

            Assert.All(layers[0].Keys, key => Assert.False(key.HasColorOverlay));
        }

        [Fact]
        public void BuildAll_WithoutALightingModel_ProducesNoOverlay()
        {
            var layers = BuildRgbLayers(lighting: null);

            Assert.All(layers[0].Keys, key => Assert.Null(key.ColorOverlayHex));
        }

        [Fact]
        public void Build_ForADeviceWithoutPerKeyRgb_ProducesNoOverlay()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));

            var layout = KeyboardLayout.Create(DeviceId.Advantage2);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, lighting, layout.Layers[0]));
        }

        [Fact]
        public void Build_WithALightingModelOfAnotherShape_IsIgnored()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, new TkoLightingModel(), layout.Layers[0]));
        }

        [Fact]
        public void Build_ForAKeyCodeTheLayerDoesNotCarry_SkipsIt()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("kp7").Code, new LedColor(1, 2, 3));

            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            Assert.Empty(KeyColorOverlay.Build(layout.Device, lighting, layout.Layers[0]));
        }

        [Fact]
        public void ApplyColorOverlays_AfterALightingEdit_RepaintsEveryCap()
        {
            // The colour lives in the lighting model, which no layout parser writes into the key,
            // so RefreshFromModel cannot reach it: the Lighting tab pushes a fresh map in.
            var lighting = new LightingModel();
            var layer = BuildRgbLayers(lighting)[0];
            var key = layer.Keys[TestLayouts.RgbDigitOneKeyIndex];
            var changed = new List<string>();

            key.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(0, 128, 255));
            layer.ApplyColorOverlays(KeyColorOverlay.Build(RgbDevice, lighting, layer.Layer));

            Assert.Equal("#0080FF", key.ColorOverlayHex);
            Assert.True(key.HasColorOverlay);
            Assert.Contains(nameof(KeyboardKeyViewModel.ColorOverlayHex), changed);
            Assert.Contains(nameof(KeyboardKeyViewModel.HasColorOverlay), changed);
        }

        [Fact]
        public void ApplyColorOverlays_WithAKeyTheMapNoLongerMentions_ClearsItsStrip()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));

            var layer = BuildRgbLayers(lighting)[0];

            Assert.True(layer.Keys[TestLayouts.RgbDigitOneKeyIndex].HasColorOverlay);

            // Black is "no colour" (specs/07-lighting.md §2.1): assigning it removes the entry.
            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, LedColor.Black);
            layer.ApplyColorOverlays(KeyColorOverlay.Build(RgbDevice, lighting, layer.Layer));

            Assert.All(layer.Keys, key => Assert.False(key.HasColorOverlay));
        }

        [Fact]
        public void ApplyColorOverlays_WithoutAMap_ClearsEveryStrip()
        {
            var lighting = new LightingModel();

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));

            var layer = BuildRgbLayers(lighting)[0];

            layer.ApplyColorOverlays(null);

            Assert.All(layer.Keys, key => Assert.Null(key.ColorOverlayHex));
        }

        [Theory]
        [InlineData("#0080FF", 0, 128, 255)]
        [InlineData("0080ff", 0, 128, 255)]
        public void TryParseHex_ForAColourString_ReadsItBack(string hex, byte red, byte green, byte blue)
        {
            Assert.True(KeyColorOverlay.TryParseHex(hex, out var color));
            Assert.Equal(new LedColor(red, green, blue), color);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("#FFF")]
        [InlineData("#GGGGGG")]
        [InlineData("rebound")]
        public void TryParseHex_ForAnythingElse_IsFalse(string? hex)
        {
            Assert.False(KeyColorOverlay.TryParseHex(hex, out _));
        }

        [Fact]
        public void Caption_ForALayerViewModel_ComesFromTheDisplayMapping()
        {
            var layers = BuildRgbLayers(lighting: null);

            Assert.Equal("Top", layers[0].Caption);
            Assert.Equal("Fn", layers[1].Caption);
        }

        [Fact]
        public void Sections_ForTheFreestyleEdgeRgb_AreTheBoardsTwoPanels()
        {
            var layer = BuildRgbLayers(lighting: null)[0];

            Assert.Equal(2, layer.Sections.Count);
            Assert.Equal(new[] { 0, 1 }, layer.Sections.Select(section => section.Index));
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Sections[1].X, layer.Sections[1].OriginX);
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Sections[1].Y, layer.Sections[1].OriginY);
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Sections[1].Width, layer.Sections[1].BoardWidth);
            Assert.Equal(VisualCatalog.FreestyleEdgeRgb.Sections[1].Height, layer.Sections[1].BoardHeight);
        }

        [Fact]
        public void Sections_HoldTheVerySameCapInstancesAsTheFlatKeyList()
        {
            // The editor resolves a cap through the flat list and ApplyColorOverlays writes through
            // it; a copy in the sections would leave the drawn board showing state nothing updates.
            var layer = BuildRgbLayers(lighting: null)[0];
            var fromSections = layer.Sections.SelectMany(section => section.Keys).ToList();

            Assert.Equal(layer.Keys.Count, fromSections.Count);

            foreach (var key in layer.Keys)
            {
                Assert.Contains(fromSections, candidate => ReferenceEquals(candidate, key));
            }
        }

        [Fact]
        public void Sections_MutatedThroughTheFlatList_ShowTheChangeToo()
        {
            var lighting = new LightingModel();
            var layer = BuildRgbLayers(lighting)[0];

            lighting.TopLayer.SetKeyColor(TestLayouts.Gen1Key("1").Code, new LedColor(255, 0, 0));
            layer.ApplyColorOverlays(KeyColorOverlay.Build(RgbDevice, lighting, layer.Layer));

            var fromSection = layer.Sections
                .SelectMany(section => section.Keys)
                .Single(key => key.Index == TestLayouts.RgbDigitOneKeyIndex);

            Assert.Equal("#FF0000", fromSection.ColorOverlayHex);
        }

        [Fact]
        public void Counts_OfAFreshLayout_AreZeroExceptForTheGeometrysLockedPositions()
        {
            var layer = BuildRgbLayers(lighting: null)[0];

            Assert.Equal(0, layer.RemappedCount);
            Assert.Equal(0, layer.MacroCount);
            Assert.Equal(0, layer.TapAndHoldCount);
            Assert.Equal(0, layer.AdvisoryCount);
            Assert.Equal(layer.Keys.Count(key => !key.CanEdit), layer.LockedCount);
        }

        [Fact]
        public void Counts_AfterEditsOnTheLayer_FollowTheModelOnRefresh()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];
            var changed = new List<string>();

            layer.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);

            var keys = layout.Layers[0].Keys;

            keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            keys[TestLayouts.RgbDigitTwoKeyIndex].ApplyRemap(TestLayouts.Gen1Key("y"));
            keys[TestLayouts.RgbDigitThreeKeyIndex].SetMacro(1, layout.CreateMacro());
            keys[TestLayouts.RgbDigitThreeKeyIndex].ApplyTapAndHold(
                TestLayouts.Gen1Key("a"),
                TestLayouts.Gen1Key("b"),
                250);

            layer.RefreshFromModel();

            Assert.Equal(2, layer.RemappedCount);
            Assert.Equal(1, layer.MacroCount);
            Assert.Equal(1, layer.TapAndHoldCount);
            Assert.Contains(nameof(KeyboardLayerViewModel.RemappedCount), changed);
            Assert.Contains(nameof(KeyboardLayerViewModel.MacroCount), changed);
            Assert.Contains(nameof(KeyboardLayerViewModel.TapAndHoldCount), changed);
        }

        [Fact]
        public void RefreshCounts_AfterASingleCapWasRefreshed_BringsTheLayerTotalsBackInLine()
        {
            // A single-key edit refreshes only that cap, so the legend row's totals are the one
            // thing that still has to be told.
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layer = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null)[0];

            layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            layer.Keys[TestLayouts.RgbDigitOneKeyIndex].RefreshFromModel();

            Assert.Equal(0, layer.RemappedCount);

            layer.RefreshCounts();

            Assert.Equal(1, layer.RemappedCount);
        }

        [Fact]
        public void Counts_AreScopedToTheirOwnLayer()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var layers = KeyboardLayerViewModel.BuildAll(layout, VisualCatalog.FreestyleEdgeRgb, lighting: null);

            layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            layers[0].RefreshFromModel();
            layers[1].RefreshFromModel();

            Assert.Equal(0, layers[0].RemappedCount);
            Assert.Equal(1, layers[1].RemappedCount);
        }

        [Fact]
        public void AdvisoryCount_IsPushedInBecauseNothingOnTheModelCarriesIt()
        {
            var layer = BuildRgbLayers(lighting: null)[0];
            var changed = new List<string>();

            layer.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);
            layer.AdvisoryCount = 3;

            Assert.Equal(3, layer.AdvisoryCount);
            Assert.Contains(nameof(KeyboardLayerViewModel.AdvisoryCount), changed);

            // RefreshCounts recomputes the four model-derived counts and must not clear this one.
            layer.RefreshCounts();

            Assert.Equal(3, layer.AdvisoryCount);
        }

        [Fact]
        public void LockedCount_CountsThePositionsThatCanNeverBeRemapped()
        {
            var layer = KeyboardLayerViewModel.BuildAll(
                TestLayouts.CreateLockedKeyLayout(),
                TestLayouts.CreateVisual(0, 1, 2),
                lighting: null)[0];

            Assert.Equal(1, layer.LockedCount);
        }

        private static IReadOnlyList<KeyboardLayerViewModel> BuildRgbLayers(object? lighting)
        {
            return KeyboardLayerViewModel.BuildAll(
                KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb),
                VisualCatalog.FreestyleEdgeRgb,
                lighting);
        }
    }
}
