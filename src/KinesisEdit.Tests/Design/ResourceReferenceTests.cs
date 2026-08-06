using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Every resource key the app's XAML looks up has to resolve — in both variants.
    /// <para>
    /// This is the typo guard, and it has to read the markup because Avalonia will not complain:
    /// a <c>{DynamicResource}</c> naming a key nobody defined resolves to nothing at all, leaves
    /// the property at its default and renders a control that merely looks a little wrong. A
    /// rendering test cannot see that; a broken <c>{StaticResource}</c>, by contrast, throws while
    /// the XAML loads and is caught by the render smoke.
    /// </para>
    /// </summary>
    public partial class ResourceReferenceTests
    {
        [AvaloniaFact]
        public void EveryResourceKeyUsedByAView_ResolvesInBothVariants()
        {
            var failures = new List<string>();

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                var declaredHere = DeclaredKeysIn(xaml);

                foreach (var key in AuthoredXaml.ResourceKeysIn(xaml))
                {
                    // A key the file declares itself lives in that control's own resources and is
                    // deliberately invisible to the application.
                    if (declaredHere.Contains(key))
                    {
                        continue;
                    }

                    foreach (var variant in DesignTokens.Variants)
                    {
                        if (!DesignTokens.TryResolve(key, variant, out _))
                        {
                            failures.Add($"{path}: '{key}' does not resolve in {variant}");
                        }
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [AvaloniaFact]
        public void EveryViewOfTheApp_IsVisibleToThisGuard()
        {
            // The guard is only worth anything if the sources actually reached the test assembly.
            var files = AuthoredXaml.Files();

            Assert.Contains("App.axaml", files.Keys);
            Assert.Contains("Themes/Tokens.axaml", files.Keys);
            Assert.Contains("Themes/Controls.axaml", files.Keys);
            Assert.Contains("Views/KeyboardEditorView.axaml", files.Keys);
            Assert.Contains("Controls/KeyCapView.axaml", files.Keys);
            Assert.True(files.Count >= 20, $"Only {files.Count} XAML sources are embedded.");
        }

        [AvaloniaFact]
        public void TheAliasFormOfAStaticResource_IsVisibleToThisGuard()
        {
            // Themes/Controls.axaml is written entirely in the element form,
            // <StaticResource x:Key="ButtonBackground" ResourceKey="SurfaceBarBrush" />, which the
            // markup-extension pattern cannot see. Without this the whole control palette would sit
            // outside the resolution guard above and a mistyped role would resolve to nothing.
            var keys = AuthoredXaml.ResourceKeysIn(AuthoredXaml.Files()["Themes/Controls.axaml"]);

            Assert.Contains("SurfaceBarBrush", keys);
            Assert.Contains("AccentTextBrush", keys);
            Assert.Contains("RadiusControl", keys);
        }

        [AvaloniaFact]
        public void NoView_HardcodesAHexColour()
        {
            // "Never hardcode a hex in a view" (docs/design/handoff.md). The lighting picker is the
            // one legitimate exception and does not do it in markup: its swatches are painted from
            // LED data through HexColorToBrush, which is a converter, not a literal.
            var failures = new List<string>();

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                // Themes/ is where the hexes belong: those files are the token definitions.
                if (path.StartsWith("Themes/", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in HexLiteralPattern().Matches(AuthoredXaml.WithoutComments(xaml)))
                {
                    failures.Add($"{path}: {match.Value}");
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        }

        [AvaloniaFact]
        public void NoView_PaintsDemoModeOnTheAdvisoryRamp()
        {
            // "Amber is the *only* warning color" (docs/design/), so amber means advisory and
            // nothing else. Demo mode is a different state — nothing is written — and has its own
            // role. This reads the markup because the mistake is a class name in a static
            // `Classes=` list: it resolves, it renders, and it is simply the wrong colour, which no
            // rendering test can notice on its own.
            var failures = new List<string>();
            var found = 0;

            foreach (var (path, xaml) in AuthoredXaml.Files())
            {
                foreach (Match match in DemoModeElementPattern().Matches(AuthoredXaml.WithoutComments(xaml)))
                {
                    found++;

                    if (match.Value.Contains("statusWarning", StringComparison.Ordinal))
                    {
                        failures.Add($"{path}: a DemoModeIndicator element carries statusWarning");
                    }
                }
            }

            Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

            // A guard that matched nothing would pass for the wrong reason. Two views author a demo
            // pill: the placeholder and the pedal editor. The keyboard editor's went with the
            // redesigned chrome — its toolbar's status chip and its Demo Mode bar say it instead,
            // and both are bound rather than naming the indicator string.
            Assert.Equal(2, found);
        }

        /// <summary>
        /// One XML element that renders <c>MainWindowViewModel.DemoModeIndicator</c>, from its
        /// opening angle bracket to its closing one, so the scan can see the element's own
        /// <c>Classes</c> without reaching into its neighbours.
        /// </summary>
        [GeneratedRegex(@"<[^<>]*DemoModeIndicator[^<>]*>", RegexOptions.Singleline)]
        private static partial Regex DemoModeElementPattern();

        /// <summary>A colour literal in an attribute value: <c>Foo="#RRGGBB"</c>.</summary>
        [GeneratedRegex("=\"#[0-9A-Fa-f]{3,8}\"")]
        private static partial Regex HexLiteralPattern();

        /// <summary><c>x:Key="Name"</c> — what this file puts into its own resource scope.</summary>
        [GeneratedRegex("x:Key=\"(?<key>[^\"]+)\"")]
        private static partial Regex DeclarationPattern();

        private static HashSet<string> DeclaredKeysIn(string xaml)
        {
            return DeclarationPattern()
                .Matches(xaml)
                .Select(match => match.Groups["key"].Value)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
