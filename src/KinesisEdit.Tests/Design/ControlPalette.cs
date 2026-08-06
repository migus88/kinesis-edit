using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Reads <c>Themes/Controls.axaml</c> — the alias map that puts the FluentTheme's own control
    /// keys onto the design system's roles. Like <see cref="DesignTokens"/> it reads the file
    /// directly, because the resource system can only answer questions about a key it is handed and
    /// these suites need to know what the file <i>declares</i>.
    /// </summary>
    internal static class ControlPalette
    {
        /// <summary>The control palette, as the app's <c>App.axaml</c> names it.</summary>
        public const string Source = "avares://KinesisEdit/Themes/Controls.axaml";

        /// <summary>The Fluent keys the palette redeclares under <paramref name="variant"/>.</summary>
        public static IReadOnlyList<string> DeclaredKeys(ThemeVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);

            var palette = Load();

            if (!palette.ThemeDictionaries.TryGetValue(variant, out var provider))
            {
                throw new InvalidOperationException($"Controls.axaml declares no '{variant.Key}' theme dictionary.");
            }

            if (provider is not ResourceDictionary dictionary)
            {
                throw new InvalidOperationException(
                    $"The '{variant.Key}' theme dictionary is a {provider.GetType().Name}, not a ResourceDictionary.");
            }

            return dictionary.Keys.Select(key => key.ToString() ?? string.Empty).ToArray();
        }

        /// <summary>The keys declared outside the variant dictionaries — the shape aliases.</summary>
        public static IReadOnlyList<string> ThemeIndependentKeys()
        {
            return Load().Keys.Select(key => key.ToString() ?? string.Empty).ToArray();
        }

        /// <summary>Loads <c>Themes/Controls.axaml</c> on its own, outside the application's merge.</summary>
        public static ResourceDictionary Load()
        {
            return (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(Source));
        }

        /// <summary>
        /// Every colour the design system declares under <paramref name="variant"/>, as the set a
        /// control key's resolved colour has to be a member of. Both halves of every token pair
        /// paint the same colour, so the set is the palette itself.
        /// </summary>
        public static IReadOnlyCollection<Color> TokenColors(ThemeVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);

            var colors = new HashSet<Color>();

            foreach (var role in DesignTokens.DeclaredKeys(variant))
            {
                if (!role.EndsWith(DesignTokens.ColorSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                colors.Add(DesignTokens.ResolveColor(role, variant));
            }

            return colors;
        }
    }
}
