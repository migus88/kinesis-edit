using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Reads the drawn half of the design system — <c>Themes/Icons.axaml</c>,
    /// <c>Themes/DeviceArt.axaml</c> and <c>Themes/LightingMarks.axaml</c> — the same two ways
    /// <see cref="DesignTokens"/> reads the colour tokens.
    /// <para>
    /// <see cref="DeclaredKeys"/> loads each dictionary on its own, because that is the only way to
    /// see what a file <i>declares</i>: the resource system can only answer about a key it is
    /// handed, so a mark that exists in the file but never reaches the application's merge would
    /// otherwise be invisible. <see cref="ResolveGeometry"/> then asks the live
    /// <see cref="Application"/>, which is what a view does.
    /// </para>
    /// <para>
    /// <see cref="PathData"/> reads the markup itself, through <see cref="AuthoredXaml"/>. A
    /// <see cref="Geometry"/> does not surrender the path string it was parsed from, and the
    /// duplicate-shape guard needs to compare marks by what they were drawn as, not by a bounding
    /// box two different shapes can share.
    /// </para>
    /// </summary>
    internal static partial class IconCatalog
    {
        /// <summary>The state, action and chrome marks, as <c>App.axaml</c> names the dictionary.</summary>
        public const string IconsSource = "avares://KinesisEdit/Themes/Icons.axaml";

        /// <summary>The device silhouettes.</summary>
        public const string DeviceArtSource = "avares://KinesisEdit/Themes/DeviceArt.axaml";

        /// <summary>The lighting-mode marks.</summary>
        public const string LightingMarksSource = "avares://KinesisEdit/Themes/LightingMarks.axaml";

        /// <summary>The one mark authored off the 16px grid — see <see cref="BoxOf"/>.</summary>
        public const string ScanningKey = "IconScanning";

        /// <summary>The style class that carries the device-art box.</summary>
        public const string DeviceArtClass = "deviceArt";

        /// <summary>The style class that carries the scanning mark, its box and its rotation.</summary>
        public const string SpinnerClass = "spinner";

        /// <summary>The icon law's grid: "16px grid, 1.5px stroke, square caps, geometry only".</summary>
        public static Rect IconBox { get; } = new(0, 0, 16, 16);

        /// <summary>The spinner's own box; <c>SpinnerSize</c> is 14, and the arc is centred on (7,7).</summary>
        public static Rect SpinnerBox { get; } = new(0, 0, 14, 14);

        /// <summary>The one box every board is drawn in, so the seven are at true proportion.</summary>
        public static Rect DeviceArtBox { get; } = new(0, 0, 92, 56);

        /// <summary>The three dictionaries, each with the prefix every key in it must carry.</summary>
        public static IReadOnlyList<Family> Families { get; } =
        [
            new Family("Themes/Icons.axaml", IconsSource, "Icon"),
            new Family("Themes/DeviceArt.axaml", DeviceArtSource, "DeviceArt"),
            new Family("Themes/LightingMarks.axaml", LightingMarksSource, "LightingMark")
        ];

        /// <summary>The family whose XAML path is <paramref name="xamlPath"/>.</summary>
        public static Family FamilyOf(string xamlPath)
        {
            ArgumentNullException.ThrowIfNull(xamlPath);

            return Families.SingleOrDefault(family => family.XamlPath == xamlPath)
                ?? throw new InvalidOperationException($"'{xamlPath}' is not one of the catalog's dictionaries.");
        }

        /// <summary>The keys <paramref name="family"/> declares, read from the file itself.</summary>
        public static IReadOnlyList<string> DeclaredKeys(Family family)
        {
            ArgumentNullException.ThrowIfNull(family);

            return Load(family.Source)
                .Keys
                .Select(key => key.ToString() ?? string.Empty)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Every key the three dictionaries declare between them.</summary>
        public static IReadOnlyList<string> AllKeys()
        {
            return Families
                .SelectMany(DeclaredKeys)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>Loads one dictionary on its own, outside the application's merge.</summary>
        public static ResourceDictionary Load(string source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(source));
        }

        /// <summary>
        /// Resolves <paramref name="key"/> through the running application and asserts it is a
        /// mark — the lookup a view performs with <c>{DynamicResource}</c>.
        /// </summary>
        public static Geometry ResolveGeometry(string key, ThemeVariant variant)
        {
            var value = DesignTokens.Resolve(key, variant);

            Assert.True(
                value is Geometry,
                $"'{key}' resolves to a {value.GetType().Name} in the {variant} theme, not a Geometry.");

            return (Geometry)value;
        }

        /// <summary>
        /// The coordinate box <paramref name="key"/> was authored in. Everything is on the 16px
        /// grid except the device silhouettes, which share one 92x56 box, and the scanning mark,
        /// which is drawn at <c>SpinnerSize</c>.
        /// </summary>
        public static Rect BoxOf(string key)
        {
            ArgumentNullException.ThrowIfNull(key);

            if (key == ScanningKey)
            {
                return SpinnerBox;
            }

            return key.StartsWith("DeviceArt", StringComparison.Ordinal) ? DeviceArtBox : IconBox;
        }

        /// <summary>
        /// Every mark's authored path data, keyed by resource key and with its whitespace
        /// collapsed, so a multi-line silhouette compares as the one string it is.
        /// </summary>
        public static IReadOnlyDictionary<string, string> PathData()
        {
            var files = AuthoredXaml.Files();
            var data = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var family in Families)
            {
                var markup = AuthoredXaml.WithoutComments(files[family.XamlPath]);

                foreach (Match match in GeometryPattern().Matches(markup))
                {
                    data[match.Groups["key"].Value] = Normalize(match.Groups["data"].Value);
                }
            }

            return data;
        }

        /// <summary>A <c>&lt;StreamGeometry x:Key="Name"&gt;path&lt;/StreamGeometry&gt;</c> declaration.</summary>
        [GeneratedRegex(@"<StreamGeometry\s+x:Key=""(?<key>[^""]+)""\s*>(?<data>.*?)</StreamGeometry>", RegexOptions.Singleline)]
        private static partial Regex GeometryPattern();

        /// <summary>Runs of whitespace, which the silhouettes use to put one subpath per line.</summary>
        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespacePattern();

        private static string Normalize(string pathData)
        {
            return WhitespacePattern().Replace(pathData, " ").Trim();
        }

        /// <summary>One of the catalog's dictionaries and the naming rule its keys follow.</summary>
        /// <param name="XamlPath">The path <see cref="AuthoredXaml.Files"/> lists it under.</param>
        /// <param name="Source">The <c>avares://</c> URI <c>App.axaml</c> merges.</param>
        /// <param name="Prefix">The prefix every key in it carries.</param>
        internal sealed record Family(string XamlPath, string Source, string Prefix);
    }
}
