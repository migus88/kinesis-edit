using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the Macros tab's <c>All layers ▾</c> filter (mockup <c>1i</c>, right).
    /// <see cref="LayerIndex"/> is null for the "everything" row, which is the default and is always
    /// first.
    /// <para>
    /// Plain immutable data rather than a view model: it carries no state that moves and raises
    /// nothing, so it is built with the filter list and thrown away with it.
    /// </para>
    /// </summary>
    public sealed record MacroLayerFilter
    {
        /// <summary>The "everything" row's caption, verbatim from mockup <c>1i</c>.</summary>
        public const string AllLayersCaption = "All layers";

        /// <summary>The layer this row narrows to, or null for every layer.</summary>
        public int? LayerIndex { get; init; }

        /// <summary>What the row reads.</summary>
        public required string Caption { get; init; }

        /// <summary>The "everything" row.</summary>
        public static MacroLayerFilter All()
        {
            return new MacroLayerFilter { Caption = AllLayersCaption };
        }

        /// <summary>Every filter row for <paramref name="layout"/>: "All layers", then each layer.</summary>
        public static IReadOnlyList<MacroLayerFilter> BuildAll(KeyboardLayout? layout)
        {
            var filters = new List<MacroLayerFilter> { All() };

            if (layout is null)
            {
                return filters;
            }

            foreach (var layer in layout.Layers)
            {
                filters.Add(new MacroLayerFilter
                {
                    LayerIndex = layer.Index,
                    Caption = LayerCaptions.ForLayer(layer, layout.Dialect)
                });
            }

            return filters;
        }
    }
}
