using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    /// <summary>
    /// The locked-key panel's "What it does on the board" facts. The interesting assertions are the
    /// per-device ones: the whole reason this is data is that the boards answer differently, and a
    /// catalog that quietly gave every device the Freestyle answer would still pass a test that
    /// only counted rows.
    /// </summary>
    public class DeviceHotkeyCatalogTests
    {
        [Fact]
        public void TheFreestyleEdgeRgb_MountsItsVDriveWithF8()
        {
            // specs/03-vdrive-and-files.md §1: `SmartSet + F8` on the Edge RGB.
            var facts = DeviceHotkeyCatalog.ForDevice(DeviceId.FreestyleEdgeRgb);

            var vdrive = Assert.Single(facts, fact => fact.Effect.Contains("v-Drive", StringComparison.Ordinal));

            Assert.Equal("F8", vdrive.Keys);
            Assert.Equal("hold + F8 mount the v-Drive", vdrive.Text);
        }

        [Fact]
        public void TheTko_MountsItsVDriveWithRightShiftAndV_NotWithF8()
        {
            // The claim the whole type exists for: the boards do not agree, so a panel cannot.
            var tko = DeviceHotkeyCatalog.ForDevice(DeviceId.Tko);
            var rgb = DeviceHotkeyCatalog.ForDevice(DeviceId.FreestyleEdgeRgb);

            var tkoVDrive = Assert.Single(tko, fact => fact.Effect.Contains("v-Drive", StringComparison.Ordinal));
            var rgbVDrive = Assert.Single(rgb, fact => fact.Effect.Contains("v-Drive", StringComparison.Ordinal));

            Assert.Equal("right shift + V", tkoVDrive.Keys);
            Assert.NotEqual(rgbVDrive.Keys, tkoVDrive.Keys);
        }

        [Fact]
        public void TheAdvantage2_MountsItsVDriveWithF1()
        {
            var facts = DeviceHotkeyCatalog.ForDevice(DeviceId.Advantage2);

            var vdrive = Assert.Single(facts);

            Assert.Equal("F1", vdrive.Keys);
        }

        [Fact]
        public void TheAdvantage360_SwitchesProfilesFromZero_BecauseProfileZeroExistsThere()
        {
            // specs/02-devices.md: profiles 0-9 on the Advantage 360, 1-9 everywhere else.
            var adv360 = Assert.Single(
                DeviceHotkeyCatalog.ForDevice(DeviceId.Advantage360),
                fact => fact.Effect.Contains("profile", StringComparison.Ordinal));

            var rgb = Assert.Single(
                DeviceHotkeyCatalog.ForDevice(DeviceId.FreestyleEdgeRgb),
                fact => fact.Effect.Contains("profile", StringComparison.Ordinal));

            Assert.Equal("0…9", adv360.Keys);
            Assert.Equal("1…9", rgb.Keys);
        }

        [Fact]
        public void OnlyTheFreestyleFamily_ClaimsTheFRowHotkeys()
        {
            // They are authored board silkscreen, not spec text, and only one board's silkscreen is
            // authored in this repo. Claiming them for the TKO would be the app inventing a fact.
            foreach (var device in Enum.GetValues<DeviceId>())
            {
                var silkscreen = DeviceHotkeyCatalog.ForDevice(device)
                    .Where(fact => fact.Source == DeviceHotkeyFact.SilkscreenSource)
                    .ToArray();

                var isFreestyle = device is DeviceId.FreestyleEdge
                    or DeviceId.FreestylePro
                    or DeviceId.FreestyleEdgeRgb;

                Assert.Equal(isFreestyle ? 1 : 0, silkscreen.Length);
            }
        }

        [Fact]
        public void EveryFact_NamesWhereItCameFrom()
        {
            // The type's whole discipline: there is no single spec section for this section of the
            // panel, so nothing may be stated without saying where it is from.
            foreach (var device in Enum.GetValues<DeviceId>())
            {
                foreach (var fact in DeviceHotkeyCatalog.ForDevice(device))
                {
                    Assert.False(string.IsNullOrWhiteSpace(fact.Source), $"{device}: '{fact.Text}' cites nothing.");
                    Assert.False(string.IsNullOrWhiteSpace(fact.Keys));
                    Assert.False(string.IsNullOrWhiteSpace(fact.Effect));
                }
            }
        }

        [Fact]
        public void EveryFactsText_StartsWithTheHoldPrefix()
        {
            // The mockup writes the combinations as "hold + X" rather than naming the configuration
            // key twice — the panel's header already says which key it is talking about.
            foreach (var device in Enum.GetValues<DeviceId>())
            {
                Assert.All(
                    DeviceHotkeyCatalog.ForDevice(device),
                    fact => Assert.StartsWith(DeviceHotkeyCatalog.HoldPrefix, fact.Text, StringComparison.Ordinal));
            }
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        [InlineData(DeviceId.None)]
        public void ADeviceWithNoConfigurationKeyOfItsOwn_AnswersWithNothing(DeviceId device)
        {
            // Empty, so the panel draws no section rather than an empty one.
            Assert.Empty(DeviceHotkeyCatalog.ForDevice(device));
            Assert.False(DeviceHotkeyCatalog.HasFacts(device));
        }

        [Fact]
        public void HasFacts_AgreesWithForDevice()
        {
            foreach (var device in Enum.GetValues<DeviceId>())
            {
                Assert.Equal(DeviceHotkeyCatalog.ForDevice(device).Count > 0, DeviceHotkeyCatalog.HasFacts(device));
            }
        }
    }
}
