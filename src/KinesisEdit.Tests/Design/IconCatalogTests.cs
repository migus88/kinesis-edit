using System.Text.RegularExpressions;
using Avalonia.Headless.XUnit;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The structural guard over the three geometry dictionaries: every mark they declare reaches
    /// the application, carries its family's name, and is nothing but a shape.
    /// <para>
    /// The keys are read from the files themselves and then resolved through the running
    /// application, because those are two different questions. A dictionary that is written but
    /// never merged into <c>App.axaml</c> resolves to nothing at all — Avalonia does not complain
    /// about a <c>{DynamicResource}</c> naming a key nobody defined, it simply leaves the property
    /// at its default and draws an icon-shaped hole.
    /// </para>
    /// </summary>
    public partial class IconCatalogTests
    {
        /// <summary>
        /// The state, action and chrome marks the design names, as the whole of Themes/Icons.axaml.
        /// A tripwire rather than a restatement: a mark dropped from the set would otherwise fail
        /// only wherever it happened to be used.
        /// </summary>
        private static readonly string[] _stateAndActionMarks =
        [
            // The four direction arrows share this file with the state and action marks rather than
            // forming a family of their own: they are chrome, drawn under one pen at one size, and a
            // fourth dictionary would buy nothing but a fourth prefix. Their coverage is driven by
            // the LightingDirection enum in IconCoverageTests.
            "IconArrowDown",
            "IconArrowLeft",
            "IconArrowRight",
            "IconArrowUp",
            "IconCannotAccess",
            "IconClose",
            "IconConfirmation",
            "IconConnected",
            "IconConnectedCore",
            "IconEject",
            "IconError",
            "IconExternalLink",
            "IconInformation",
            "IconNotDetected",
            "IconOption",
            "IconRefresh",
            "IconScanning",
            "IconWarning"
        ];

        /// <summary>The three dictionaries, by the path AuthoredXaml lists them under.</summary>
        public static TheoryData<string> Families()
        {
            var families = new TheoryData<string>();

            foreach (var family in IconCatalog.Families)
            {
                families.Add(family.XamlPath);
            }

            return families;
        }

        [AvaloniaTheory]
        [MemberData(nameof(Families))]
        public void EveryDeclaredMark_ResolvesAsAGeometryInBothVariants(string xamlPath)
        {
            var family = IconCatalog.FamilyOf(xamlPath);
            var keys = IconCatalog.DeclaredKeys(family);

            Assert.NotEmpty(keys);

            foreach (var variant in DesignTokens.Variants)
            {
                foreach (var key in keys)
                {
                    IconCatalog.ResolveGeometry(key, variant);
                }
            }
        }

        [AvaloniaTheory]
        [MemberData(nameof(Families))]
        public void EveryDeclaredMark_CarriesItsFamilysPrefix(string xamlPath)
        {
            var family = IconCatalog.FamilyOf(xamlPath);

            var strays = IconCatalog.DeclaredKeys(family)
                .Where(key => !key.StartsWith(family.Prefix, StringComparison.Ordinal))
                .ToArray();

            Assert.True(strays.Length == 0, $"{xamlPath} declares [{string.Join(", ", strays)}], which do not begin '{family.Prefix}'.");
        }

        [AvaloniaFact]
        public void TheCatalog_DeclaresNoKeyTwice()
        {
            // The three dictionaries are merged into one scope, so a key declared in two of them
            // would silently resolve to whichever was merged last.
            var duplicates = IconCatalog.AllKeys()
                .GroupBy(key => key, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            Assert.True(duplicates.Length == 0, $"Declared more than once: {string.Join(", ", duplicates)}.");
        }

        [AvaloniaFact]
        public void TheStateAndActionMarks_AreTheOnesTheDesignNames()
        {
            Assert.Equal(_stateAndActionMarks, IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/Icons.axaml")));
        }

        [AvaloniaTheory]
        [MemberData(nameof(Families))]
        public void NoMarkDictionary_DeclaresAColour(string xamlPath)
        {
            // "A geometry is a shape; the call site names the token it is painted with." These files
            // live under Themes/, where the hexes belong, so ResourceReferenceTests deliberately
            // skips them — but a colour baked into a mark cannot be re-themed, cannot be recoloured
            // for a selected row, and would be a bug even here.
            var markup = AuthoredXaml.WithoutComments(AuthoredXaml.Files()[xamlPath]);
            var failures = new List<string>();

            foreach (var word in new[] { "Color", "Brush" })
            {
                if (markup.Contains(word, StringComparison.Ordinal))
                {
                    failures.Add(word);
                }
            }

            foreach (Match match in HexLiteralPattern().Matches(markup))
            {
                failures.Add(match.Value);
            }

            Assert.True(failures.Count == 0, $"{xamlPath} names colour: {string.Join(", ", failures)}.");
        }

        [AvaloniaTheory]
        [MemberData(nameof(Families))]
        public void EveryMarkDictionary_IsMergedIntoTheApplicationsResources(string xamlPath)
        {
            var family = IconCatalog.FamilyOf(xamlPath);
            var app = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["App.axaml"]);

            Assert.Contains($"ResourceInclude Source=\"{family.Source}\"", app, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void BothIconFiles_AreVisibleToTheseGuards()
        {
            // Themes/Icons.axaml (the marks) and Styles/Icons.axaml (the spinner class) share a
            // file name; a guard that silently read one of them twice would prove nothing.
            var files = AuthoredXaml.Files();

            Assert.Contains("Themes/Icons.axaml", files.Keys);
            Assert.Contains("Themes/DeviceArt.axaml", files.Keys);
            Assert.Contains("Themes/LightingMarks.axaml", files.Keys);
            Assert.Contains("Styles/Icons.axaml", files.Keys);
            Assert.NotEqual(files["Themes/Icons.axaml"], files["Styles/Icons.axaml"]);
        }

        /// <summary>A colour literal, in an attribute or in element content.</summary>
        [GeneratedRegex("#[0-9A-Fa-f]{3,8}")]
        private static partial Regex HexLiteralPattern();
    }
}
