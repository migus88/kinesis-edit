using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Save semantics per specs/04-layout-file-format.md §4.3 and the serializer variants of
    /// specs/06-macros.md §3: full regeneration in layer/key-index order, one single-key line
    /// per position (tap-and-hold else multi-modifier else remap), per-dialect persisted macro
    /// slots and co-trigger counts, the speed/repeat token variants, and the kept-invalid-line
    /// append of §4.3 step 4 / §5.2.
    /// </summary>
    public class LayoutFileSerializerTests
    {
        [Fact]
        public void Serialize_AFactoryStateFreestyleLayout_WritesNothing()
        {
            var layout = LayoutFixtures.CreateLayout(DeviceId.FreestyleEdge);

            Assert.Empty(LayoutFileSerializer.Serialize(layout));
        }

        [Fact]
        public void Serialize_AFactoryStateGen2Layout_WritesTheFiveHeaders()
        {
            var layout = LayoutFixtures.CreateLayout(DeviceId.Advantage360);

            Assert.Equal(
                ["<base>", "<keypad>", "<function1>", "<function2>", "<function3>"],
                LayoutFileSerializer.Serialize(layout));
        }

        [Fact]
        public void Serialize_WritesLayersInOrderAndKeysInPhysicalIndexOrder()
        {
            // Fixture lines arrive shuffled; 04 §4.3 orders output by layer then key index —
            // on the FS top layer F1 is index 1, hyph 30, caps 53.
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdge,
                "fn [4]>[LED]",
                "[caps]>[lwin]",
                "[F1]>[a]",
                "[hyph]>[obrk]");

            Assert.Equal(
                ["[F1]>[a]", "[hyph]>[obrk]", "[caps]>[lwin]", "fn [4]>[LED]"],
                LayoutFileSerializer.Serialize(result.Layout));
        }

        [Fact]
        public void Serialize_WritesCanonicalCasing()
        {
            // 04 §1.2: parsing lowercases, saving emits the registry's canonical casing.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "fn [4]>[led]", "[f1]>[F13]");

            Assert.Equal(["[F1]>[F13]", "fn [4]>[LED]"], LayoutFileSerializer.Serialize(result.Layout));
        }

        [Fact]
        public void Serialize_TapAndHoldWinsOverMultiModifierAndRemap()
        {
            // 04 §4.3: tap-and-hold line, else multi-modifier, else remap — a tolerantly loaded
            // key holding several rules writes only the highest-precedence one.
            var layout = LayoutFixtures.CreateLayout(DeviceId.FreestyleEdgeRgb);
            var key = LayoutFixtures.Position(layout, 0, "caps", TokenDialect.Gen1);

            key.ApplyRemap(LayoutFixtures.Key("lwin"));
            key.ApplyMultiModifiers("caws");
            key.ApplyTapAndHold(LayoutFixtures.Key("a"), LayoutFixtures.Key("lctrl"), 250);

            Assert.Equal(["[caps]>[a][t&h250][lctrl]"], LayoutFileSerializer.Serialize(layout));
        }

        [Fact]
        public void Serialize_AMultiModifierOnAnOlderDialect_IsNotWritten()
        {
            // 04 §2.3: multi-modifier lines are written by the RGB-family/Gen2 serializer; the
            // legacy FS app does not emit them.
            var layout = LayoutFixtures.CreateLayout(DeviceId.FreestyleEdge);
            var key = LayoutFixtures.Position(layout, 0, "caps", TokenDialect.Gen1);

            key.ApplyMultiModifiers("caws");

            Assert.Empty(LayoutFileSerializer.Serialize(layout));
        }

        [Fact]
        public void Serialize_FreestyleWritesOnlyThreeMacroSlots()
        {
            // 06 §1: the FS and Adv2 dialects persist Macro1..Macro3; the model still holds 5.
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdge,
                "{q}>{x1}{a}",
                "{lshft}{q}>{x1}{b}",
                "{lctrl}{q}>{x1}{c}",
                "{lalt}{q}>{x1}{d}");

            Assert.Equal(4, LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).MacroCount);
            Assert.Equal(
                ["{q}>{x1}{a}", "{lshft}{q}>{x1}{b}", "{lctrl}{q}>{x1}{c}"],
                LayoutFileSerializer.Serialize(result.Layout));
        }

        [Fact]
        public void Serialize_TheRgbFamilyWritesAllFiveMacroSlots()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.Tko,
                "{q}>{s5}{x1}{a}",
                "{lshft}{q}>{s5}{x1}{b}",
                "{lctrl}{q}>{s5}{x1}{c}",
                "{lalt}{q}>{s5}{x1}{d}",
                "{lwin}{q}>{s5}{x1}{e}");

            Assert.Equal(5, LayoutFileSerializer.Serialize(result.Layout).Count);
        }

        [Fact]
        public void Serialize_FreestyleTruncatesCoTriggersToTheFirst()
        {
            // 06 §3: the old FS serializer writes only the first co-trigger; all three survive
            // in the model.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{lshft}{lctrl}{lalt}{q}>{x1}{a}");

            Assert.Equal(["{lshft}{q}>{x1}{a}"], LayoutFileSerializer.Serialize(result.Layout));
        }

        [Fact]
        public void Serialize_TheRgbFamilyAlwaysWritesSpeedAndRepeatIncludingZero()
        {
            // 06 §3: the RGB-family serializer always writes {sN}/{xN}, including {s0}.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{q}>{s0}{x1}{a}");

            Assert.Equal(["{q}>{s0}{x1}{a}"], LayoutFileSerializer.Serialize(result.Layout));
            Assert.True(result.Layout.Device.Macros.PersistsRepeat);
        }

        [Fact]
        public void Serialize_Advantage2WritesSpeedNAndNoRepeatToken()
        {
            // 06 §3: the Adv2 serializer writes {speedN} for 1-9 and no repeat token.
            var layout = LayoutFixtures.CreateLayout(DeviceId.Advantage2);
            var key = LayoutFixtures.Position(layout, 0, "d", TokenDialect.Legacy);
            var macro = layout.CreateMacro();

            macro.Speed = 5;
            macro.RepeatFrequency = 3;
            macro.AddKeystroke(new Keystroke(LayoutFixtures.Key("a", TokenDialect.Legacy)));
            key.SetMacro(1, macro);

            Assert.Equal(["{d}>{speed5}{a}"], LayoutFileSerializer.Serialize(layout));

            // The catalog datum the editor reads to hide a control whose value this drops must
            // agree with what actually happened here.
            Assert.False(layout.Device.Macros.PersistsRepeat);
        }

        [Fact]
        public void Serialize_AnEmptyMacroSlot_WritesNothing()
        {
            // 06 §3: only non-empty macros are emitted.
            var layout = LayoutFixtures.CreateLayout(DeviceId.FreestyleEdgeRgb);
            var key = LayoutFixtures.Position(layout, 0, "q", TokenDialect.Gen1);

            key.SetMacro(1, layout.CreateMacro());

            Assert.Empty(LayoutFileSerializer.Serialize(layout));
        }

        [Fact]
        public void Serialize_AKeptInvalidLine_IsAppendedVerbatimAfterItsLayer()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdgeRgb,
                "[F1]>[a]",
                "fn [ZZZ]>[a]",
                "fn [4]>[LED]");

            var tracked = Assert.Single(result.InvalidLines);

            tracked.Keep = true;

            // 04 §4.3 step 4: kept lines land after their layer's rules, exactly as loaded —
            // prefix and casing included.
            Assert.Equal(
                ["[F1]>[a]", "fn [4]>[LED]", "fn [ZZZ]>[a]"],
                LayoutFileSerializer.Serialize(result.Layout, result.InvalidLines));
        }

        [Fact]
        public void Serialize_AnUnkeptInvalidLine_IsDropped()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1]>[a]", "fn [ZZZ]>[a]");

            // The keep flag defaults to off — unchecked lines are discarded on save (04 §5.2).
            Assert.Equal(["[F1]>[a]"], LayoutFileSerializer.Serialize(result.Layout, result.InvalidLines));
        }

        [Fact]
        public void Serialize_WithANullLayout_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LayoutFileSerializer.Serialize(null!));
        }
    }
}
