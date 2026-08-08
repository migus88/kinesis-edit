using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The structural guard over <c>Themes/Tokens.axaml</c>: both variants carry the same roles,
    /// every role resolves through the running application, and the two halves of every token pair
    /// agree. The app follows the OS theme, so either variant can be the live one — a role that
    /// exists in only one of them is a colour that silently disappears on half the machines.
    /// </summary>
    public class TokenCompletenessTests
    {
        [AvaloniaFact]
        public void ThemeDictionaries_AreExactlyDarkAndLight()
        {
            var declared = DesignTokens.LoadTokenDictionary()
                .ThemeDictionaries
                .Keys
                .Select(variant => variant.Key?.ToString())
                .OrderBy(key => key, StringComparer.Ordinal);

            Assert.Equal(new[] { "Dark", "Light" }, declared);
        }

        [AvaloniaFact]
        public void DarkAndLight_DeclareTheSameRoles()
        {
            var dark = DesignTokens.DeclaredKeys(ThemeVariant.Dark).ToHashSet(StringComparer.Ordinal);
            var light = DesignTokens.DeclaredKeys(ThemeVariant.Light).ToHashSet(StringComparer.Ordinal);

            var missingFromLight = dark.Except(light, StringComparer.Ordinal).Order(StringComparer.Ordinal);
            var missingFromDark = light.Except(dark, StringComparer.Ordinal).Order(StringComparer.Ordinal);

            Assert.True(
                !missingFromLight.Any() && !missingFromDark.Any(),
                $"Light is missing [{string.Join(", ", missingFromLight)}]; "
                + $"Dark is missing [{string.Join(", ", missingFromDark)}].");
        }

        [AvaloniaFact]
        public void EveryDeclaredRole_ResolvesInBothVariants()
        {
            var keys = AllRoles();
            var unresolved = new List<string>();

            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var key in keys)
                {
                    if (!DesignTokens.TryResolve(key, variant, out _))
                    {
                        unresolved.Add($"{key} ({variant})");
                    }
                }
            }

            Assert.True(unresolved.Count == 0, $"Unresolved tokens: {string.Join(", ", unresolved)}.");
        }

        [AvaloniaFact]
        public void TheTokenSet_IsTheWholePalette()
        {
            // A blunt tripwire: the palette of docs/design/handoff.md is ~90 roles per variant, and
            // a merge that dropped half of Tokens.axaml would otherwise still pass every test above.
            Assert.True(
                DesignTokens.DeclaredKeys(ThemeVariant.Dark).Count >= 80,
                $"Only {DesignTokens.DeclaredKeys(ThemeVariant.Dark).Count} roles are declared.");
        }

        [AvaloniaFact]
        public void EveryColorRole_HasTheMatchingBrushRole()
        {
            var roles = AllRoles().ToHashSet(StringComparer.Ordinal);

            var orphanColors = roles
                .Where(role => role.EndsWith(DesignTokens.ColorSuffix, StringComparison.Ordinal))
                .Where(role => !roles.Contains(ToBrushRole(role)))
                .Order(StringComparer.Ordinal);

            Assert.True(!orphanColors.Any(), $"Colors with no Brush: {string.Join(", ", orphanColors)}.");
        }

        [AvaloniaFact]
        public void EveryBrushRole_HasTheMatchingColorRole()
        {
            var roles = AllRoles().ToHashSet(StringComparer.Ordinal);

            var orphanBrushes = roles
                .Where(role => role.EndsWith(DesignTokens.BrushSuffix, StringComparison.Ordinal))
                .Where(role => !roles.Contains(ToColorRole(role)))
                .Order(StringComparer.Ordinal);

            Assert.True(!orphanBrushes.Any(), $"Brushes with no Color: {string.Join(", ", orphanBrushes)}.");
        }

        [AvaloniaFact]
        public void EveryBrushRole_PaintsItsOwnColorRole()
        {
            var mismatches = new List<string>();

            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var role in AllRoles().Where(role => role.EndsWith(DesignTokens.BrushSuffix, StringComparison.Ordinal)))
                {
                    var declared = DesignTokens.ResolveColor(ToColorRole(role), variant);
                    var painted = DesignTokens.ResolveBrushColor(role, variant);

                    if (declared != painted)
                    {
                        mismatches.Add($"{role} ({variant}) paints {painted} but its Color is {declared}");
                    }
                }
            }

            Assert.True(mismatches.Count == 0, string.Join("; ", mismatches));
        }

        [AvaloniaFact]
        public void EveryColorRole_ResolvesToAColor()
        {
            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var role in AllRoles().Where(role => role.EndsWith(DesignTokens.ColorSuffix, StringComparison.Ordinal)))
                {
                    Assert.IsType<Color>(DesignTokens.Resolve(role, variant));
                }
            }
        }

        [AvaloniaFact]
        public void EveryShadowRole_ResolvesToBoxShadows()
        {
            var shadows = AllRoles()
                .Where(role => role.StartsWith("Shadow", StringComparison.Ordinal))
                .ToArray();

            // `ShadowKeyHaloStrong` joined the set with issue #128 — the opaque two-tone ring a
            // selected key cap actually wears, beside the handoff's own translucent wash rather
            // than in place of it. See TokenValueTests for both values and why there are two.
            Assert.Equal(
                new[]
                {
                    "ShadowFocusHalo", "ShadowKeyHalo", "ShadowKeyHaloStrong", "ShadowModal", "ShadowPopover"
                },
                shadows.Order(StringComparer.Ordinal));

            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var role in shadows)
                {
                    Assert.IsType<BoxShadows>(DesignTokens.Resolve(role, variant));
                }
            }
        }

        private static IReadOnlyList<string> AllRoles()
        {
            return DesignTokens.DeclaredKeys(ThemeVariant.Dark)
                .Union(DesignTokens.DeclaredKeys(ThemeVariant.Light), StringComparer.Ordinal)
                .OrderBy(role => role, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ToBrushRole(string colorRole)
        {
            return colorRole[..^DesignTokens.ColorSuffix.Length] + DesignTokens.BrushSuffix;
        }

        private static string ToColorRole(string brushRole)
        {
            return brushRole[..^DesignTokens.BrushSuffix.Length] + DesignTokens.ColorSuffix;
        }
    }
}
