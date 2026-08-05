namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// The Advantage360 layer header lines of specs/04-layout-file-format.md §3.3: the file is
    /// partitioned by standalone <c>&lt;...&gt;</c> lines, exact-match; every rule belongs to
    /// the last header seen, Base before the first one. Mapping the header to the geometry's
    /// layer index (Base 0 … Fn3 4, specs/05-key-model.md §4.7) is this table's job.
    /// </summary>
    internal static class Gen2LayerHeaders
    {
        /// <summary>The header line of each layer index 0..4, in layer order (04 §3.3).</summary>
        public static IReadOnlyList<string> Lines { get; } =
        [
            "<base>",
            "<keypad>",
            "<function1>",
            "<function2>",
            "<function3>"
        ];

        /// <summary>
        /// Matches an already-lowercased line against the five known headers (04 §3.3). False
        /// means an unknown header — an invalid line, never a layer switch.
        /// </summary>
        public static bool TryGetLayerIndex(string line, out int layerIndex)
        {
            for (var i = 0; i < Lines.Count; i++)
            {
                if (string.Equals(Lines[i], line, StringComparison.Ordinal))
                {
                    layerIndex = i;
                    return true;
                }
            }

            layerIndex = 0;
            return false;
        }
    }
}
