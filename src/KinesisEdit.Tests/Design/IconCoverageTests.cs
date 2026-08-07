using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using KinesisEdit.Converters;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The catalog's coverage, driven by the domain rather than by a list: the devices come from
    /// <see cref="DeviceCatalog"/>, the lighting marks from the <see cref="LightingMode"/> enum, the
    /// direction arrows from <see cref="LightingDirection"/> and the message marks from
    /// <see cref="MessageBoxIcon"/>. Adding a member to any of those fails this suite until its mark
    /// exists, which is the point — a missing mark is a hole in a rail nobody notices until the
    /// device is plugged in.
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

        [AvaloniaTheory]
        [MemberData(nameof(LightingDirections))]
        public void EveryLightingDirection_OtherThanNone_HasAnArrow(LightingDirection direction)
        {
            // The direction row is data-driven — which arrows a mode accepts comes out of the
            // lighting catalog — so the four marks are reached from C# through
            // LightingDirectionMarkConverter and are invisible to every markup guard. This is the
            // one thing that can see them, which is why the key is asked of the converter rather
            // than composed here: a typo in its prefix would resolve to nothing and simply draw an
            // arrow-shaped hole in the row.
            var key = LightingDirectionMarkConverter.GetResourceKey(direction);

            Assert.NotNull(key);

            foreach (var variant in DesignTokens.Variants)
            {
                IconCatalog.ResolveGeometry(key!, variant);
            }
        }

        [AvaloniaFact]
        public void TheDirectionlessMember_HasNoArrowAndAsksForNone()
        {
            // `None` is "this effect has no direction", not "it points nowhere" — the file format
            // never writes it (specs/07-lighting.md). It converts to null, and an Icon handed null
            // Data draws nothing, which is what the design's "a feature a device lacks is not
            // rendered at all" needs.
            Assert.Null(LightingDirectionMarkConverter.GetResourceKey(LightingDirection.None));
            Assert.False(
                DesignTokens.TryResolve(LightingDirectionMarkConverter.KeyPrefix + LightingDirection.None, ThemeVariant.Dark, out _),
                "There is a mark for the directionless member, which nothing can ever draw.");
        }

        [AvaloniaFact]
        public void NoArrowMark_NamesSomethingOutsideTheDirectionEnum()
        {
            // The mirror, as for the device art and the mode marks. The arrows share
            // Themes/Icons.axaml with the state and action marks rather than living in a family of
            // their own, so the scan is by prefix rather than by file.
            var strays = IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/Icons.axaml"))
                .Where(key => key.StartsWith(LightingDirectionMarkConverter.KeyPrefix, StringComparison.Ordinal))
                .Where(key => !Enum.TryParse<LightingDirection>(
                    key[LightingDirectionMarkConverter.KeyPrefix.Length..],
                    ignoreCase: false,
                    out var direction) || direction == LightingDirection.None)
                .ToArray();

            Assert.True(strays.Length == 0, $"Arrows naming no direction: {string.Join(", ", strays)}.");

            // ...and there are four of them, so a scan that stopped finding any cannot pass.
            Assert.Equal(
                4,
                IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/Icons.axaml"))
                    .Count(key => key.StartsWith(LightingDirectionMarkConverter.KeyPrefix, StringComparison.Ordinal)));
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

        /// <summary>Every direction that is drawn; <c>None</c> is the absence of one.</summary>
        public static TheoryData<LightingDirection> LightingDirections()
        {
            var directions = new TheoryData<LightingDirection>();

            foreach (var direction in Enum.GetValues<LightingDirection>().Where(direction => direction != LightingDirection.None))
            {
                directions.Add(direction);
            }

            return directions;
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
