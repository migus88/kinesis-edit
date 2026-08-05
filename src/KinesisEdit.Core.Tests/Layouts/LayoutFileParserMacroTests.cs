using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Macro line parsing per specs/06-macros.md §2 and the worked examples of §7: trigger and
    /// co-trigger collection (§2.1), the value-side token semantics with active-modifier
    /// tracking (§2.2), the first-token-only speed/repeat rules with their per-dialect
    /// out-of-range policies (§2.2, §4), the per-dialect <c>d125</c>/<c>d500</c> resolution
    /// (§2.2; 05 §3.12), first-empty-slot filling versus the Gen2 flat list
    /// (04 §4.2 step 5), and unknown-token segment invalidation.
    /// </summary>
    public class LayoutFileParserMacroTests
    {
        [Fact]
        public void Parse_TheSection71Example_BuildsTheNadiaMacro()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdge,
                "{q}>{x1}{-lshft}{n}{+lshft}{a}{d}{i}{a}");

            var key = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1);
            var macro = key.Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.NotNull(macro);
            Assert.Equal(0, macro.CoTriggerCount);
            Assert.Equal(1, macro.RepeatFrequency);
            Assert.Equal(0, macro.Speed);
            Assert.Equal(5, macro.Keystrokes.Count);
            Assert.Equal(LayoutFixtures.Key("n").Code, macro.Keystrokes[0].Key.Code);
            Assert.Equal(MacroModifiers.LeftShift, macro.Keystrokes[0].Modifiers);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[1].Modifiers);
        }

        [Fact]
        public void Parse_TheSection72Example_CollectsTheCoTrigger()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdgeRgb,
                "{lshft}{esc}>{s2}{x2}{a}{s}{d}{f}{kpent}");

            var key = LayoutFixtures.Position(result.Layout, 0, "esc", TokenDialect.Gen1);
            var macro = key.Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.NotNull(macro);
            Assert.Equal(1, macro.CoTriggerCount);
            Assert.Equal(LayoutFixtures.Key("lshft").Code, macro.CoTriggers[0].Code);
            Assert.Equal(2, macro.Speed);
            Assert.Equal(2, macro.RepeatFrequency);
        }

        [Fact]
        public void Parse_WithoutSpeedAndRepeatTokens_AppliesTheDeviceDefaults()
        {
            // 06 §4: defaults apply when the tokens are absent — 5/1 on the RGB family.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{q}>{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Equal(5, macro!.Speed);
            Assert.Equal(1, macro.RepeatFrequency);
        }

        [Fact]
        public void Parse_SpeedZeroOnTheRgbFamily_IsAccepted()
        {
            // 06 §2.2: the RGB family accepts 0-9; 0 means "use global speed".
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{q}>{s0}{x1}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(0, macro!.Speed);
        }

        [Fact]
        public void Parse_AnOutOfRangeSpeedOnTheRgbFamily_IsIgnored()
        {
            // 06 §2.2: out of range is simply ignored — the default (5) applies.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{q}>{s12}{x1}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(5, macro!.Speed);
        }

        [Fact]
        public void Parse_AnOutOfRangeSpeedOnFreestyle_SubstitutesTheDefault()
        {
            // 06 §2.2: the old FS parser substitutes the FS default (0).
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{q}>{s12}{x1}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(0, macro!.Speed);
        }

        [Theory]
        [InlineData("{s0}{x1}", 1, 1)]
        [InlineData("{s1}{x0}", 1, 1)]
        [InlineData("{s12}{x2}", 9, 2)]
        public void Parse_SpeedAndRepeatOnGen2_AreClampedIntoRange(string tokens, int expectedSpeed, int expectedRepeat)
        {
            // 06 §2.2, §4: the Advantage360 clamps out-of-range values into its 1-9 range.
            var result = LayoutFixtures.Parse(DeviceId.Advantage360, $"{{caps}}>{tokens}{{spc}}");

            var macro = Assert.Single(result.Layout.Macros);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(expectedSpeed, macro.Speed);
            Assert.Equal(expectedRepeat, macro.RepeatFrequency);
        }

        [Fact]
        public void Parse_TheAdvantage2SpeedSyntax_SetsTheSpeed()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "{d}>{speed5}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "d", TokenDialect.Legacy).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(5, macro!.Speed);
        }

        [Fact]
        public void Parse_AnOutOfRangeAdvantage2Speed_SubstitutesTheDefault()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "{d}>{speed12}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "d", TokenDialect.Legacy).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(0, macro!.Speed);
        }

        [Fact]
        public void Parse_ASpeedTokenAfterAKeystroke_IsAKeystrokeOfThePseudoKey()
        {
            // 06 §2.2: speed/repeat markers are honored only as the first tokens; later on,
            // {s2} resolves like any registry token — the s2 pseudo-key of 05 §3.12.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{q}>{a}{s2}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Equal(0, macro!.Speed);
            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(11193, macro.Keystrokes[1].Key.Code);
        }

        [Fact]
        public void Parse_LegacyDelaysOnFreestyle_ResolveToTheDistinctLegacyKeys()
        {
            // 06 §7.5, 05 §3.12: on FS, d125/d500 are the legacy 125/500 ms delay keys.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{o}>{x1}{d125}{d500}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "o", TokenDialect.Gen1).Macro1;

            Assert.Equal(10007, macro!.Keystrokes[0].Key.Code);
            Assert.Equal(10008, macro.Keystrokes[1].Key.Code);
        }

        [Fact]
        public void Parse_LegacyDelaysOnTheRgbFamily_ResolveToTheGeneratedRange()
        {
            // 06 §2.2: on RGB/TKO, d125/d500 resolve to the generated dNNN keys (10085 + N).
            var result = LayoutFixtures.Parse(DeviceId.Tko, "{q}>{s5}{x1}{d125}{d500}{d050}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Equal(10085 + 125, macro!.Keystrokes[0].Key.Code);
            Assert.Equal(10085 + 500, macro.Keystrokes[1].Key.Code);
            Assert.Equal(10085 + 50, macro.Keystrokes[2].Key.Code);
        }

        [Fact]
        public void Parse_MacroLinesOnTheSameTrigger_FillTheFirstEmptySlots()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.FreestyleEdgeRgb,
                "{3}>{s5}{x1}{a}",
                "{lshft}{3}>{s5}{x1}{b}",
                "{rctrl}{3}>{s5}{x1}{c}");

            var key = LayoutFixtures.Position(result.Layout, 0, "3", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(3, key.MacroCount);
            Assert.Equal(0, key.Macro1!.CoTriggerCount);
            Assert.Equal(1, key.Macro2!.CoTriggerCount);
            Assert.Equal(LayoutFixtures.Key("rctrl").Code, key.Macro3!.CoTriggers[0].Code);
        }

        [Fact]
        public void Parse_ASixthMacroLineOnOneTrigger_IsTrackedNotSilentlyDropped()
        {
            var lines = new string[6];

            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] = "{q}>{s5}{x1}{a}";
            }

            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, lines);

            var key = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1);
            var tracked = Assert.Single(result.InvalidLines);

            // 04 §4.2 step 5: at most 5 macro lines per trigger key are retained; the sixth is
            // grammatically fine — every segment is valid — but could not be applied.
            Assert.Equal(5, key.MacroCount);
            Assert.Equal(6, tracked.LineNumber);
            Assert.All(tracked.Segments, segment => Assert.True(segment.IsValid));
        }

        [Fact]
        public void Parse_AllCoTriggersAreStoredOnLoad_TruncationHappensOnlyOnSave()
        {
            // The old FS parser kept a single co-trigger (06 §2.1); this parser stores what the
            // file carries and lets the serializer apply the persisted count.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{lshft}{lctrl}{lalt}{q}>{x1}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.Equal(3, macro!.CoTriggerCount);
        }

        [Fact]
        public void Parse_ANonModifierCoTrigger_MarksThatSegmentInvalid()
        {
            // 06 §2.1: only modifiers may precede the trigger.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{a}{q}>{s5}{x1}{b}");

            var line = Assert.Single(result.InvalidLines);

            Assert.Contains(line.Segments, segment => segment is { Text: "{a}", IsValid: false });
            Assert.Contains(line.Segments, segment => segment is { Text: "{q}", IsValid: true });
        }

        [Fact]
        public void Parse_AGen2Macro_JoinsTheFlatListWithTheHeadersLayer()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.Advantage360,
                "<function1>",
                "{caps}>{s5}{x1}{spc}");

            var macro = Assert.Single(result.Layout.Macros);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(2, macro.LayerIndex);
            Assert.Equal(LayoutFixtures.Key("caps", TokenDialect.Gen2).Code, macro.TriggerKey);
            Assert.Equal(0, LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen2).MacroCount);
        }

        [Fact]
        public void Parse_AGen2ExplicitDownUpPair_KeepsTheDirections()
        {
            // 06 §2.2: {-d}/{+d} on a non-modifier are explicit key-down/key-up events.
            var result = LayoutFixtures.Parse(DeviceId.Advantage360, "{caps}>{s5}{x1}{-d}{spc}{+d}");

            var macro = Assert.Single(result.Layout.Macros);

            Assert.Equal(KeyDirection.Down, macro.Keystrokes[0].UpDown);
            Assert.Equal(KeyDirection.None, macro.Keystrokes[1].UpDown);
            Assert.Equal(KeyDirection.Up, macro.Keystrokes[2].UpDown);
            Assert.True(macro.HasBalancedKeyDirections());
        }

        [Fact]
        public void Parse_ABareModifierToken_IsASingleTap()
        {
            // 06 §2.2: a modifier with no +/- is a single tap of that modifier.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{q}>{x1}{lwin}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;
            var keystroke = Assert.Single(macro!.Keystrokes);

            Assert.Equal(LayoutFixtures.Key("lwin").Code, keystroke.Key.Code);
            Assert.Equal(KeyDirection.None, keystroke.UpDown);
        }

        [Fact]
        public void Parse_AModifierUpWithoutADown_IsKeptAsASingleTap()
        {
            // 06 §2.2: an up with no matching down is kept as a single tap in the macro.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{q}>{x1}{+lshft}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Equal(2, macro!.Keystrokes.Count);
            Assert.Equal(LayoutFixtures.Key("lshft").Code, macro.Keystrokes[0].Key.Code);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[1].Modifiers);
        }

        [Fact]
        public void Parse_ADownImmediatelyFollowedByItsUp_IsKeptAsASingleTap()
        {
            // 06 §2.2: an up arriving immediately after the same key's down is a single tap.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "{q}>{x1}{-lshft}{+lshft}{a}");

            var macro = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1).Macro1;

            Assert.Equal(2, macro!.Keystrokes.Count);
            Assert.Equal(LayoutFixtures.Key("lshft").Code, macro.Keystrokes[0].Key.Code);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[1].Modifiers);
        }

        [Fact]
        public void Parse_AnAdvantage2KeypadMacro_RoutesTriggerAndCoTriggerToTheKeypadLayer()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "{kp-lshift}{kp0}>{speed5}{a}");

            var key = LayoutFixtures.Position(result.Layout, 1, "kp0", TokenDialect.Legacy);
            var macro = key.Macro1;

            Assert.Empty(result.InvalidLines);
            Assert.NotNull(macro);
            Assert.Equal(1, macro.LayerIndex);
            Assert.Equal(LayoutFixtures.Key("lshift", TokenDialect.Legacy).Code, macro.CoTriggers[0].Code);
        }

        [Fact]
        public void Parse_AnUnknownValueToken_InvalidatesOnlyThatSegmentAndTheLineIsNotApplied()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "{q}>{s5}{x1}{a}{zzz}{b}");

            var key = LayoutFixtures.Position(result.Layout, 0, "q", TokenDialect.Gen1);
            var line = Assert.Single(result.InvalidLines);

            Assert.Equal(0, key.MacroCount);
            Assert.Contains(line.Segments, segment => segment is { Text: "{zzz}", IsValid: false });
            Assert.Contains(line.Segments, segment => segment is { Text: "{a}", IsValid: true });
            Assert.Contains(line.Segments, segment => segment is { Text: "{b}", IsValid: true });
        }

        [Fact]
        public void Parse_ABareModifierAsTheFinalToken_IsItselfTheTrigger()
        {
            // 06 §2.1: a modifier that is the final token is the trigger — Adv360 modifiers can
            // host macros (05 §5.3).
            var result = LayoutFixtures.Parse(DeviceId.Advantage360, "<base>", "{lshf}>{s5}{x1}{spc}");

            var macro = Assert.Single(result.Layout.Macros);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(LayoutFixtures.Key("lshf", TokenDialect.Gen2).Code, macro.TriggerKey);
        }
    }
}
