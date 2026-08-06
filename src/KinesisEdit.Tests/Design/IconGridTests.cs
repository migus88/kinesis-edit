using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The icon law — "16px grid, 1.5px stroke, square caps, geometry only" — made enforceable.
    /// Every mark is measured against the coordinate box it was authored in, and every mark is
    /// compared against every other, because a set of marks is only worth having if the members
    /// stay on the grid and stay distinguishable.
    /// </summary>
    public class IconGridTests
    {
        /// <summary>
        /// Rounding room, in authoring units. The bounds come from the rasteriser's own path
        /// measurement, so a curve's extremum is a fraction rather than an exact number.
        /// </summary>
        private const double Tolerance = 0.75;

        /// <summary>
        /// The room the stroked-extent check allows. Much tighter than <see cref="Tolerance"/>,
        /// because that one covers a mark authored a fraction off the grid and this one is the
        /// grid: a mark whose stroke overflows its box does so by a half pen, not by a rounding.
        /// </summary>
        private const double PenTolerance = 0.1;

        /// <summary>
        /// A mark must fill a reasonable share of its box; the fraction is loose enough for the
        /// smallest deliberate mark in the set (the 5.6-wide connected core) and tight enough to
        /// catch a path that was emptied or authored at the wrong scale.
        /// </summary>
        private const double MinimumBoxShare = 0.25;

        /// <summary>
        /// The two pairs that are deliberately the same shape. Named here rather than tolerated
        /// silently: an exemption nobody can see is an exemption that grows.
        /// </summary>
        private static readonly (string First, string Second, string Reason)[] _sharedShapes =
        [
            (
                "IconCannotAccess",
                "IconWarning",
                "the design draws both as a `!` in a circle and separates them by colour — red for "
                    + "the failure, amber for the advisory, per the law that an advisory is never red"
            ),
            (
                "IconConnected",
                "IconNotDetected",
                "both are the shared ring; the difference is the pen, because the design draws "
                    + "not-detected dashed and a dash pattern is a pen property, not a shape"
            )
        ];

        [AvaloniaFact]
        public void EveryMark_PaintsInsideItsAuthoredBox()
        {
            var failures = new List<string>();

            foreach (var key in IconCatalog.AllKeys())
            {
                var box = IconCatalog.BoxOf(key);
                var bounds = IconCatalog.ResolveGeometry(key, ThemeVariant.Dark).Bounds;

                if (bounds.X < box.X - Tolerance
                    || bounds.Y < box.Y - Tolerance
                    || bounds.Right > box.Right + Tolerance
                    || bounds.Bottom > box.Bottom + Tolerance)
                {
                    failures.Add($"{key} spans {bounds}, outside its {box.Width}x{box.Height} box");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [AvaloniaFact]
        public void EveryMark_KeepsItsStrokeInsideItsBoxToo()
        {
            // "The painted result, stroke included, never leaves the box": a ring of radius 7 on
            // (8,8) drawn with the 1.5 pen lands its outer edge at 0.25 and 15.75. A mark drawn to
            // the very edge of its box would be clipped by whatever crops the icon, and would sit a
            // half-pen closer to its neighbour than every other mark in the rail.
            var pen = (double)DesignTokens.Resolve("IconStrokeThickness", ThemeVariant.Dark) / 2;
            var failures = new List<string>();

            foreach (var key in IconCatalog.AllKeys())
            {
                var box = IconCatalog.BoxOf(key);
                var painted = IconCatalog.ResolveGeometry(key, ThemeVariant.Dark).Bounds.Inflate(pen);

                if (painted.X < box.X - PenTolerance
                    || painted.Y < box.Y - PenTolerance
                    || painted.Right > box.Right + PenTolerance
                    || painted.Bottom > box.Bottom + PenTolerance)
                {
                    failures.Add($"{key} paints {painted}, outside its {box.Width}x{box.Height} box");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [AvaloniaFact]
        public void EveryMark_FillsEnoughOfItsBoxToRead()
        {
            // The longer edge is what is measured: several marks are a single horizontal or
            // vertical run — Disable is one 8-unit bar — and have no extent at all in the other
            // axis. This catches an emptied path and a mark authored at the wrong scale.
            var failures = new List<string>();

            foreach (var key in IconCatalog.AllKeys())
            {
                var box = IconCatalog.BoxOf(key);
                var bounds = IconCatalog.ResolveGeometry(key, ThemeVariant.Dark).Bounds;
                var minimum = Math.Max(box.Width, box.Height) * MinimumBoxShare;

                if (Math.Max(bounds.Width, bounds.Height) < minimum)
                {
                    failures.Add($"{key} measures {bounds.Width:0.##}x{bounds.Height:0.##}, under {minimum:0.##}");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [AvaloniaFact]
        public void EveryDeviceArt_IsCentredInTheSharedBox()
        {
            // "Every geometry is authored in the same 92x56 box, with the board centred in it, so
            // in these coordinates the seven are at true proportion to one another." A board drawn
            // off-centre would sit off-centre in every card that draws the box rather than the ink.
            var box = IconCatalog.DeviceArtBox;
            var failures = new List<string>();

            foreach (var key in IconCatalog.DeclaredKeys(IconCatalog.FamilyOf("Themes/DeviceArt.axaml")))
            {
                var bounds = IconCatalog.ResolveGeometry(key, ThemeVariant.Dark).Bounds;

                if (Math.Abs(bounds.Center.X - box.Center.X) > Tolerance
                    || Math.Abs(bounds.Center.Y - box.Center.Y) > Tolerance)
                {
                    failures.Add($"{key} centres on {bounds.Center}, not {box.Center}");
                }
            }

            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }

        [AvaloniaFact]
        public void TheScanningMark_IsLopsided_WhichIsWhyEveryMarkDeclaresItsBox()
        {
            // Three quarters of a ring: the right quadrant is missing, so the arc's ink is not
            // centred on the circle it belongs to. Fitting that ink instead of the 14x14 box would
            // re-centre the arc, and the spinner would wobble as it turned. This is the mark that
            // makes the SourceBox rule load-bearing rather than tidy.
            var bounds = IconCatalog.ResolveGeometry(IconCatalog.ScanningKey, ThemeVariant.Dark).Bounds;
            var box = IconCatalog.SpinnerBox;

            Assert.True(
                Math.Abs(bounds.Center.Y - box.Center.Y) <= Tolerance,
                $"The scanning arc centres vertically on {bounds.Center.Y}, not {box.Center.Y}.");
            Assert.True(
                box.Center.X - bounds.Center.X > Tolerance,
                $"The scanning arc's ink centres on x {bounds.Center.X}; a mark that is not lopsided proves nothing here.");
        }

        [AvaloniaFact]
        public void NoTwoMarks_ShareTheSamePathData_ExceptTheNamedPairs()
        {
            // "Distinguishable at rail size", as far as a machine can check it: two marks that are
            // the same path are the same picture, and the user has no way to tell them apart.
            var data = IconCatalog.PathData();

            Assert.Equal(IconCatalog.AllKeys().Count, data.Count);

            var exempt = _sharedShapes
                .Select(pair => new[] { pair.First, pair.Second }.Order(StringComparer.Ordinal).ToArray())
                .Select(pair => string.Join(" == ", pair))
                .ToHashSet(StringComparer.Ordinal);

            var collisions = data
                .GroupBy(mark => mark.Value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => string.Join(" == ", group.Select(mark => mark.Key).Order(StringComparer.Ordinal)))
                .Where(collision => !exempt.Contains(collision))
                .ToArray();

            Assert.True(collisions.Length == 0, $"Marks drawn identically: {string.Join("; ", collisions)}.");
        }

        [AvaloniaFact]
        public void TheDeliberatelySharedShapes_AreStillShared()
        {
            // The mirror of the guard above. If one of these pairs diverges the exemption is stale
            // and must be removed, or the next collision hides behind it.
            var data = IconCatalog.PathData();

            foreach (var (first, second, reason) in _sharedShapes)
            {
                Assert.True(
                    string.Equals(data[first], data[second], StringComparison.Ordinal),
                    $"{first} and {second} no longer share a shape; drop the exemption ({reason}).");
            }
        }
    }
}
