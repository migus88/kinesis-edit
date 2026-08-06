using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The cap's own state: the badge flags it pushes at the picture, and the legend rule that
    /// decides what an untouched cap reads. Core announces nothing, so everything here is about
    /// <see cref="KeyboardKeyViewModel.RefreshFromModel"/> agreeing with the constructor.
    /// </summary>
    public class KeyboardKeyViewModelTests
    {
        [AvaloniaFact]
        public void IsMacro_AfterAMacroIsAssigned_FollowsTheModelOnRefresh()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var cap = TestLayouts.CreateKeyViewModel(key);
            var changed = new List<string>();

            Assert.False(cap.IsMacro);

            cap.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);
            key.SetMacro(1, layout.CreateMacro());

            // The model raises nothing on its own — the cap is stale until it is refreshed.
            Assert.False(cap.IsMacro);

            cap.RefreshFromModel();

            Assert.True(cap.IsMacro);
            Assert.Contains(nameof(KeyboardKeyViewModel.IsMacro), changed);
        }

        [AvaloniaFact]
        public void IsMacro_AfterTheMacroIsCleared_GoesBackDown()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];

            key.SetMacro(1, layout.CreateMacro());

            var cap = TestLayouts.CreateKeyViewModel(key);

            Assert.True(cap.IsMacro);

            key.SetMacro(1, null);
            cap.RefreshFromModel();

            Assert.False(cap.IsMacro);
        }

        [AvaloniaFact]
        public void IsTapAndHold_AfterAnAssignment_FollowsTheModelOnRefresh()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex];
            var cap = TestLayouts.CreateKeyViewModel(key);
            var changed = new List<string>();

            Assert.False(cap.IsTapAndHold);

            cap.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);
            key.ApplyTapAndHold(TestLayouts.Gen1Key("a"), TestLayouts.Gen1Key("b"), 250);
            cap.RefreshFromModel();

            Assert.True(cap.IsTapAndHold);
            Assert.Contains(nameof(KeyboardKeyViewModel.IsTapAndHold), changed);
        }

        [AvaloniaFact]
        public void Caption_OfAnUnmodifiedKeyWithALegend_IsTheSilkscreen()
        {
            // The Gen1 caption of the digit position is "1 !"; the board prints "1" with "!" under
            // it, and the cap draws the two apart.
            var cap = CreateCap("1", legend: "1", secondaryLegend: "!");

            Assert.Equal("1", cap.Caption);
            Assert.Equal("!", cap.SecondaryLegend);
        }

        [AvaloniaFact]
        public void Caption_OfAnUnmodifiedKeyWithoutALegend_IsTheKeysOwnCaption()
        {
            var cap = CreateCap("1", legend: null, secondaryLegend: null);

            Assert.Equal("1 !", cap.Caption);
            Assert.Null(cap.SecondaryLegend);
        }

        [AvaloniaFact]
        public void Caption_OfARemappedKey_IsTheAssignmentAndNotTheSilkscreen()
        {
            var cap = CreateCap("1", legend: "1", secondaryLegend: "!");

            cap.Key.ApplyRemap(TestLayouts.Gen1Key("esc"));
            cap.RefreshFromModel();

            Assert.Equal("Esc", cap.Caption);

            // The print on the cap never moves, so the secondary legend is still there.
            Assert.Equal("!", cap.SecondaryLegend);
        }

        [AvaloniaFact]
        public void Caption_WhenTheRemapIsCleared_GoesBackToTheSilkscreen()
        {
            var cap = CreateCap("1", legend: "1");

            cap.Key.ApplyRemap(TestLayouts.Gen1Key("esc"));
            cap.RefreshFromModel();
            cap.Key.Reset();
            cap.RefreshFromModel();

            Assert.Equal("1", cap.Caption);
        }

        [AvaloniaFact]
        public void Caption_InTheConstructor_FollowsTheSameLegendRuleAsARefresh()
        {
            // Invariant 3 says every model write ends in RefreshFromModel, so a cap built over an
            // already-remapped key and a cap refreshed into that state must read the same.
            var built = CreateCap("1", legend: "1");

            built.Key.ApplyRemap(TestLayouts.Gen1Key("esc"));

            var rebuilt = new KeyboardKeyViewModel(
                built.Key,
                new KeyVisual(built.Index, 0, 0, legend: "1"),
                TokenDialect.Gen1);

            built.RefreshFromModel();

            Assert.Equal(built.Caption, rebuilt.Caption);
            Assert.Equal("Esc", rebuilt.Caption);
        }

        [AvaloniaFact]
        public void IsCaptionStacked_FollowsTheCaptionOntoAndOffTheSecondLine()
        {
            // The cap draws a caption that carries its own line break one type step down — two 9px
            // lines do not fit a 30x26 cap alongside the LED strip (Controls/KeyCapView.axaml). The
            // flag has to MOVE with the caption: remapping `Caps\nLock` to `Esc` puts one line on
            // the cap, and a caption still drawn at the stacked step would read smaller than every
            // neighbour for no reason the user can see.
            var cap = CreateCap("caps", legend: "Caps\nLock");
            var changed = new List<string>();

            Assert.True(cap.IsCaptionStacked);

            cap.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);

            cap.Key.ApplyRemap(TestLayouts.Gen1Key("esc"));
            cap.RefreshFromModel();

            Assert.Equal("Esc", cap.Caption);
            Assert.False(cap.IsCaptionStacked);
            Assert.Contains(nameof(KeyboardKeyViewModel.IsCaptionStacked), changed);

            cap.Key.Reset();
            cap.RefreshFromModel();

            Assert.True(cap.IsCaptionStacked);
        }

        [AvaloniaFact]
        public void IsCaptionStacked_OfAnOrdinaryOneLineCaption_IsFalse()
        {
            Assert.False(CreateCap("1", legend: "1").IsCaptionStacked);
        }

        [AvaloniaFact]
        public void Section_OfACap_IsThePanelTheVisualPutsItIn()
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var cap = new KeyboardKeyViewModel(
                layout.Layers[0].Keys[0],
                new KeyVisual(0, 0, 0, section: 1),
                TokenDialect.Gen1);

            Assert.Equal(1, cap.Section);
        }

        private static KeyboardKeyViewModel CreateCap(string token, string? legend = null, string? secondaryLegend = null)
        {
            var layout = KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb);
            var key = layout.Layers[0].Keys.First(candidate => candidate.OriginalKey.Code == TestLayouts.Gen1Key(token).Code);

            return new KeyboardKeyViewModel(
                key,
                new KeyVisual(key.Index, 0, 0, legend: legend, secondaryLegend: secondaryLegend),
                TokenDialect.Gen1);
        }
    }
}
