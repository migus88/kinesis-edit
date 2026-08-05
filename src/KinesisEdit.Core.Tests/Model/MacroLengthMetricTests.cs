using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The two per-macro metrics of specs/06-macros.md §6 — the Advantage360's 500 "serialized
    /// macro text" characters against every other family's 300 weighted keystrokes — and the
    /// guarantee that whatever reads a macro's length gets the same answer
    /// <see cref="KeyboardLayout.Validate"/> gates the save with.
    /// </summary>
    public sealed class MacroLengthMetricTests
    {
        [Fact]
        public void Measure_OnASlotBasedLayout_IsTheWeightedKeystrokeCount()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift));

            // 04 §5.3: 1 per keystroke plus 2 per attached modifier.
            Assert.False(MacroLengthMetric.UsesSerializedTextLength(layout));
            Assert.Equal(MacroLengthMetric.WeightedKeystrokeUnit, MacroLengthMetric.UnitFor(layout));
            Assert.Equal(3, macro.WeightedKeystrokeCount);
            Assert.Equal(3, MacroLengthMetric.Measure(macro, layout));
        }

        [Fact]
        public void Measure_OnTheAdvantage360_IsTheSerializedKeystrokeTextLength()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = layout.CreateMacro();

            macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));
            macro.AddKeystroke(new Keystroke(ModelTokens.Key("b", TokenDialect.Gen2)));

            Assert.True(MacroLengthMetric.UsesSerializedTextLength(layout));
            Assert.Equal(MacroLengthMetric.SerializedTextUnit, MacroLengthMetric.UnitFor(layout));
            Assert.Equal("{a}{b}".Length, MacroLengthMetric.Measure(macro, layout));
            Assert.NotEqual(macro.WeightedKeystrokeCount, MacroLengthMetric.Measure(macro, layout));
        }

        /// <summary>
        /// The reason the metric is one shared helper: 04 §5.3's save gate reports the very number
        /// this returns, so a readout built on a different metric would contradict the violation
        /// that stops the write.
        /// </summary>
        [Fact]
        public void Measure_OnAnOverlongAdvantage360Macro_IsTheLengthValidateReports()
        {
            var layout = KeyboardLayout.Create(DeviceId.Advantage360);
            var macro = layout.CreateMacro();
            var limit = layout.Device.Macros.MaxCharactersPerMacro!.Value;

            macro.TriggerKey = ModelTokens.Key("a", TokenDialect.Gen2).Code;
            macro.LayerIndex = 0;

            for (var index = 0; index <= limit; index++)
            {
                macro.AddKeystroke(new Keystroke(ModelTokens.Key("a", TokenDialect.Gen2)));
            }

            layout.AddMacro(macro);

            var violation = layout.Validate()
                .Single(candidate => candidate.Kind == ModelViolationKind.MacroLengthExceeded);

            Assert.Equal(MacroLengthMetric.Measure(macro, layout), violation.ActualValue);
            Assert.Equal(limit, violation.Limit);
            Assert.Contains(MacroLengthMetric.SerializedTextUnit, violation.Message, StringComparison.Ordinal);
        }
    }
}
