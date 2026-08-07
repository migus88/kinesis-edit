using System.Diagnostics.CodeAnalysis;

namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// The authored physical picture of one board: every key position placed in key
    /// units, plus the overall bounds.
    /// <para>
    /// One visual is shared by <b>all</b> of a device's layers. Layers differ only in
    /// the tokens bound to a position (spec 05 §7.4: identical positions share the same
    /// index across layers), never in where the key physically sits — so the keyboard
    /// view renders the same rectangles for every layer and swaps only the captions.
    /// </para>
    /// <para>
    /// <see cref="Keys"/> is the flat authoring-order list and is what arrow navigation
    /// (<see cref="KeyAdjacency"/>) and the view-model join work from. <see cref="Sections"/>
    /// is the same set grouped into drawn panels; the two are views of one list, never two
    /// lists that could disagree.
    /// </para>
    /// </summary>
    public sealed class KeyboardVisual
    {
        /// <summary>
        /// Layout variant this visual was authored for, matching
        /// <see cref="DeviceGeometry.Variant"/>. Only the Advantage2 ships two variants.
        /// </summary>
        public LayoutVariant Variant { get; }

        /// <summary>Key placements in authoring order. Indices are unique, not necessarily sorted.</summary>
        public IReadOnlyList<KeyVisual> Keys { get; }

        /// <summary>
        /// The board's drawn panels, ordered by <see cref="KeyboardSection.Index"/>. Every
        /// key of <see cref="Keys"/> appears in exactly one section, and the indices are
        /// dense from 0 — a gap is an authoring bug and throws, like a duplicate key index.
        /// </summary>
        public IReadOnlyList<KeyboardSection> Sections { get; }

        /// <summary>Board width in key units: the largest <see cref="KeyVisual.Right"/>; 0 when empty.</summary>
        public double Width { get; }

        /// <summary>Board height in key units: the largest <see cref="KeyVisual.Bottom"/>; 0 when empty.</summary>
        public double Height { get; }

        private readonly IReadOnlyDictionary<int, KeyVisual> _keysByIndex;

        public KeyboardVisual(LayoutVariant variant, IReadOnlyList<KeyVisual> keys)
        {
            ArgumentNullException.ThrowIfNull(keys);

            var copy = new KeyVisual[keys.Count];
            var keysByIndex = new Dictionary<int, KeyVisual>(keys.Count);
            var width = 0.0;
            var height = 0.0;

            for (var i = 0; i < keys.Count; i++)
            {
                var key = keys[i];

                if (key is null)
                {
                    throw new ArgumentException($"Key at list offset {i} is null.", nameof(keys));
                }

                if (!keysByIndex.TryAdd(key.Index, key))
                {
                    throw new ArgumentException($"Key index {key.Index} appears more than once.", nameof(keys));
                }

                copy[i] = key;
                width = Math.Max(width, key.Right);
                height = Math.Max(height, key.Bottom);
            }

            Variant = variant;
            Keys = copy;
            Sections = BuildSections(copy);
            Width = width;
            Height = height;
            _keysByIndex = keysByIndex;
        }

        /// <summary>
        /// Looks up the placement of a logical key position by its
        /// <see cref="KeyPosition.Index"/>.
        /// </summary>
        public bool TryGetKey(int index, [NotNullWhen(true)] out KeyVisual? key)
        {
            return _keysByIndex.TryGetValue(index, out key);
        }

        /// <summary>
        /// Groups the keys by <see cref="KeyVisual.Section"/> and measures each group's
        /// board-absolute bounding box, preserving authoring order inside a section.
        /// </summary>
        private static IReadOnlyList<KeyboardSection> BuildSections(IReadOnlyList<KeyVisual> keys)
        {
            if (keys.Count == 0)
            {
                return Array.Empty<KeyboardSection>();
            }

            var grouped = new SortedDictionary<int, List<KeyVisual>>();

            foreach (var key in keys)
            {
                if (!grouped.TryGetValue(key.Section, out var members))
                {
                    members = new List<KeyVisual>();
                    grouped.Add(key.Section, members);
                }

                members.Add(key);
            }

            var sections = new KeyboardSection[grouped.Count];
            var expectedIndex = 0;

            foreach (var (index, members) in grouped)
            {
                if (index != expectedIndex)
                {
                    throw new ArgumentException(
                        $"Section indices must be dense from 0; section {expectedIndex} has no keys.",
                        nameof(keys));
                }

                sections[expectedIndex] = Measure(index, members);
                expectedIndex++;
            }

            return sections;
        }

        private static KeyboardSection Measure(int index, IReadOnlyList<KeyVisual> members)
        {
            var left = double.MaxValue;
            var top = double.MaxValue;
            var right = double.MinValue;
            var bottom = double.MinValue;

            foreach (var key in members)
            {
                left = Math.Min(left, key.X);
                top = Math.Min(top, key.Y);
                right = Math.Max(right, key.Right);
                bottom = Math.Max(bottom, key.Bottom);
            }

            return new KeyboardSection(index, members.ToArray(), left, top, right - left, bottom - top);
        }
    }
}
