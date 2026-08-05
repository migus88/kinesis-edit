namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// The held-key bookkeeping behind <see cref="KeystrokeRecorder"/>: which physical positions
    /// are currently down, in press order, and which of them have already taken part in a
    /// combination and can therefore no longer count as "tapped alone"
    /// (specs/12-savant-elite.md §"Programming a pedal" step 3).
    /// <para>
    /// Every held position is tracked, not only modifiers — the recorder needs that to suppress
    /// auto-repeat of ordinary keys (specs/10-apps-and-ui.md, "Keyboard-input capture") and to
    /// avoid swallowing a key-up for a key it never recorded.
    /// </para>
    /// </summary>
    internal sealed class HeldKeySet
    {
        /// <summary>The positions currently held, in press order.</summary>
        public IReadOnlyList<PhysicalKeyCode> Positions
        {
            get => _positions;
        }

        private readonly List<PhysicalKeyCode> _positions = new();
        private readonly HashSet<PhysicalKeyCode> _consumedPositions = new();

        /// <summary>Whether <paramref name="key"/> is currently held.</summary>
        public bool Contains(PhysicalKeyCode key)
        {
            return _positions.Contains(key);
        }

        /// <summary>
        /// Whether any position other than <paramref name="key"/> is currently held — the test
        /// for "this key press joins a combination rather than standing alone".
        /// </summary>
        public bool ContainsAnyOtherThan(PhysicalKeyCode key)
        {
            foreach (var position in _positions)
            {
                if (position != key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Marks <paramref name="key"/> as held. A position already held is left untouched —
        /// auto-repeat must neither duplicate it nor clear its consumed flag, or a modifier that
        /// took part in a combination would resurrect as a lone tap while still physically down.
        /// A genuinely fresh press starts unconsumed, which is what lets a modifier re-pressed
        /// after a combination register as tapped alone again.
        /// </summary>
        public void Hold(PhysicalKeyCode key)
        {
            if (_positions.Contains(key))
            {
                return;
            }

            _positions.Add(key);
            _consumedPositions.Remove(key);
        }

        /// <summary>
        /// Releases <paramref name="key"/> and reports whether it was held at all;
        /// <paramref name="wasConsumed"/> tells whether it had taken part in a combination.
        /// </summary>
        public bool Release(PhysicalKeyCode key, out bool wasConsumed)
        {
            wasConsumed = _consumedPositions.Remove(key);

            return _positions.Remove(key);
        }

        /// <summary>
        /// Marks every currently held position as consumed — none of them can still register as
        /// tapped alone once another key press has joined them.
        /// </summary>
        public void MarkAllConsumed()
        {
            foreach (var position in _positions)
            {
                _consumedPositions.Add(position);
            }
        }

        /// <summary>Forgets every held position and its consumed flag.</summary>
        public void Clear()
        {
            _positions.Clear();
            _consumedPositions.Clear();
        }
    }
}
