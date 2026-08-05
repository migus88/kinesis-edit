using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    /// <summary>
    /// Asserts the macro capability record of specs/06-macros.md §1 (slots), §2.1 (co-triggers),
    /// §4 (speed and repeat) and §6 (limits).
    /// </summary>
    public class MacroCapabilityTests
    {
        [Fact]
        public void None_Always_ReportsNoSupportAndNoLimits()
        {
            var capability = MacroCapability.None;

            Assert.False(capability.IsSupported);
            Assert.Null(capability.MaxMacroCount);
            Assert.Null(capability.GatedMaxMacroCount);
            Assert.Null(capability.MacroCountGateFirmware);
            Assert.Null(capability.MaxCharactersPerMacro);
            Assert.Null(capability.MaxTotalKeystrokes);
            Assert.Null(capability.SlotsPerKey);
            Assert.Null(capability.PersistedSlotsPerKey);
            Assert.Null(capability.MaxCoTriggersPerMacro);
            Assert.Null(capability.PersistedCoTriggersPerMacro);
            Assert.Null(capability.Speed);
            Assert.Null(capability.Repeat);
            Assert.False(capability.UsesFlatMacroList);
            Assert.False(capability.ClampsOutOfRangeValues);
        }

        [Fact]
        public void PersistedSlotsPerKey_WhenSet_NeverExceedsSlotsPerKey()
        {
            foreach (var device in DeviceCatalog.All)
            {
                var macros = device.Macros;

                if (macros.PersistedSlotsPerKey is null)
                {
                    continue;
                }

                Assert.NotNull(macros.SlotsPerKey);
                Assert.InRange(macros.PersistedSlotsPerKey.Value, 1, macros.SlotsPerKey.Value);
            }
        }

        [Fact]
        public void PersistedCoTriggersPerMacro_WhenSet_NeverExceedsMaxCoTriggersPerMacro()
        {
            foreach (var device in DeviceCatalog.All)
            {
                var macros = device.Macros;

                if (macros.PersistedCoTriggersPerMacro is null)
                {
                    continue;
                }

                Assert.NotNull(macros.MaxCoTriggersPerMacro);
                Assert.InRange(macros.PersistedCoTriggersPerMacro.Value, 1, macros.MaxCoTriggersPerMacro.Value);
            }
        }

        [Fact]
        public void SpeedAndRepeat_ForEveryDevice_HaveDefaultsInsideTheirOwnRange()
        {
            foreach (var device in DeviceCatalog.All)
            {
                var speed = device.Macros.Speed;
                var repeat = device.Macros.Repeat;

                if (speed is not null)
                {
                    Assert.True(speed.Contains(speed.Default));
                }

                if (repeat is not null)
                {
                    Assert.True(repeat.Contains(repeat.Default));
                }
            }
        }

        [Fact]
        public void UsesFlatMacroList_ForEveryDevice_ExcludesPerKeySlots()
        {
            foreach (var device in DeviceCatalog.All)
            {
                var macros = device.Macros;

                if (macros.UsesFlatMacroList)
                {
                    Assert.Null(macros.SlotsPerKey);
                    Assert.Null(macros.PersistedSlotsPerKey);
                }
            }
        }
    }
}
