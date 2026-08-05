namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Mutable helper for composing ordered <see cref="KeyPosition"/> lists, either
    /// from scratch (indices assigned densely in append order) or as an overlay on an
    /// existing layer — specs/05-key-model.md §4 defines several layers as deltas
    /// against another layer (RGB vs Edge, keypad layers vs top, Dvorak vs QWERTY,
    /// Advantage360 Keypad/Fn vs Base).
    /// </summary>
    internal sealed class LayerBuilder
    {
        private readonly List<KeyPosition> _keys;

        private LayerBuilder(List<KeyPosition> keys)
        {
            _keys = keys;
        }

        public static LayerBuilder Start()
        {
            return new LayerBuilder(new List<KeyPosition>());
        }

        public static LayerBuilder StartFrom(IReadOnlyList<KeyPosition> source)
        {
            return new LayerBuilder(new List<KeyPosition>(source));
        }

        public LayerBuilder Key(string defaultToken)
        {
            return Append(new KeyPosition(_keys.Count, defaultToken));
        }

        public LayerBuilder Key(string defaultToken, string positionToken)
        {
            return Append(new KeyPosition(_keys.Count, defaultToken, positionToken));
        }

        public LayerBuilder Keys(params string[] defaultTokens)
        {
            foreach (var defaultToken in defaultTokens)
            {
                Key(defaultToken);
            }

            return this;
        }

        public LayerBuilder Modifier(string defaultToken)
        {
            return Append(new KeyPosition(_keys.Count, defaultToken, canAssignMacro: false));
        }

        public LayerBuilder Locked(string defaultToken)
        {
            return Append(new KeyPosition(_keys.Count, defaultToken, canEdit: false, canAssignMacro: false));
        }

        public LayerBuilder HostDependentKey(string defaultToken, string masterAppDefaultToken)
        {
            return Append(new KeyPosition(_keys.Count, defaultToken, masterAppDefaultToken: masterAppDefaultToken));
        }

        public LayerBuilder Override(int index, string defaultToken)
        {
            return Replace(new KeyPosition(index, defaultToken));
        }

        public LayerBuilder Override(int index, string defaultToken, string positionToken)
        {
            return Replace(new KeyPosition(index, defaultToken, positionToken));
        }

        public LayerBuilder OverrideModifier(int index, string defaultToken)
        {
            return Replace(new KeyPosition(index, defaultToken, canAssignMacro: false));
        }

        public IReadOnlyList<KeyPosition> Build()
        {
            return _keys.ToArray();
        }

        private LayerBuilder Append(KeyPosition position)
        {
            _keys.Add(position);
            return this;
        }

        private LayerBuilder Replace(KeyPosition position)
        {
            _keys[position.Index] = position;
            return this;
        }
    }
}
