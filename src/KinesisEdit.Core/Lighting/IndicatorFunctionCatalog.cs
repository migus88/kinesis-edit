using System.Collections.ObjectModel;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The immutable catalog of the eight Advantage360 indicator functions and their file
    /// tokens, encoding the function table of specs/07-lighting.md §5. Pure data — parsing
    /// lives in <see cref="LedFileParser"/>.
    /// </summary>
    public static class IndicatorFunctionCatalog
    {
        /// <summary>All function rows in spec table order.</summary>
        public static IReadOnlyList<IndicatorFunctionDefinition> All { get; }

        private static readonly Dictionary<IndicatorFunction, IndicatorFunctionDefinition> _byFunction;
        private static readonly Dictionary<string, IndicatorTokenMatch> _byToken;

        static IndicatorFunctionCatalog()
        {
            All = new ReadOnlyCollection<IndicatorFunctionDefinition>(BuildDefinitions());

            _byFunction = new Dictionary<IndicatorFunction, IndicatorFunctionDefinition>(All.Count);
            _byToken = new Dictionary<string, IndicatorTokenMatch>(StringComparer.OrdinalIgnoreCase);

            foreach (var definition in All)
            {
                _byFunction.Add(definition.Function, definition);

                for (var tokenIndex = 0; tokenIndex < definition.Tokens.Count; tokenIndex++)
                {
                    _byToken.Add(definition.Tokens[tokenIndex], new IndicatorTokenMatch
                    {
                        Definition = definition,
                        Layer = (IndicatorLayer)tokenIndex
                    });
                }
            }
        }

        /// <summary>Returns the catalog row for <paramref name="function"/>.</summary>
        public static IndicatorFunctionDefinition Find(IndicatorFunction function)
        {
            return _byFunction.TryGetValue(function, out var definition)
                ? definition
                : throw new ArgumentOutOfRangeException(nameof(function), function, "Unknown indicator function.");
        }

        /// <summary>
        /// Resolves a function token case-insensitively to its row and addressed layer, or null
        /// when the token is not an indicator function token (§5).
        /// </summary>
        public static IndicatorTokenMatch? FindByToken(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return _byToken.GetValueOrDefault(token);
        }

        private static List<IndicatorFunctionDefinition> BuildDefinitions()
        {
            return
            [
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.NkroMode,
                    Tokens = ["nkro"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.ScrollLock,
                    Tokens = ["sclk"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.NumLock,
                    Tokens = ["nmlk"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.CapsLock,
                    Tokens = ["caps"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.Layer,
                    Tokens = ["layd", "layk", "lay1", "lay2", "lay3"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.Profile,
                    Tokens = ["prof"],
                    HasColor = true
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.Battery,
                    Tokens = ["batt"],
                    HasColor = false
                },
                new IndicatorFunctionDefinition
                {
                    Function = IndicatorFunction.Disabled,
                    Tokens = ["null"],
                    HasColor = false
                }
            ];
        }
    }
}
