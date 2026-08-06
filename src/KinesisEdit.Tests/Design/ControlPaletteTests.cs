using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The guard over <c>Themes/Controls.axaml</c>: the base controls have to paint from the design
    /// system, in both variants, and from nothing else.
    /// <para>
    /// This matters because the failure it catches is silent. A FluentTheme key that nobody
    /// redeclares does not throw and does not fail a rendering test — it simply keeps painting
    /// Fluent's grey next to our cards, which is exactly the defect this file exists to close.
    /// </para>
    /// </summary>
    public class ControlPaletteTests
    {
        /// <summary>
        /// A sample of the map, pinned so the intent survives an edit: Fluent key, the role it must
        /// resolve to, and whether that role is the same in both variants.
        /// </summary>
        public static TheoryData<string, string> SharedAliases()
        {
            return new TheoryData<string, string>
            {
                { "ButtonBackground", "SurfaceBarBrush" },
                { "ButtonBackgroundPointerOver", "SurfaceRaisedBrush" },
                { "ButtonBackgroundPressed", "SurfaceLineBrush" },
                { "ButtonBackgroundDisabled", "SurfaceInsetBrush" },
                { "ButtonForeground", "TextPrimaryBrush" },
                { "ButtonForegroundDisabled", "TextDisabledBrush" },
                { "ButtonBorderBrush", "SurfaceBorderRaisedBrush" },
                { "ButtonBorderBrushDisabled", "SurfaceLineBrush" },
                { "AccentButtonBackground", "AccentBrush" },
                { "AccentButtonForeground", "AccentTextBrush" },
                { "AccentButtonBackgroundDisabled", "SurfaceInsetBrush" },
                { "AccentButtonForegroundDisabled", "TextDisabledBrush" },
                { "ComboBoxBackground", "SurfaceInsetBrush" },
                { "ComboBoxBorderBrush", "SurfaceLineBrush" },
                { "ComboBoxDropDownBackground", "SurfacePanelBrush" },
                { "TextControlBackground", "SurfaceInsetBrush" },
                { "TextControlBorderBrushFocused", "AccentBrush" },
                { "CheckBoxCheckBackgroundFillChecked", "AccentBrush" },
                { "CheckBoxCheckGlyphForegroundChecked", "AccentTextBrush" },
                { "SliderTrackFill", "SurfaceLineHighBrush" },
                { "SliderTrackValueFill", "AccentBrush" },
                { "ToggleSwitchFillOn", "AccentBrush" },
                { "ScrollBarTrackFill", "SurfaceInsetBrush" },
                { "ScrollBarPanningThumbBackground", "SurfaceLineHighBrush" },
                { "MenuFlyoutPresenterBackground", "SurfacePanelBrush" },
                { "FlyoutPresenterBackground", "SurfacePanelBrush" },
                { "ToolTipBackground", "SurfacePanelBrush" },
                { "SystemControlHighlightListAccentLowBrush", "AccentSelectionFillBrush" },
                { "SystemControlFocusVisualPrimaryBrush", "AccentBrush" }
            };
        }

        /// <summary>The keys typed as a <see cref="Color"/> rather than a brush by Fluent.</summary>
        public static TheoryData<string, string> ColorAliases()
        {
            return new TheoryData<string, string>
            {
                { "ExpanderHeaderBackground", "SurfaceBarColor" },
                { "ExpanderContentBackground", "SurfacePanelColor" },
                { "ExpanderHeaderForeground", "TextPrimaryColor" },
                { "ScrollBarThumbBackgroundColor", "SurfaceLineColor" },
                { "SystemAccentColor", "AccentColor" }
            };
        }

        [AvaloniaFact]
        public void ThemeDictionaries_AreExactlyDarkAndLight()
        {
            var declared = ControlPalette.Load()
                .ThemeDictionaries
                .Keys
                .Select(variant => variant.Key?.ToString())
                .OrderBy(key => key, StringComparer.Ordinal);

            Assert.Equal(new[] { "Dark", "Light" }, declared);
        }

        [AvaloniaFact]
        public void DarkAndLight_RedeclareTheSameControlKeys()
        {
            // The app follows the OS theme, so a control key mapped in one variant only is a
            // control that reverts to Fluent's palette on half the machines.
            var dark = ControlPalette.DeclaredKeys(ThemeVariant.Dark).ToHashSet(StringComparer.Ordinal);
            var light = ControlPalette.DeclaredKeys(ThemeVariant.Light).ToHashSet(StringComparer.Ordinal);

            var missingFromLight = dark.Except(light, StringComparer.Ordinal).Order(StringComparer.Ordinal);
            var missingFromDark = light.Except(dark, StringComparer.Ordinal).Order(StringComparer.Ordinal);

            Assert.True(
                !missingFromLight.Any() && !missingFromDark.Any(),
                $"Light is missing [{string.Join(", ", missingFromLight)}]; "
                + $"Dark is missing [{string.Join(", ", missingFromDark)}].");
        }

        [AvaloniaFact]
        public void EveryControlKey_ResolvesInBothVariants()
        {
            var unresolved = new List<string>();

            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var key in AllControlKeys())
                {
                    if (!DesignTokens.TryResolve(key, variant, out _))
                    {
                        unresolved.Add($"{key} ({variant})");
                    }
                }
            }

            Assert.True(unresolved.Count == 0, $"Unresolved control keys: {string.Join(", ", unresolved)}.");
        }

        [AvaloniaFact]
        public void EveryControlKey_PaintsADesignToken()
        {
            // The point of the whole file: after the merge, no base control can reach a colour the
            // design system does not name. A key left behind still resolves — to Fluent's own
            // value — so this is the assertion that catches one.
            var strays = new List<string>();

            foreach (var variant in DesignTokens.Variants)
            {
                var palette = ControlPalette.TokenColors(variant);

                foreach (var key in AllControlKeys())
                {
                    var value = DesignTokens.Resolve(key, variant);

                    var color = value switch
                    {
                        ISolidColorBrush brush => brush.Color,
                        Color declared => declared,
                        _ => (Color?)null
                    };

                    if (color is { } painted && !palette.Contains(painted))
                    {
                        strays.Add($"{key} ({variant}) paints {painted}, which is not a token");
                    }
                }
            }

            Assert.True(strays.Count == 0, string.Join(Environment.NewLine, strays));
        }

        [AvaloniaTheory]
        [MemberData(nameof(SharedAliases))]
        public void ControlKey_ResolvesToItsRole_InBothVariants(string controlKey, string role)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(
                    DesignTokens.ResolveBrushColor(role, variant),
                    DesignTokens.ResolveBrushColor(controlKey, variant));
            }
        }

        [AvaloniaTheory]
        [MemberData(nameof(ColorAliases))]
        public void ColorTypedControlKey_ResolvesToTheColorHalfOfItsRole(string controlKey, string role)
        {
            // Fluent types a scatter of its keys as Color rather than SolidColorBrush; aliasing one
            // of those to a Brush role leaves the control drawing nothing at all.
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(
                    DesignTokens.ResolveColor(role, variant),
                    DesignTokens.ResolveColor(controlKey, variant));
            }
        }

        [AvaloniaFact]
        public void TheAccentRamp_MovesAwayFromTheOnAccentColour_InEachVariant()
        {
            // Both variants rest on the accent, and both leave it entirely when disabled; only the
            // engaged states differ, because the label riding the fill flips between the variants.
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(
                    DesignTokens.ResolveBrushColor("AccentBrush", variant),
                    DesignTokens.ResolveBrushColor("AccentButtonBackground", variant));

                Assert.Equal(
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant),
                    DesignTokens.ResolveBrushColor("AccentButtonBackgroundDisabled", variant));
            }

            Assert.Equal(
                DesignTokens.ResolveBrushColor("AccentLinkHoverBrush", ThemeVariant.Dark),
                DesignTokens.ResolveBrushColor("AccentButtonBackgroundPointerOver", ThemeVariant.Dark));

            Assert.Equal(
                DesignTokens.ResolveBrushColor("AccentSelectedRingBrush", ThemeVariant.Light),
                DesignTokens.ResolveBrushColor("AccentButtonBackgroundPointerOver", ThemeVariant.Light));
        }

        [AvaloniaFact]
        public void TheShapeAliases_AreDeclaredOnceForBothVariants()
        {
            // A corner radius does not change with the theme, so these sit outside the variant
            // dictionaries — and must therefore resolve without one.
            Assert.Equal(
                new[] { "ControlCornerRadius", "OverlayCornerRadius" },
                ControlPalette.ThemeIndependentKeys().Order(StringComparer.Ordinal));

            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(DesignTokens.Resolve("RadiusControl", variant), DesignTokens.Resolve("ControlCornerRadius", variant));
                Assert.Equal(DesignTokens.Resolve("RadiusPanel", variant), DesignTokens.Resolve("OverlayCornerRadius", variant));
            }
        }

        [AvaloniaFact]
        public void ThePaletteCovers_EveryControlTypeTheAppPutsOnScreen()
        {
            // A tripwire on the scope: the file is deliberately not all of Fluent, but it has to be
            // every control the views actually use. One key per family is enough to prove the
            // family was not dropped wholesale.
            var keys = AllControlKeys().ToHashSet(StringComparer.Ordinal);

            foreach (var key in new[]
            {
                "ButtonBackground",
                "RepeatButtonBackground",
                "AccentButtonBackground",
                "ComboBoxBackground",
                "ComboBoxItemBackgroundSelected",
                "TextControlBackground",
                "CheckBoxCheckBackgroundFillChecked",
                "RadioButtonOuterEllipseCheckedFill",
                "SliderTrackValueFill",
                "ToggleSwitchFillOn",
                "ExpanderHeaderBackground",
                "ScrollBarTrackFill",
                "MenuFlyoutItemForeground",
                "FlyoutPresenterBackground",
                "ToolTipBackground",
                "SystemControlHighlightListLowBrush"
            })
            {
                Assert.Contains(key, keys);
            }

            Assert.True(keys.Count >= 180, $"Only {keys.Count} control keys are mapped.");
        }

        private static IReadOnlyList<string> AllControlKeys()
        {
            return ControlPalette.DeclaredKeys(ThemeVariant.Dark)
                .Union(ControlPalette.DeclaredKeys(ThemeVariant.Light), StringComparer.Ordinal)
                .Union(ControlPalette.ThemeIndependentKeys(), StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
