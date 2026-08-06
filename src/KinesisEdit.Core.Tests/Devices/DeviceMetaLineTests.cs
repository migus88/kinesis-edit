using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    public class DeviceMetaLineTests
    {
        private const string Separator = " · ";

        [Theory]
        [InlineData(DeviceId.SavantElite2, "3-button foot pedal · accessory jack")]
        [InlineData(DeviceId.Advantage2, "Split contoured · 2 layers")]
        [InlineData(DeviceId.FreestyleEdge, "Split flat · 2 layers · blue backlight")]
        [InlineData(DeviceId.FreestylePro, "Split flat · 2 layers")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "Split flat · 2 layers · per-key RGB")]
        [InlineData(DeviceId.CrossfireKeypad, "")]
        [InlineData(DeviceId.Tko, "60% gaming · 2 layers · per-key + edge RGB")]
        [InlineData(DeviceId.Advantage360, "Split contoured · 5 layers · 6 indicator LEDs")]
        [InlineData(DeviceId.Advantage360Professional, "Split contoured")]
        public void Describe_WithCatalogDevice_MatchesTheDesignMetaLine(DeviceId deviceId, string expected)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expected, DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithSavantElite2_IsFormFactorPlusAccessoryJackOnly()
        {
            var device = DeviceCatalog.GetById(DeviceId.SavantElite2);

            var metaLine = DeviceMetaLine.Describe(device);

            Assert.Equal(2, metaLine.Split(Separator).Length);
            Assert.Equal("3-button foot pedal", metaLine.Split(Separator)[0]);
            Assert.Equal("accessory jack", metaLine.Split(Separator)[1]);
            Assert.DoesNotContain("layer", metaLine);
        }

        [Fact]
        public void Describe_WithEveryCatalogDevice_HasNoEmptyOrDanglingSegment()
        {
            foreach (var device in DeviceCatalog.All)
            {
                var metaLine = DeviceMetaLine.Describe(device);

                Assert.Equal(metaLine.Trim(), metaLine);
                Assert.DoesNotContain("  ", metaLine);
                Assert.False(metaLine.StartsWith('·'), device.DisplayName);
                Assert.False(metaLine.EndsWith('·'), device.DisplayName);

                if (metaLine.Length == 0)
                {
                    continue;
                }

                foreach (var segment in metaLine.Split(Separator))
                {
                    Assert.False(string.IsNullOrWhiteSpace(segment), device.DisplayName);
                }
            }
        }

        [Fact]
        public void Describe_WithNoLayerCountAndNoLighting_OmitsBothSegments()
        {
            var device = CreateDevice() with
            {
                FormFactor = DeviceFormFactor.SplitFlat,
                LayerCount = null,
                Lighting = LightingCapability.None
            };

            Assert.Equal("Split flat", DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithNoFormFactorButLighting_StartsAtTheLightingSegment()
        {
            var device = CreateDevice() with
            {
                FormFactor = DeviceFormFactor.None,
                LayerCount = null,
                Lighting = new LightingCapability { Kind = LightingKind.PerKeyRgb }
            };

            Assert.Equal("per-key RGB", DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithNothingDescribable_ReturnsEmptyString()
        {
            var device = CreateDevice();

            Assert.Equal(string.Empty, DeviceMetaLine.Describe(device));
        }

        [Theory]
        [InlineData(1, "1 layer")]
        [InlineData(2, "2 layers")]
        [InlineData(5, "5 layers")]
        public void Describe_WithLayerCount_PluralisesTheLayerSegment(int layerCount, string expected)
        {
            var device = CreateDevice() with { LayerCount = layerCount };

            Assert.Equal(expected, DeviceMetaLine.Describe(device));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Describe_WithNonPositiveLayerCount_OmitsTheLayerSegment(int layerCount)
        {
            var device = CreateDevice() with { LayerCount = layerCount };

            Assert.Equal(string.Empty, DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithPerKeyRgbAndEdgeStrip_NamesBoth()
        {
            var device = CreateDevice() with
            {
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.PerKeyRgb,
                    EdgeLedLeftCount = 9,
                    EdgeLedBottomCount = 15,
                    EdgeLedRightCount = 9
                }
            };

            Assert.Equal("per-key + edge RGB", DeviceMetaLine.Describe(device));
        }

        [Theory]
        [InlineData(1, "1 indicator LED")]
        [InlineData(6, "6 indicator LEDs")]
        public void Describe_WithIndicatorLeds_PluralisesTheLightingSegment(int ledCount, string expected)
        {
            var device = CreateDevice() with
            {
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.IndicatorLeds,
                    IndicatorLedCount = ledCount
                }
            };

            Assert.Equal(expected, DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithIndicatorLedKindButNoCount_OmitsTheLightingSegment()
        {
            var device = CreateDevice() with
            {
                Lighting = new LightingCapability { Kind = LightingKind.IndicatorLeds }
            };

            Assert.Equal(string.Empty, DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithBlueBacklight_UsesTheSpecWording()
        {
            var device = CreateDevice() with
            {
                Lighting = new LightingCapability { Kind = LightingKind.BlueBacklight }
            };

            Assert.Equal("blue backlight", DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithEverySegmentPresent_OrdersFormFactorLayersLightingAccessory()
        {
            var device = CreateDevice() with
            {
                FormFactor = DeviceFormFactor.SplitContoured,
                LayerCount = 5,
                Lighting = new LightingCapability
                {
                    Kind = LightingKind.IndicatorLeds,
                    IndicatorLedCount = 6
                },
                AccessoryNote = "accessory jack"
            };

            Assert.Equal(
                "Split contoured · 5 layers · 6 indicator LEDs · accessory jack",
                DeviceMetaLine.Describe(device));
        }

        [Fact]
        public void Describe_WithNullDevice_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DeviceMetaLine.Describe(null!));
        }

        private static DeviceDefinition CreateDevice()
        {
            return new DeviceDefinition
            {
                Id = DeviceId.None,
                DisplayName = "Test Device",
                LegacyAppId = 0,
                ServingApp = ServingApp.None
            };
        }
    }
}
