using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;
using KinesisEdit.Core.Model;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The cap's own state: the badge flags it pushes at the picture, the legend rule that decides
    /// what an untouched cap reads, and the two lighting layers pushed onto its face. Core announces
    /// nothing, so most of this is about <see cref="KeyboardKeyViewModel.RefreshFromModel"/>
    /// agreeing with the constructor; the face is the exception, because it is pushed in rather
    /// than read back, and it is where a stored colour becomes a drawn one (issue #124).
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

        [AvaloniaFact]
        public void ApplyPaint_PutsTheSoftenedColourOnTheFace_AndNotTheStoredOne()
        {
            // Issue #124: the seam. A stored colour becomes a FACE colour here, so the cap's hex is
            // the softened value while the lighting model — and the rail's colour slots, and the
            // picker, which read it — keep the value that is on file. Asserted against
            // LedPreviewTint rather than against a literal, because the constants are a look and
            // are meant to be re-tuned without a test moving with them; that the two differ at all
            // is the claim.
            var cap = CreateCap("1");

            cap.ApplyPaint(new LedColor(255, 0, 0), LightingEffectFrame.PaintOpacityDirect);

            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(255, 0, 0))),
                cap.PaintColorHex);
            Assert.NotEqual("#FF0000", cap.PaintColorHex);
            Assert.True(cap.HasPaintColor);
        }

        [AvaloniaFact]
        public void ApplyEffect_SoftensTheSampledColourTheSameWay()
        {
            // The effect layer is re-pushed ~30 times a second and is not file state at all, but it
            // is drawn on the same face by the same rules, so it goes through the same tint. A
            // preview whose paint was softened and whose effect was not would show two boards.
            var cap = CreateCap("1");

            cap.ApplyEffect(new LedColor(0, 255, 0), 1.0);

            Assert.Equal(
                KeyColorOverlay.ToHex(LedPreviewTint.Soften(new LedColor(0, 255, 0))),
                cap.EffectColorHex);
            Assert.NotEqual("#00FF00", cap.EffectColorHex);
        }

        [AvaloniaFact]
        public void ApplyPaint_WithNoColour_LeavesTheCapUnlit_RatherThanDimGrey()
        {
            // "Off is hatched, never black" — and never a dim grey either. An unlit key is ABSENCE,
            // so the softening must not be reached at all: a null that came back as a colour would
            // put a face on every unpainted cap of the board.
            var cap = CreateCap("1");

            cap.ApplyPaint(new LedColor(255, 0, 0), LightingEffectFrame.PaintOpacityDirect);
            cap.ApplyPaint(null, LightingEffectFrame.PaintOpacityDirect);
            cap.ApplyEffect(null, 0);

            Assert.Null(cap.PaintColorHex);
            Assert.Null(cap.EffectColorHex);
            Assert.False(cap.HasPaintColor);
            Assert.False(cap.HasEffectColor);
        }

        [AvaloniaFact]
        public void ApplyPaint_WithBlack_StaysBlack()
        {
            // The other half: black IS a colour a key can be lit (Pitch Black lights every key black
            // at full intensity, and it has to read differently from off). Softening it toward grey
            // would make that mode look like a board of faintly lit keys.
            var cap = CreateCap("1");

            cap.ApplyPaint(LedColor.Black, LightingEffectFrame.PaintOpacityDirect);

            Assert.Equal("#000000", cap.PaintColorHex);
            Assert.True(cap.HasPaintColor);
        }

        [AvaloniaFact]
        public void ApplyPaint_WithTheSameStoredColourTwice_SoftensOnceAndNotifiesOnce()
        {
            // The change check stays on the STORED colour, which is what keeps the ~30 fps repaint
            // free for a cap nothing moved on — and what stops the tint, which is not idempotent,
            // from being applied to its own output.
            var cap = CreateCap("1");
            var changed = new List<string>();

            cap.ApplyPaint(new LedColor(0, 128, 255), LightingEffectFrame.PaintOpacityDirect);

            var first = cap.PaintColorHex;

            cap.PropertyChanged += (_, arguments) => changed.Add(arguments.PropertyName!);
            cap.ApplyPaint(new LedColor(0, 128, 255), LightingEffectFrame.PaintOpacityDirect);

            Assert.Equal(first, cap.PaintColorHex);
            Assert.DoesNotContain(nameof(KeyboardKeyViewModel.PaintColorHex), changed);
        }

        [AvaloniaFact]
        public void PaintColorHex_SetDirectly_IsTakenAsTheFaceColour()
        {
            // The escape hatch a test or a design scene stands a lit cap up with. It is deliberately
            // NOT softened: it is already a face colour, and softening it here would be the second
            // application the one-seam rule exists to prevent.
            var cap = CreateCap("1");

            cap.PaintColorHex = "#FF0000";

            Assert.Equal("#FF0000", cap.PaintColorHex);
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
