using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The layout-level model of specs/05-key-model.md §1.4/§1.5 and specs/06-macros.md §1: layers
    /// built from a device's geometry, the Gen2 flat macro list, the counters and limits of
    /// specs/04-layout-file-format.md §5.3 and specs/06-macros.md §6, and the report-never-enforce
    /// rule — every violation kind is raised while the offending state stays in the model, because
    /// files written by older firmware must still load.
    /// </summary>
    public class KeyboardLayoutTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdge, LayoutVariant.None, 2, 95, TokenDialect.Gen1)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LayoutVariant.None, 2, 95, TokenDialect.Gen1)]
        [InlineData(DeviceId.FreestylePro, LayoutVariant.None, 2, 95, TokenDialect.Gen1)]
        [InlineData(DeviceId.Tko, LayoutVariant.None, 2, 63, TokenDialect.Gen1)]
        [InlineData(DeviceId.Advantage2, LayoutVariant.None, 2, 89, TokenDialect.Legacy)]
        [InlineData(DeviceId.Advantage2, LayoutVariant.Qwerty, 2, 89, TokenDialect.Legacy)]
        [InlineData(DeviceId.Advantage2, LayoutVariant.Dvorak, 2, 89, TokenDialect.Legacy)]
        [InlineData(DeviceId.Advantage360, LayoutVariant.None, 5, 77, TokenDialect.Gen2)]
        public void Create_ForEveryProgrammableDevice_BuildsDenseLayers(
            DeviceId deviceId,
            LayoutVariant variant,
            int expectedLayers,
            int expectedKeysPerLayer,
            TokenDialect expectedDialect)
        {
            var layout = KeyboardLayout.Create(deviceId, variant);

            Assert.Equal(deviceId, layout.Device.Id);
            Assert.Equal(expectedDialect, layout.Dialect);
            Assert.Equal(expectedLayers, layout.Layers.Count);

            for (var index = 0; index < layout.Layers.Count; index++)
            {
                var layer = layout.Layers[index];

                Assert.Equal(index, layer.Index);
                Assert.Same(layer, layout.FindLayer(index));
                Assert.Equal(expectedKeysPerLayer, layer.Keys.Count);
                Assert.Equal(Enumerable.Range(0, expectedKeysPerLayer), layer.Keys.Select(key => key.Index));
            }
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void Create_ForADeviceWithoutGeometry_Throws(DeviceId deviceId)
        {
            Assert.Throws<ArgumentException>(() => KeyboardLayout.Create(deviceId));
        }

        [Fact]
        public void Create_ForTheAdvantage2_ResolvesTheTokenlessKeypadAndProgramButtons()
        {
            var topLayer = KeyboardLayout.Create(DeviceId.Advantage2).Layers[0];

            Assert.Equal(10014, topLayer.Keys[16].OriginalKey.Code);
            Assert.Equal(10013, topLayer.Keys[17].OriginalKey.Code);
            Assert.False(topLayer.Keys[16].CanEdit);
            Assert.False(topLayer.Keys[17].CanEdit);
        }

        [Fact]
        public void UsesFlatMacroList_AcrossTheDeviceFamilies_IsTrueOnlyOnTheAdvantage360()
        {
            Assert.True(KeyboardLayout.Create(DeviceId.Advantage360).UsesFlatMacroList);
            Assert.False(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb).UsesFlatMacroList);
        }

        [Fact]
        public void AddMacro_OnASlotBasedDevice_Throws()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            Assert.Throws<InvalidOperationException>(() => layout.AddMacro(layout.CreateMacro()));
        }

        [Fact]
        public void AddAndRemoveMacro_OnTheAdvantage360_MaintainTheFlatList()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 2, triggerKey: 4242);

            layout.AddMacro(macro);

            Assert.Equal(1, layout.MacroCount);
            Assert.Equal(new[] { macro }, layout.FindMacros(2, 4242));
            Assert.Empty(layout.FindMacros(1, 4242));
            Assert.True(layout.RemoveMacro(macro));
            Assert.False(layout.RemoveMacro(macro));
            Assert.Equal(0, layout.MacroCount);
        }

        [Fact]
        public void CreateMacro_OnTheAdvantage360_TakesTheDeviceSpeedAndRepeatDefaults()
        {
            var macro = KeyboardLayout.Create(DeviceId.Advantage360).CreateMacro();

            Assert.Equal(5, macro.Speed);
            Assert.Equal(1, macro.RepeatFrequency);
        }

        [Fact]
        public void Counters_AfterEdits_ReflectTheModel()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var topLayer = layout.Layers[0];
            var bottomLayer = layout.Layers[1];

            topLayer.Keys[0].Remap(ModelTokens.Key("a"));
            bottomLayer.Keys[0].Remap(ModelTokens.Key("b"));
            topLayer.Keys[1].SetTapAndHold(ModelTokens.Key("a"), ModelTokens.Key("lctrl"), 250);

            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a")));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b"), MacroModifiers.LeftShift));
            topLayer.Keys[2].SetMacro(1, macro);

            Assert.Equal(2, layout.ModifiedKeyCount);
            Assert.Equal(1, layout.TapAndHoldCount);
            Assert.Equal(1, layout.MacroCount);
            Assert.Equal(4, layout.TotalKeystrokes);
            Assert.Equal(new[] { macro }, layout.EnumerateMacros());
        }

        [Fact]
        public void Reset_OnALayout_ClearsEveryLayerAndTheFlatMacroList()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            layout.Layers[0].Keys[0].Remap(ModelTokens.Key("a", TokenDialect.Gen2));
            layout.Layers[2].Keys[1].SetTapAndHold(
                ModelTokens.Key("a", TokenDialect.Gen2),
                ModelTokens.Key("lctr", TokenDialect.Gen2),
                150);
            layout.AddMacro(CreateMacro(layout, layerIndex: 0, triggerKey: 4242));

            layout.Reset();

            Assert.Equal(0, layout.ModifiedKeyCount);
            Assert.Equal(0, layout.TapAndHoldCount);
            Assert.Equal(0, layout.MacroCount);
            Assert.Empty(layout.Macros);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.Advantage360)]
        public void Validate_OnAFreshLayout_ReportsNothing(DeviceId deviceId)
        {
            Assert.Empty(KeyboardLayout.Create(deviceId).Validate());
        }

        [Fact]
        public void Validate_WithMoreMacrosThanTheDeviceAllows_ReportsItAndKeepsThem()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            for (var i = 0; i < 101; i++)
            {
                layout.AddMacro(CreateMacro(layout, layerIndex: 0, triggerKey: 4000 + i));
            }

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroCountExceeded);

            Assert.Equal(100, violation.Limit);
            Assert.Equal(101, violation.ActualValue);
            Assert.Equal(101, layout.MacroCount);
        }

        [Fact]
        public void Validate_ForAnAdvantage360Macro_MeasuresTheKeystrokeTextAgainstTheFiveHundredCap()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242, keystrokes: 200);

            layout.AddMacro(macro);

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroLengthExceeded);

            // 06 §6 caps "the length of the serialized *macro* text": 200 × "{a}" = 600 characters.
            Assert.Equal(500, violation.Limit);
            Assert.Equal(600, violation.ActualValue);
            Assert.Equal(200, macro.Keystrokes.Count);
        }

        [Fact]
        public void Validate_ForAnAdvantage360Macro_ExcludesTheTriggerAndMarkersFromTheCap()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242, keystrokes: 166);

            layout.AddMacro(macro);

            // 166 × "{a}" = 498 characters of keystroke text; the whole line is longer than 500, so
            // measuring the line instead of the macro would report a breach that does not exist.
            Assert.Equal(498, MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen2));
            Assert.True(MacroKeystrokeRenderer.Render(macro, TokenDialect.Gen2).Length > 500);
            Assert.DoesNotContain(layout.Validate(), violation => violation.Kind == ModelViolationKind.MacroLengthExceeded);
        }

        [Fact]
        public void Validate_ForAGen1Macro_MeasuresWeightedKeystrokesNotSerializedText()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242, keystrokes: 150);

            layout.Layers[0].Keys[0].SetMacro(1, macro);

            // 150 × "{a}" is 450 serialized characters, but only 150 keystrokes — 06 §6 caps the
            // Gen1 families in keystrokes, so this macro is well inside the limit.
            Assert.Equal(450, MacroKeystrokeRenderer.KeystrokeTextLength(macro, TokenDialect.Gen1));
            Assert.DoesNotContain(layout.Validate(), violation => violation.Kind == ModelViolationKind.MacroLengthExceeded);

            macro.AddKeystrokes(Enumerable.Range(0, 151).Select(_ => new Keystroke(ModelTokens.Key("a"))));

            var reported = SingleViolation(layout.Validate(), ModelViolationKind.MacroLengthExceeded);

            Assert.Equal(300, reported.Limit);
            Assert.Equal(301, reported.ActualValue);
        }

        [Fact]
        public void Validate_ForAGen1MacroWithModifiers_CountsThemLikeTheLayoutBudgetDoes()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            // 04 §5.3 defines keystroke accounting once for the whole document — 1 per keystroke
            // plus 2 per attached modifier — so the 300 cap and the 7200 budget use the same count.
            macro.AddKeystrokes(Enumerable
                .Range(0, 101)
                .Select(_ => new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift)));

            layout.Layers[0].Keys[0].SetMacro(1, macro);

            var reported = SingleViolation(layout.Validate(), ModelViolationKind.MacroLengthExceeded);

            Assert.Equal(101, macro.Keystrokes.Count);
            Assert.Equal(303, macro.WeightedKeystrokeCount);
            Assert.Equal(300, reported.Limit);
            Assert.Equal(303, reported.ActualValue);
        }

        [Fact]
        public void MacroCountAndTotalKeystrokes_ForSlotMacrosOnTheAdvantage360_IncludeThem()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var key = layout.Layers[0].Keys[0];

            // 04 §4.2 appends every Gen2 macro to the flat list, so slots stay empty on a
            // well-formed model — but if a tolerant load puts one there, the counters and the
            // validator must not disagree about whether it exists.
            key.SetMacro(1, CreateMacro(layout, layerIndex: 0, triggerKey: 4242, keystrokes: 3));

            Assert.Equal(1, layout.MacroCount);
            Assert.Equal(3, layout.TotalKeystrokes);
            Assert.Equal(new[] { key.Macro1 }, layout.EnumerateMacros());
        }

        [Fact]
        public void Validate_ForManySlotMacrosOnTheAdvantage360_ReportsTheCountAndBudgetBreaches()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            for (var index = 0; index < 41; index++)
            {
                for (var slot = 1; slot <= KeyboardKey.MacroSlotCount; slot++)
                {
                    layout.Layers[0].Keys[index].SetMacro(
                        slot,
                        CreateMacro(layout, layerIndex: 0, triggerKey: 4000 + (index * 10) + slot, keystrokes: 40));
                }
            }

            var violations = layout.Validate();

            Assert.Equal(205, layout.MacroCount);
            Assert.Equal(8200, layout.TotalKeystrokes);
            Assert.Equal(100, SingleViolation(violations, ModelViolationKind.MacroCountExceeded).Limit);
            Assert.Equal(205, SingleViolation(violations, ModelViolationKind.MacroCountExceeded).ActualValue);
            Assert.Equal(8200, SingleViolation(violations, ModelViolationKind.MacroKeystrokeBudgetExceeded).ActualValue);
        }

        [Fact]
        public void Validate_WhenTheKeystrokeBudgetIsExceeded_ReportsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            for (var i = 0; i < 20; i++)
            {
                var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4000 + i);

                macro.ClearKeystrokes();
                macro.AddKeystrokes(Enumerable
                    .Range(0, 121)
                    .Select(_ => new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2), MacroModifiers.LeftShift)));

                layout.AddMacro(macro);
            }

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroKeystrokeBudgetExceeded);

            Assert.Equal(7200, violation.Limit);
            Assert.Equal(7260, violation.ActualValue);
        }

        [Fact]
        public void Validate_WithMoreCoTriggersThanTheDeviceAllows_ReportsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage2);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Legacy)));

            for (var i = 0; i < 4; i++)
            {
                macro.AddCoTrigger(ModelTokens.Key("lshift", TokenDialect.Legacy));
            }

            layout.Layers[0].Keys[30].SetMacro(1, macro);

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroCoTriggerLimitExceeded);

            Assert.Equal(3, violation.Limit);
            Assert.Equal(4, violation.ActualValue);
        }

        [Fact]
        public void Validate_ForTwoMacrosWithTheSameCoTriggerSet_ReportsTheCollision()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[0];
            var first = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);
            var second = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            // 06 §5: the sets are compared, not the slot order — the same two co-triggers added the
            // other way round is still the same trigger combination.
            first.AddCoTrigger(ModelTokens.Key("lshft"));
            first.AddCoTrigger(ModelTokens.Key("lctrl"));
            second.AddCoTrigger(ModelTokens.Key("lctrl"));
            second.AddCoTrigger(ModelTokens.Key("lshft"));

            key.SetMacro(1, first);
            key.SetMacro(2, second);

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroTriggerCollision);

            Assert.Equal(0, violation.LayerIndex);
            Assert.Equal(0, violation.KeyIndex);
            Assert.Equal(2, violation.MacroIndex);
        }

        [Fact]
        public void Validate_ForTwoMacrosWithDifferentCoTriggerSets_ReportsNoCollision()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[0];
            var first = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);
            var second = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            first.AddCoTrigger(ModelTokens.Key("lshft"));
            second.AddCoTrigger(ModelTokens.Key("lctrl"));

            key.SetMacro(1, first);
            key.SetMacro(2, second);

            Assert.DoesNotContain(
                layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MacroTriggerCollision);
        }

        [Fact]
        public void Validate_ForACollisionAfterTheMacroIndexWasOverwritten_NamesTheSlotItFoundThemIn()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[0];
            var second = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            key.SetMacro(1, CreateMacro(layout, layerIndex: 0, triggerKey: 4242));
            key.SetMacro(2, second);

            // Macro.MacroIndex has a public setter, so it may name a slot this key does not hold;
            // the report has to describe where the macro actually sits.
            second.MacroIndex = 5;

            Assert.Equal(2, SingleViolation(layout.Validate(), ModelViolationKind.MacroTriggerCollision).MacroIndex);
        }

        [Fact]
        public void Validate_ForUnboundGen2Macros_ReportsThemWithoutInventingCollisions()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            for (var i = 0; i < 3; i++)
            {
                var macro = layout.CreateMacro();

                macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));
                layout.AddMacro(macro);
            }

            var violations = layout.Validate();

            // 06 §5 detects duplicates per trigger key + layer, so macros that name neither are not
            // on a trigger at all and cannot collide; 06 §5 does require "a layer is selected".
            Assert.DoesNotContain(violations, violation => violation.Kind == ModelViolationKind.MacroTriggerCollision);
            Assert.Equal(3, violations.Count(violation => violation.Kind == ModelViolationKind.UnboundMacro));
        }

        [Fact]
        public void AssignMacro_OnAnAdvantage360Position_RefusesTheSlotAndLeavesTheMacroIndexAlone()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            layout.AddMacro(macro);

            Assert.Equal(0, layout.Layers[0].Keys[10].AssignMacro(macro));
            Assert.Equal(0, macro.MacroIndex);
            Assert.Equal(1, layout.MacroCount);
        }

        [Fact]
        public void EnumerateMacros_ForOneInstanceInBothStores_YieldsItOnce()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            layout.AddMacro(macro);
            layout.Layers[0].Keys[10].SetMacro(1, macro);

            // One macro parked in both stores is still one macro; counting it twice would push a
            // layout over the 04 §5.3 caps it does not breach.
            Assert.Equal(new[] { macro }, layout.EnumerateMacros());
            Assert.Equal(1, layout.MacroCount);
            Assert.Equal(1, layout.TotalKeystrokes);
        }

        [Fact]
        public void Validate_ForOneInstanceInBothStores_ReportsEachBreachOnce()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            macro.Speed = 0;
            layout.AddMacro(macro);
            layout.Layers[0].Keys[10].SetMacro(1, macro);

            SingleViolation(layout.Validate(), ModelViolationKind.MacroSpeedOutOfRange);
        }

        [Fact]
        public void Validate_ForAMacroInAKeySlotOnTheAdvantage360_ReportsItAndKeepsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var key = layout.Layers[0].Keys[10];

            key.SetMacro(1, CreateMacro(layout, layerIndex: 0, triggerKey: 4242));

            // 04 §4.3 writes per-key macro lines on non-Gen2 devices only, so a macro left in a slot
            // here would vanish on the next save.
            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroOutsideFlatList);

            Assert.Equal(0, violation.LayerIndex);
            Assert.Equal(10, violation.KeyIndex);
            Assert.True(key.IsMacro);
        }

        [Fact]
        public void Validate_ForAMacroInAKeySlotOnASlotBasedDevice_ReportsNothing()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);

            layout.Layers[0].Keys[10].SetMacro(1, CreateMacro(layout, layerIndex: 0, triggerKey: 4242));

            Assert.DoesNotContain(
                layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MacroOutsideFlatList);
        }

        [Fact]
        public void Validate_ForAPositionHoldingTwoSingleKeyRules_ReportsTheConflict()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var key = layout.Layers[0].Keys[10];

            key.ApplyTapAndHold(
                ModelTokens.Key("a", TokenDialect.Gen2),
                ModelTokens.Key("lctr", TokenDialect.Gen2),
                150);
            key.ApplyRemap(ModelTokens.Key("z", TokenDialect.Gen2));

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.ConflictingSingleKeyRules);

            Assert.Equal(0, violation.LayerIndex);
            Assert.Equal(10, violation.KeyIndex);
        }

        [Fact]
        public void Validate_AfterCopyingKeyDataOntoAStaleMultiModifier_ReportsNoNewConflict()
        {
            var layout = KeyboardLayout.Create(DeviceId.Tko);
            var source = layout.Layers[0].Keys[5];
            var target = layout.Layers[0].Keys[6];

            source.Remap(ModelTokens.Key("a"));
            target.ApplyMultiModifiers("caws");
            target.CopyFrom(source, KeyCopyScopes.KeyData);

            // CopyFrom is an editor path: it may carry a conflict the source already had, but it
            // must never manufacture one out of a source that held a single rule.
            Assert.DoesNotContain(
                layout.Validate(),
                violation => violation.Kind == ModelViolationKind.ConflictingSingleKeyRules);
            Assert.DoesNotContain(
                layout.Validate(),
                violation => violation.Kind == ModelViolationKind.MultiModifiersNotSupported);
        }

        [Fact]
        public void Validate_ForAPositionHoldingOneSingleKeyRule_ReportsNoConflict()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            layout.Layers[0].Keys[10].Remap(ModelTokens.Key("z", TokenDialect.Gen2));

            Assert.Empty(layout.Validate());
        }

        [Fact]
        public void Validate_ForAnUnboundGen2Macro_ReportsNoLayerOnAnyOfItsViolations()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            layout.AddMacro(layout.CreateMacro());

            // -1 is the unassigned sentinel, not a layer: no report may name layer -1.
            Assert.All(layout.Validate(), violation => Assert.Null(violation.LayerIndex));
        }

        [Fact]
        public void Validate_ForFlatMacroGroups_ReportsThemInFlatListOrder()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var layersInFirstAppearanceOrder = new[] { 3, 1, 4, 0, 2 };

            // One colliding pair per layer, appended in an order that is neither sorted nor grouped
            // by anything a hash container would choose — so the reported order pins the contract
            // "flat-list order", not whatever a Dictionary happens to enumerate.
            foreach (var layerIndex in layersInFirstAppearanceOrder)
            {
                layout.AddMacro(CreateMacro(layout, layerIndex, triggerKey: 4000 + layerIndex));
            }

            foreach (var layerIndex in layersInFirstAppearanceOrder)
            {
                layout.AddMacro(CreateMacro(layout, layerIndex, triggerKey: 4000 + layerIndex));
            }

            var violations = layout.Validate();

            Assert.Equal(
                layersInFirstAppearanceOrder.Select(layerIndex => (int?)layerIndex),
                violations
                    .Where(violation => violation.Kind == ModelViolationKind.MacroTriggerCollision)
                    .Select(violation => violation.LayerIndex));
            Assert.Equal(Describe(violations), Describe(layout.Validate()));
        }

        [Fact]
        public void Validate_ForABoundGen2Macro_ReportsNoUnboundViolation()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            layout.AddMacro(CreateMacro(layout, layerIndex: 0, triggerKey: 4242));

            Assert.DoesNotContain(layout.Validate(), violation => violation.Kind == ModelViolationKind.UnboundMacro);
        }

        [Fact]
        public void Validate_ForAnEditOnALockedPosition_ReportsItAndKeepsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Tko);
            var key = layout.Layers[0].Keys[61];

            Assert.False(key.CanEdit);

            key.ApplyRemap(ModelTokens.Key("a"));

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.EditOnLockedKey);

            Assert.Equal(0, violation.LayerIndex);
            Assert.Equal(61, violation.KeyIndex);
            Assert.True(key.IsModified);
        }

        [Fact]
        public void Validate_ForAMultiModifierOnANonAdvantage360_ReportsItAndKeepsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Tko);
            var key = layout.Layers[0].Keys[0];

            Assert.False(layout.Device.SupportsMultiModifiers);
            Assert.True(key.ApplyMultiModifiers("caws"));

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MultiModifiersNotSupported);

            Assert.Equal(0, violation.KeyIndex);
            Assert.Equal("caws", key.MultiModifiers);
        }

        [Fact]
        public void Validate_ForAMultiModifierOnTheAdvantage360_ReportsNothing()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            Assert.True(layout.Layers[0].Keys[0].TrySetMultiModifiers("caws"));
            Assert.Empty(layout.Validate());
        }

        [Fact]
        public void Validate_ForAMacroOnARestrictedPosition_ReportsItAndKeepsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdge);
            var key = layout.Layers[0].Keys[69];

            Assert.False(key.CanAssignMacro);

            key.SetMacro(1, CreateMacro(layout, layerIndex: 0, triggerKey: 4242));

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.MacroOnRestrictedKey);

            Assert.Equal(69, violation.KeyIndex);
            Assert.True(key.IsMacro);
        }

        [Theory]
        [InlineData(0, ModelViolationKind.MacroSpeedOutOfRange)]
        [InlineData(10, ModelViolationKind.MacroSpeedOutOfRange)]
        public void Validate_ForASpeedOutsideTheDeviceRange_ReportsIt(int speed, ModelViolationKind expected)
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            macro.Speed = speed;
            layout.AddMacro(macro);

            var violation = SingleViolation(layout.Validate(), expected);

            Assert.Equal(speed, violation.ActualValue);
            Assert.Equal(speed, macro.Speed);
        }

        [Fact]
        public void Validate_ForARepeatOutsideTheDeviceRange_ReportsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: 4242);

            macro.RepeatFrequency = 0;
            layout.AddMacro(macro);

            Assert.Equal(0, SingleViolation(layout.Validate(), ModelViolationKind.MacroRepeatOutOfRange).ActualValue);
        }

        [Fact]
        public void Validate_ForAnEmptyGen2Macro_ReportsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);

            layout.AddMacro(layout.CreateMacro());

            SingleViolation(layout.Validate(), ModelViolationKind.EmptyMacro);
        }

        [Fact]
        public void Validate_ForAHangingKeyDown_ReportsIt()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("d", TokenDialect.Gen2)) { UpDown = KeyDirection.Down });
            layout.AddMacro(macro);

            SingleViolation(layout.Validate(), ModelViolationKind.UnbalancedMacroKeyDirection);
        }

        [Theory]
        [InlineData(KeyboardKey.Fn1ShiftKeyCode)]
        [InlineData(KeyboardKey.KeypadToggleKeyCode)]
        public void Validate_ForAReservedGen2TriggerWithoutCoTriggers_ReportsIt(int triggerKey)
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = CreateMacro(layout, layerIndex: 0, triggerKey: triggerKey);

            layout.AddMacro(macro);

            SingleViolation(layout.Validate(), ModelViolationKind.ReservedMacroTriggerWithoutCoTrigger);

            macro.AddCoTrigger(ModelTokens.Key("lshf", TokenDialect.Gen2));

            Assert.DoesNotContain(
                layout.Validate(),
                violation => violation.Kind == ModelViolationKind.ReservedMacroTriggerWithoutCoTrigger);
        }

        [Fact]
        public void Validate_WithMoreTapAndHoldKeysThanAllowed_ReportsItAndKeepsThem()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var capability = layout.Device.TapAndHold;
            var layer = layout.Layers[0];

            for (var index = 20; index < 31; index++)
            {
                layer.Keys[index].SetTapAndHold(
                    ModelTokens.Key("a", TokenDialect.Gen2),
                    ModelTokens.Key("lctr", TokenDialect.Gen2),
                    capability);
            }

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.TapAndHoldCountExceeded);

            Assert.Equal(10, violation.Limit);
            Assert.Equal(11, violation.ActualValue);
            Assert.Equal(11, layout.TapAndHoldCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public void Validate_ForATapAndHoldDelayOutsideTheBounds_ReportsItAndKeepsIt(int delay)
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var key = layout.Layers[0].Keys[20];

            key.SetTapAndHold(
                ModelTokens.Key("a", TokenDialect.Gen2),
                ModelTokens.Key("lctr", TokenDialect.Gen2),
                delay);

            var violation = SingleViolation(layout.Validate(), ModelViolationKind.TapAndHoldDelayOutOfRange);

            Assert.Equal(delay, violation.ActualValue);
            Assert.Equal(delay, key.TimingDelay);
        }

        [Fact]
        public void Validate_ForATapAndHoldOnADeviceWithoutTheFeature_ReportsIt()
        {
            var device = DeviceCatalog.GetById(DeviceId.Advantage360) with { TapAndHold = TapAndHoldCapability.None };
            var layout = new KeyboardLayout(device, GeometryCatalog.Advantage360);

            layout.Layers[0].Keys[20].SetTapAndHold(
                ModelTokens.Key("a", TokenDialect.Gen2),
                ModelTokens.Key("lctr", TokenDialect.Gen2),
                150);

            SingleViolation(layout.Validate(), ModelViolationKind.TapAndHoldNotSupported);
        }

        private static Macro CreateMacro(KeyboardLayout layout, int layerIndex, int triggerKey, int keystrokes = 1)
        {
            var macro = layout.CreateMacro();
            var key = ModelTokens.Key("a", layout.Dialect);

            macro.LayerIndex = layerIndex;
            macro.TriggerKey = triggerKey;
            macro.AddKeystrokes(Enumerable.Range(0, keystrokes).Select(_ => new Keystroke(key)));

            return macro;
        }

        private static ModelViolation SingleViolation(IReadOnlyList<ModelViolation> violations, ModelViolationKind kind)
        {
            return Assert.Single(violations, violation => violation.Kind == kind);
        }

        private static string Describe(IReadOnlyList<ModelViolation> violations)
        {
            return string.Join(
                "|",
                violations.Select(violation => $"{violation.Kind}:{violation.LayerIndex}:{violation.KeyIndex}:{violation.MacroIndex}"));
        }
    }
}
