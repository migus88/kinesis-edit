using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The catalog's coverage, driven by the domain rather than by a list: the devices come from
    /// <see cref="DeviceCatalog"/>, the lighting marks from the <see cref="LightingMode"/> enum and
    /// the message marks from <see cref="MessageBoxIcon"/>. Adding a member to any of those fails
    /// this suite until its mark exists, which is the point — a missing mark is a hole in a rail
    /// nobody notices until the device is plugged in.
    /// <para>
    /// It runs the other way too: a mark that names nothing is dead weight the catalog must not
    /// carry, and a mark for a device the app cannot configure would be worse than dead — it would
    /// suggest an editor that does not exist.
    /// </para>
    /// </summary>
    public class IconCoverageTests
    {
        private const string DeviceArtPrefix = "DeviceArt";

        private const string LightingMarkPrefix = "LightingMark";

        private const string IconPrefix = "Icon";

        [AvaloniaFact]
        public void EveryProgrammableDevice_HasArtOfItsOwn()
        {
            var missing = DeviceCatalog.All
                .Where(device => device.IsProgrammable)
                .Select(device => DeviceArtPrefix + device.Id)
                .Where(key => !DesignTokens.TryResolve(key, ThemeVariant.Dark, out _))
                .ToArray();

            Assert.True(missing.Length == 0, $"No device art for: {string.Join(", ", missing)}.");
        }

        [AvaloniaFact]
        public void EveryProgrammableDevicesArt_ResolvesInBothVariants()
        {
            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var device in DeviceCatalog.All.Where(device => device.IsProgrammable))
                {
                    IconCatalog.ResolveGeometry(DeviceArtPrefix + device.Id, variant);
                }
            }
        }

        [AvaloniaFact]
        public void NoDeviceTheAppCannotConfigure_CarriesArt()
        {
            // The Crossfire keypad and the Advantage360 Professional are detected and named, never
            // edited. Drawing them would promise an editor that does not exist.
            var unprogrammable = DeviceCatalog.All
                .Where(device => !device.IsProgrammable)
                .Select(device => device.Id)
                .ToArray();

            Assert.Equal(new[] { DeviceId.CrossfireKeypad, DeviceId.Advantage360Professional }, unprogrammable);

            foreach (var id in unprogrammable)
            {
                Assert.False(
                    DesignTokens.TryResolve(DeviceArtPrefix + id, ThemeVariant.Dark, out _),
                    $"{id} is not programmable but carries device art.");
            }
        }

        [AvaloniaFact]
        public void NoDeviceArtKey_NamesSomethingOutsideTheCatalog()
        {
            var programmable = DeviceCatalog.All
                .Where(device => device.IsProgrammable)
                .Select(device => DeviceArtPrefix + device.Id)
                .ToHashSet(StringComparer.Ordinal);

            var strays = IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/DeviceArt.axaml"))
                .Where(key => !programmable.Contains(key))
                .ToArray();

            Assert.True(strays.Length == 0, $"Device art naming no programmable device: {string.Join(", ", strays)}.");
        }

        [AvaloniaTheory]
        [MemberData(nameof(LightingModes))]
        public void EveryLightingMode_HasAMark(LightingMode mode)
        {
            // The mode picker renders from the enum, including the two rows that never reach a
            // menu — Disabled is the menu's "Disable" row and PitchBlack the reserved [black] token.
            foreach (var variant in DesignTokens.Variants)
            {
                IconCatalog.ResolveGeometry(LightingMarkPrefix + mode, variant);
            }
        }

        [AvaloniaFact]
        public void NoLightingMarkKey_NamesSomethingOutsideTheEnum()
        {
            var strays = IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/LightingMarks.axaml"))
                .Where(key => !Enum.TryParse<LightingMode>(key[LightingMarkPrefix.Length..], ignoreCase: false, out _))
                .ToArray();

            Assert.True(strays.Length == 0, $"Lighting marks naming no mode: {string.Join(", ", strays)}.");
        }

        [AvaloniaTheory]
        [MemberData(nameof(MessageBoxIcons))]
        public void EveryMessageBoxIcon_OtherThanNone_HasAMark(MessageBoxIcon icon)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                IconCatalog.ResolveGeometry(IconPrefix + icon, variant);
            }
        }

        /// <summary>Every member of the lighting-mode enum, which is what the mode picker lists.</summary>
        public static TheoryData<LightingMode> LightingModes()
        {
            var modes = new TheoryData<LightingMode>();

            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                modes.Add(mode);
            }

            return modes;
        }

        /// <summary>Every dialog type that draws a mark; <c>None</c> draws nothing by definition.</summary>
        public static TheoryData<MessageBoxIcon> MessageBoxIcons()
        {
            var icons = new TheoryData<MessageBoxIcon>();

            foreach (var icon in Enum.GetValues<MessageBoxIcon>().Where(icon => icon != MessageBoxIcon.None))
            {
                icons.Add(icon);
            }

            return icons;
        }
    }
}
