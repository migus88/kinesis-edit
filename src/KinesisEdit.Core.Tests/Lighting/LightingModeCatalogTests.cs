using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Pins the mode catalog against the mode tables of specs/07-lighting.md §2.2, §2.3, and
    /// §3: file tokens (including <c>[star]</c>, not <c>[starlight]</c>), the reserved
    /// <c>[black]</c> token, per-effect direction rules, menu availability per device/context,
    /// and the Ripple/Fireball firmware gate.
    /// </summary>
    public class LightingModeCatalogTests
    {
        [Fact]
        public void All_Always_HasOneRowPerLightingModeValue()
        {
            Assert.Equal(16, LightingModeCatalog.All.Count);
            Assert.Equal(16, LightingModeCatalog.All.Select(definition => definition.Mode).Distinct().Count());
        }

        [Fact]
        public void All_Always_HasThirteenKeyBacklightTokensAndEightEdgeTokens()
        {
            var keyBacklightTokens = LightingModeCatalog.All
                .Where(definition => definition.KeyBacklightToken is not null)
                .Select(definition => definition.KeyBacklightToken)
                .ToList();
            var edgeTokens = LightingModeCatalog.All
                .Where(definition => definition.EdgeToken is not null)
                .Select(definition => definition.EdgeToken)
                .ToList();

            // 12 readable key-backlight mode tokens plus the reserved [black] (§2.2).
            Assert.Equal(13, keyBacklightTokens.Count);
            Assert.Equal(8, edgeTokens.Count);
        }

        [Fact]
        public void All_Always_OffersFourteenModesPerKeyBacklightMenuAndTenOnTheEdge()
        {
            // 13 effects + Disable in both key-backlight menus; 9 modes + Disable on the edge (§3).
            Assert.Equal(14, LightingModeCatalog.All.Count(definition => definition.OffersInRgbKeyBacklightMenu));
            Assert.Equal(14, LightingModeCatalog.All.Count(definition => definition.OffersInTkoKeyBacklightMenu));
            Assert.Equal(10, LightingModeCatalog.All.Count(definition => definition.OffersInTkoEdgeMenu));
        }

        [Theory]
        [InlineData(LightingMode.Monochrome, "mono", "mono_edge")]
        [InlineData(LightingMode.Breathe, "breathe", "breathe_edge")]
        [InlineData(LightingMode.Spectrum, "spectrum", "spectrum_edge")]
        [InlineData(LightingMode.Wave, "wave", "wave_edge")]
        [InlineData(LightingMode.FrozenWave, null, "frozenwave_edge")]
        [InlineData(LightingMode.Reactive, "reactive", null)]
        [InlineData(LightingMode.Ripple, "ripple", null)]
        [InlineData(LightingMode.Fireball, "fireball", null)]
        [InlineData(LightingMode.Starlight, "star", null)]
        [InlineData(LightingMode.Rebound, "rebound", "rebound_edge")]
        [InlineData(LightingMode.Loop, "loop", "loop_edge")]
        [InlineData(LightingMode.Pulse, "pulse", "pulse_edge")]
        [InlineData(LightingMode.Rain, "rain", null)]
        [InlineData(LightingMode.PitchBlack, "black", null)]
        [InlineData(LightingMode.Freestyle, null, null)]
        [InlineData(LightingMode.Disabled, null, null)]
        public void Find_PerMode_CarriesTheSpecFileTokens(
            LightingMode mode,
            string? expectedKeyBacklightToken,
            string? expectedEdgeToken)
        {
            var definition = LightingModeCatalog.Find(mode);

            Assert.Equal(expectedKeyBacklightToken, definition.KeyBacklightToken);
            Assert.Equal(expectedEdgeToken, definition.EdgeToken);
        }

        [Theory]
        [InlineData(LightingMode.Disabled, "Disable")]
        [InlineData(LightingMode.Freestyle, "Freestyle")]
        [InlineData(LightingMode.Monochrome, "Monochrome")]
        [InlineData(LightingMode.Breathe, "Breathe")]
        [InlineData(LightingMode.Spectrum, "Spectrum")]
        [InlineData(LightingMode.Wave, "Wave")]
        [InlineData(LightingMode.FrozenWave, "Frozen Wave")]
        [InlineData(LightingMode.Reactive, "Reactive")]
        [InlineData(LightingMode.Ripple, "Ripple")]
        [InlineData(LightingMode.Fireball, "Fireball")]
        [InlineData(LightingMode.Starlight, "Starlight")]
        [InlineData(LightingMode.Rebound, "Rebound")]
        [InlineData(LightingMode.Loop, "Loop")]
        [InlineData(LightingMode.Pulse, "Pulse")]
        [InlineData(LightingMode.Rain, "Rain")]
        [InlineData(LightingMode.PitchBlack, "Pitch Black")]
        public void Find_PerMode_CarriesTheSpec3UiCaption(LightingMode mode, string expectedDisplayName)
        {
            Assert.Equal(expectedDisplayName, LightingModeCatalog.Find(mode).DisplayName);
        }

        [Fact]
        public void All_Always_HasADistinctNonEmptyDisplayNamePerMode()
        {
            Assert.All(LightingModeCatalog.All, definition => Assert.False(string.IsNullOrWhiteSpace(definition.DisplayName)));
            Assert.Equal(
                LightingModeCatalog.All.Count,
                LightingModeCatalog.All.Select(definition => definition.DisplayName).Distinct().Count());
        }

        [Theory]
        [InlineData("mono", LightingMode.Monochrome)]
        [InlineData("MONO", LightingMode.Monochrome)]
        [InlineData("star", LightingMode.Starlight)]
        [InlineData("rain", LightingMode.Rain)]
        public void FindByKeyBacklightToken_WithKnownToken_ResolvesCaseInsensitively(
            string token,
            LightingMode expectedMode)
        {
            Assert.Equal(expectedMode, LightingModeCatalog.FindByKeyBacklightToken(token)?.Mode);
        }

        [Theory]
        [InlineData("starlight")]
        [InlineData("mono_edge")]
        [InlineData("")]
        [InlineData(null)]
        public void FindByKeyBacklightToken_WithUnknownToken_ReturnsNull(string? token)
        {
            Assert.Null(LightingModeCatalog.FindByKeyBacklightToken(token));
        }

        [Theory]
        [InlineData("mono_edge", LightingMode.Monochrome)]
        [InlineData("FROZENWAVE_EDGE", LightingMode.FrozenWave)]
        [InlineData("rebound_edge", LightingMode.Rebound)]
        public void FindByEdgeToken_WithKnownToken_ResolvesCaseInsensitively(string token, LightingMode expectedMode)
        {
            Assert.Equal(expectedMode, LightingModeCatalog.FindByEdgeToken(token)?.Mode);
        }

        [Fact]
        public void Find_WithPitchBlack_IsReservedAndInNoMenu()
        {
            var definition = LightingModeCatalog.Find(LightingMode.PitchBlack);

            Assert.True(definition.IsReserved);
            Assert.False(definition.OffersInRgbKeyBacklightMenu);
            Assert.False(definition.OffersInTkoKeyBacklightMenu);
            Assert.False(definition.OffersInTkoEdgeMenu);
        }

        [Theory]
        [InlineData(LightingMode.Wave, new[] { LightingDirection.Down, LightingDirection.Left, LightingDirection.Up, LightingDirection.Right })]
        [InlineData(LightingMode.Loop, new[] { LightingDirection.Down, LightingDirection.Left, LightingDirection.Up, LightingDirection.Right })]
        [InlineData(LightingMode.Fireball, new[] { LightingDirection.Left, LightingDirection.Right })]
        [InlineData(LightingMode.Rebound, new[] { LightingDirection.Left, LightingDirection.Up })]
        [InlineData(LightingMode.Reactive, new LightingDirection[0])]
        [InlineData(LightingMode.Rain, new LightingDirection[0])]
        public void Find_PerMode_CarriesTheKeyBacklightDirectionRulesOfSpec24(
            LightingMode mode,
            LightingDirection[] expectedDirections)
        {
            Assert.Equal(expectedDirections, LightingModeCatalog.Find(mode).KeyBacklightDirections);
        }

        [Theory]
        [InlineData(LightingMode.Wave, new[] { LightingDirection.Left, LightingDirection.Right })]
        [InlineData(LightingMode.Loop, new[] { LightingDirection.Left, LightingDirection.Right })]
        [InlineData(LightingMode.Rebound, new LightingDirection[0])]
        public void Find_PerMode_CarriesTheEdgeDirectionRulesOfSpec23(
            LightingMode mode,
            LightingDirection[] expectedDirections)
        {
            Assert.Equal(expectedDirections, LightingModeCatalog.Find(mode).EdgeDirections);
        }

        [Fact]
        public void Find_WithRippleAndFireball_AreGatedByTheRippleAndFireballFeature()
        {
            Assert.Equal(
                FirmwareFeature.RippleAndFireballEffects,
                LightingModeCatalog.Find(LightingMode.Ripple).GatedByFeature);
            Assert.Equal(
                FirmwareFeature.RippleAndFireballEffects,
                LightingModeCatalog.Find(LightingMode.Fireball).GatedByFeature);
            Assert.Equal(FirmwareFeature.None, LightingModeCatalog.Find(LightingMode.Reactive).GatedByFeature);
        }

        [Fact]
        public void Find_WithEdgeOnlyModes_MatchesTheTkoEdgeMenuOfSpec03()
        {
            var edgeMenuModes = LightingModeCatalog.All
                .Where(definition => definition.OffersInTkoEdgeMenu)
                .Select(definition => definition.Mode)
                .ToList();

            Assert.Equal(
                new[]
                {
                    LightingMode.Disabled,
                    LightingMode.Freestyle,
                    LightingMode.Monochrome,
                    LightingMode.Breathe,
                    LightingMode.Spectrum,
                    LightingMode.Wave,
                    LightingMode.FrozenWave,
                    LightingMode.Rebound,
                    LightingMode.Loop,
                    LightingMode.Pulse
                },
                edgeMenuModes);
        }
    }
}
