using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// Reaches the design system's token dictionaries two ways, because the suites need both.
    /// <see cref="DeclaredKeys"/> reads <c>Themes/Tokens.axaml</c> directly, which is the only way
    /// to see what a theme variant <i>declares</i> — the resource system can only be asked whether
    /// a key it is given resolves. <see cref="TryResolve"/> then asks the live
    /// <see cref="Application"/>, which is what a view actually does, so a token that is declared
    /// but never merged still fails.
    /// </summary>
    internal static class DesignTokens
    {
        /// <summary>The token dictionary, as the app's <c>App.axaml</c> names it.</summary>
        public const string TokensSource = "avares://KinesisEdit/Themes/Tokens.axaml";

        /// <summary>Suffix of the <c>Color</c> half of a token pair.</summary>
        public const string ColorSuffix = "Color";

        /// <summary>Suffix of the <c>Brush</c> half of a token pair.</summary>
        public const string BrushSuffix = "Brush";

        /// <summary>Both variants the app ships. Every token exists in both.</summary>
        public static readonly ThemeVariant[] Variants = [ThemeVariant.Dark, ThemeVariant.Light];

        /// <summary>The keys <c>Tokens.axaml</c> declares under <paramref name="variant"/>.</summary>
        public static IReadOnlyList<string> DeclaredKeys(ThemeVariant variant)
        {
            ArgumentNullException.ThrowIfNull(variant);

            var tokens = LoadTokenDictionary();

            if (!tokens.ThemeDictionaries.TryGetValue(variant, out var provider))
            {
                throw new InvalidOperationException(
                    $"Tokens.axaml declares no '{variant.Key}' theme dictionary.");
            }

            if (provider is not ResourceDictionary dictionary)
            {
                throw new InvalidOperationException(
                    $"The '{variant.Key}' theme dictionary is a {provider.GetType().Name}, not a ResourceDictionary.");
            }

            return dictionary.Keys.Select(key => key.ToString() ?? string.Empty).ToArray();
        }

        /// <summary>Loads <c>Themes/Tokens.axaml</c> on its own, outside the application's merge.</summary>
        public static ResourceDictionary LoadTokenDictionary()
        {
            return (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(TokensSource));
        }

        /// <summary>
        /// Resolves <paramref name="key"/> through the running application under
        /// <paramref name="variant"/> — the same lookup a <c>DynamicResource</c> in a view performs.
        /// </summary>
        public static bool TryResolve(string key, ThemeVariant variant, out object? value)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(variant);

            var application = Application.Current
                ?? throw new InvalidOperationException("No Avalonia application is running; use [AvaloniaFact].");

            return application.TryGetResource(key, variant, out value) && value is not null;
        }

        /// <summary>Resolves <paramref name="key"/>, failing the test if it does not.</summary>
        public static object Resolve(string key, ThemeVariant variant)
        {
            Assert.True(TryResolve(key, variant, out var value), $"'{key}' does not resolve in the {variant} theme.");

            return value!;
        }

        /// <summary>Resolves a <c>&lt;Role&gt;Color</c> token.</summary>
        public static Color ResolveColor(string key, ThemeVariant variant)
        {
            var value = Resolve(key, variant);

            Assert.True(value is Color, $"'{key}' resolves to a {value.GetType().Name} in the {variant} theme, not a Color.");

            return (Color)value;
        }

        /// <summary>Resolves a <c>&lt;Role&gt;Brush</c> token and returns the colour it paints.</summary>
        public static Color ResolveBrushColor(string key, ThemeVariant variant)
        {
            var value = Resolve(key, variant);

            Assert.True(
                value is ISolidColorBrush,
                $"'{key}' resolves to a {value.GetType().Name} in the {variant} theme, not a SolidColorBrush.");

            return ((ISolidColorBrush)value).Color;
        }
    }
}
