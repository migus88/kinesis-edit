namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Spatial navigation over an authored board picture: given a key and a direction, which key
    /// should a user moving that way land on. Pure geometry over <see cref="KeyVisual"/>
    /// rectangles, so it works for every device whose visual is authored, split halves and
    /// staggered rows included.
    /// <para>
    /// <b>No row or column arithmetic.</b> A <see cref="KeyVisual"/> carries only X/Y/Width/Height
    /// in key units — there is no row index and no column index, and the boards do not have one:
    /// the Freestyle Edge RGB lays its hotkey column, left half, split gap and right half into one
    /// continuous coordinate space, and the right half's rows are staggered by a different offset
    /// per row. Bucketing keys into integer rows or columns therefore answers wrongly on exactly
    /// the rows that matter. The resolution is rectangle overlap plus centre distance instead, and
    /// crossing the split gap falls out of that scoring rather than being special-cased.
    /// </para>
    /// </summary>
    public static class KeyAdjacency
    {
        /// <summary>
        /// Slack for the double comparisons. Authored coordinates are quarter-unit multiples, so
        /// this only guards against a future board authored with untidy numbers.
        /// </summary>
        private const double Epsilon = 1e-6;

        /// <summary>
        /// Returns the key <paramref name="direction"/> of the key at <paramref name="fromIndex"/>,
        /// or null when nothing lies that way — at a board edge, and for an index the visual does
        /// not carry. A caller keeps its current selection on null.
        /// <para>
        /// Scoring, best first: a candidate must have its centre strictly on the correct side of
        /// the source's centre; candidates whose perpendicular span overlaps the source's span
        /// (the vertical span for Left/Right, the horizontal span for Up/Down) always beat those
        /// that do not; inside a group the smaller primary-axis centre distance wins, then the
        /// smaller perpendicular centre distance, then the lower <see cref="KeyVisual.Index"/>.
        /// The last tier makes the answer deterministic on symmetric boards, where two candidates
        /// are genuinely equidistant.
        /// </para>
        /// <para>
        /// The non-overlapping tier is <b>asymmetric by direction</b>, and deliberately so — see
        /// <see cref="TryScore"/>.
        /// </para>
        /// </summary>
        public static KeyVisual? Next(KeyboardVisual visual, int fromIndex, NavigationDirection direction)
        {
            ArgumentNullException.ThrowIfNull(visual);

            if (direction is not (NavigationDirection.Up or NavigationDirection.Down
                or NavigationDirection.Left or NavigationDirection.Right))
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown navigation direction.");
            }

            if (!visual.TryGetKey(fromIndex, out var source))
            {
                return null;
            }

            KeyVisual? best = null;
            var bestScore = default(Candidate);

            foreach (var candidate in visual.Keys)
            {
                if (candidate.Index == source.Index)
                {
                    continue;
                }

                if (!TryScore(source, candidate, direction, out var score))
                {
                    continue;
                }

                if (best is null || IsBetter(score, bestScore))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>
        /// Scores <paramref name="candidate"/> as a destination, or returns false when its centre
        /// is not strictly on the <paramref name="direction"/> side of the source's centre, or
        /// when it fails the non-overlapping rule for this direction.
        /// <para>
        /// <b>Left/Right require row overlap.</b> "The key to my left" is always on my row; there
        /// is no board where it is not. Without the rule the 2U <c>hk0</c> at the top-left of the
        /// Freestyle Edge RGB — centre X <c>1.0</c> — pulled in every 1U hotkey below it (centre X
        /// <c>0.5</c>, strictly left, no Y overlap), so Left on the board's top-left key walked the
        /// user diagonally down the hotkey column instead of returning null.
        /// </para>
        /// <para>
        /// <b>Up/Down keep the fallback, bounded by a 45° cone</b>
        /// (<c>perpendicularDistance &lt;= primaryDistance</c>). The vertical fallback earns its
        /// place: the Advantage2 and Advantage360 thumb clusters sit clear of the main well with
        /// no X overlap at all, so a thumb key one row down and one unit across must stay
        /// reachable. The cone is what stops that from also licensing a three-row diagonal
        /// teleport across the board.
        /// </para>
        /// </summary>
        private static bool TryScore(
            KeyVisual source,
            KeyVisual candidate,
            NavigationDirection direction,
            out Candidate score)
        {
            score = default;

            var isHorizontal = direction is NavigationDirection.Left or NavigationDirection.Right;
            var isTowardsEnd = direction is NavigationDirection.Right or NavigationDirection.Down;

            var sourceCenterX = source.X + (source.Width / 2.0);
            var sourceCenterY = source.Y + (source.Height / 2.0);
            var candidateCenterX = candidate.X + (candidate.Width / 2.0);
            var candidateCenterY = candidate.Y + (candidate.Height / 2.0);

            var primaryDelta = isHorizontal
                ? candidateCenterX - sourceCenterX
                : candidateCenterY - sourceCenterY;
            var primaryDistance = isTowardsEnd ? primaryDelta : -primaryDelta;

            if (primaryDistance <= Epsilon)
            {
                return false;
            }

            var perpendicularDistance = isHorizontal
                ? Math.Abs(candidateCenterY - sourceCenterY)
                : Math.Abs(candidateCenterX - sourceCenterX);

            var overlaps = isHorizontal
                ? Overlaps(source.Y, source.Bottom, candidate.Y, candidate.Bottom)
                : Overlaps(source.X, source.Right, candidate.X, candidate.Right);

            if (!overlaps && !IsAcceptedWithoutOverlap(isHorizontal, primaryDistance, perpendicularDistance))
            {
                return false;
            }

            score = new Candidate(overlaps ? 0 : 1, primaryDistance, perpendicularDistance, candidate.Index);

            return true;
        }

        /// <summary>
        /// Whether a candidate that shares no perpendicular span with the source may still be a
        /// destination: never for Left/Right, and for Up/Down only inside a 45° cone around the
        /// direction of travel. Rationale in <see cref="TryScore"/>.
        /// </summary>
        private static bool IsAcceptedWithoutOverlap(
            bool isHorizontal,
            double primaryDistance,
            double perpendicularDistance)
        {
            if (isHorizontal)
            {
                return false;
            }

            return perpendicularDistance <= primaryDistance + Epsilon;
        }

        /// <summary>Whether two spans on one axis share more than a touching edge.</summary>
        private static bool Overlaps(double sourceStart, double sourceEnd, double candidateStart, double candidateEnd)
        {
            return Math.Min(sourceEnd, candidateEnd) - Math.Max(sourceStart, candidateStart) > Epsilon;
        }

        private static bool IsBetter(in Candidate candidate, in Candidate best)
        {
            if (candidate.OverlapRank != best.OverlapRank)
            {
                return candidate.OverlapRank < best.OverlapRank;
            }

            if (Math.Abs(candidate.PrimaryDistance - best.PrimaryDistance) > Epsilon)
            {
                return candidate.PrimaryDistance < best.PrimaryDistance;
            }

            if (Math.Abs(candidate.PerpendicularDistance - best.PerpendicularDistance) > Epsilon)
            {
                return candidate.PerpendicularDistance < best.PerpendicularDistance;
            }

            return candidate.Index < best.Index;
        }

        /// <summary>The four scoring tiers of <see cref="Next"/>, compared in declaration order.</summary>
        private readonly struct Candidate
        {
            /// <summary>0 when the perpendicular spans overlap, 1 when they do not.</summary>
            public int OverlapRank { get; }

            /// <summary>Centre distance along the direction's axis; always positive.</summary>
            public double PrimaryDistance { get; }

            /// <summary>Centre distance across the direction's axis.</summary>
            public double PerpendicularDistance { get; }

            /// <summary>The candidate's <see cref="KeyVisual.Index"/>, the final tie-break.</summary>
            public int Index { get; }

            public Candidate(int overlapRank, double primaryDistance, double perpendicularDistance, int index)
            {
                OverlapRank = overlapRank;
                PrimaryDistance = primaryDistance;
                PerpendicularDistance = perpendicularDistance;
                Index = index;
            }
        }
    }
}
