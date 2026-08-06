using System.Reflection;
using System.Text.RegularExpressions;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The app's XAML sources, embedded into this assembly by the project file (see the
    /// <c>EmbeddedResource</c> item group) and read back as text.
    /// <para>
    /// This exists because the app compiles its XAML: the markup is gone from the built assembly,
    /// so the only way a test can check what a view <i>asks</i> for is to look at the source. The
    /// resource-key guard needs exactly that — Avalonia resolves a missing
    /// <c>{DynamicResource}</c> to nothing at all rather than throwing, so a typo is invisible to
    /// a rendering test and visible only here.
    /// </para>
    /// </summary>
    internal static partial class AuthoredXaml
    {
        private const string ResourcePrefix = "KinesisEdit.Tests.Xaml.";

        private const string XamlExtension = ".axaml";

        /// <summary>Every authored XAML file, keyed by a readable path such as <c>Views/ToastView.axaml</c>.</summary>
        public static IReadOnlyDictionary<string, string> Files()
        {
            var assembly = typeof(AuthoredXaml).Assembly;
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                    || !resourceName.EndsWith(XamlExtension, StringComparison.Ordinal))
                {
                    continue;
                }

                files[ToDisplayPath(resourceName)] = ReadResource(assembly, resourceName);
            }

            if (files.Count == 0)
            {
                throw new InvalidOperationException(
                    "No XAML sources are embedded in the test assembly; check the EmbeddedResource item group.");
            }

            return files;
        }

        /// <summary>
        /// <paramref name="xaml"/> with its XML comments removed. Every scan runs on this: these
        /// files are heavily commented, and the comments quote the very markup they explain, so a
        /// raw search finds the explanation as readily as the code.
        /// </summary>
        public static string WithoutComments(string xaml)
        {
            ArgumentNullException.ThrowIfNull(xaml);

            return CommentPattern().Replace(xaml, string.Empty);
        }

        /// <summary>
        /// Every resource key <paramref name="xaml"/> looks up, in all three forms the app writes:
        /// the <c>{DynamicResource X}</c> and <c>{StaticResource X}</c> markup extensions, and the
        /// <c>&lt;StaticResource ResourceKey="X" /&gt;</c> element that Themes/Controls.axaml uses
        /// to alias a Fluent control key onto a role.
        /// </summary>
        public static IReadOnlyList<string> ResourceKeysIn(string xaml)
        {
            ArgumentNullException.ThrowIfNull(xaml);

            var body = WithoutComments(xaml);

            return ResourceReferencePattern()
                .Matches(body)
                .Concat(ResourceAliasPattern().Matches(body))
                .Select(match => match.Groups["key"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// <c>{DynamicResource Key}</c> / <c>{StaticResource Key}</c>, in the plain positional form
        /// this app writes them in. A key is a XAML name, so it cannot contain a brace, a comma or
        /// whitespace, which is what bounds the match.
        /// </summary>
        [GeneratedRegex(@"\{(?:Dynamic|Static)Resource\s+(?<key>[^\s},]+)\s*\}")]
        private static partial Regex ResourceReferencePattern();

        /// <summary>
        /// The element form, <c>&lt;StaticResource x:Key="Alias" ResourceKey="Role" /&gt;</c>. The
        /// markup-extension pattern above cannot see it, and it is the whole of Controls.axaml.
        /// </summary>
        [GeneratedRegex(@"ResourceKey=""(?<key>[^""]+)""")]
        private static partial Regex ResourceAliasPattern();

        /// <summary>An XML comment, including the multi-line ones these files are full of.</summary>
        [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
        private static partial Regex CommentPattern();

        /// <summary>
        /// Turns the flattened manifest name back into a path. The project links the files under
        /// <c>Xaml</c>, so every dot after the prefix is a directory separator except the one
        /// before the extension.
        /// </summary>
        private static string ToDisplayPath(string resourceName)
        {
            var relative = resourceName[ResourcePrefix.Length..^XamlExtension.Length];

            return relative.Replace('.', '/') + XamlExtension;
        }

        private static string ReadResource(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' could not be opened.");

            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
