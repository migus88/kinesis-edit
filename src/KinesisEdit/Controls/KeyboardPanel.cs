using Avalonia;
using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// Arranges its children from board-absolute <b>key-unit</b> rectangles (1.0 = one 1U cap,
    /// <see cref="Core.Geometry.Visual.KeyVisual"/>), scaling the whole board uniformly into
    /// whatever space it is given and centring it there. It is the only piece of the keyboard
    /// picture that does arithmetic: it knows nothing about keys, layers or remaps.
    /// <para>
    /// Placement travels on <b>attached properties</b> rather than being read off each child's
    /// <c>DataContext</c>, so the panel stays usable with any item type — the item view model is
    /// projected onto <see cref="UnitXProperty"/>…<see cref="UnitHeightProperty"/> by the item
    /// container's style (see <c>KeyboardView.axaml</c>). They are named <c>Unit*</c> rather than
    /// the shorter <c>Width</c>/<c>Height</c> because a static <c>WidthProperty</c> declared here
    /// would hide <see cref="Layoutable.WidthProperty"/>, which is both a compiler warning and a
    /// genuine ambiguity in XAML.
    /// </para>
    /// </summary>
    public sealed class KeyboardPanel : Panel
    {
        /// <summary>
        /// Pixel size of one key unit when the panel is measured without a usable constraint (an
        /// auto-sized or scrolling parent). A constrained parent overrides it: the scale is then
        /// derived from the space actually offered.
        /// </summary>
        public const double NaturalUnitSize = 44.0;

        /// <summary>Gap left between neighbouring caps, in key units, so they never touch.</summary>
        public const double KeyGapUnits = 0.06;

        /// <summary>Left edge of the child on the board, in key units.</summary>
        public static readonly AttachedProperty<double> UnitXProperty =
            AvaloniaProperty.RegisterAttached<KeyboardPanel, Control, double>("UnitX");

        /// <summary>Top edge of the child on the board, in key units.</summary>
        public static readonly AttachedProperty<double> UnitYProperty =
            AvaloniaProperty.RegisterAttached<KeyboardPanel, Control, double>("UnitY");

        /// <summary>Width of the child, in key units.</summary>
        public static readonly AttachedProperty<double> UnitWidthProperty =
            AvaloniaProperty.RegisterAttached<KeyboardPanel, Control, double>("UnitWidth", 1.0);

        /// <summary>Height of the child, in key units.</summary>
        public static readonly AttachedProperty<double> UnitHeightProperty =
            AvaloniaProperty.RegisterAttached<KeyboardPanel, Control, double>("UnitHeight", 1.0);

        /// <summary>Width of the whole board, in key units.</summary>
        public static readonly StyledProperty<double> BoardWidthProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(BoardWidth));

        /// <summary>Height of the whole board, in key units.</summary>
        public static readonly StyledProperty<double> BoardHeightProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(BoardHeight));

        /// <summary>Reads <see cref="UnitXProperty"/> off <paramref name="control"/>.</summary>
        public static double GetUnitX(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);

            return control.GetValue(UnitXProperty);
        }

        /// <summary>Writes <see cref="UnitXProperty"/> onto <paramref name="control"/>.</summary>
        public static void SetUnitX(Control control, double value)
        {
            ArgumentNullException.ThrowIfNull(control);

            control.SetValue(UnitXProperty, value);
        }

        /// <summary>Reads <see cref="UnitYProperty"/> off <paramref name="control"/>.</summary>
        public static double GetUnitY(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);

            return control.GetValue(UnitYProperty);
        }

        /// <summary>Writes <see cref="UnitYProperty"/> onto <paramref name="control"/>.</summary>
        public static void SetUnitY(Control control, double value)
        {
            ArgumentNullException.ThrowIfNull(control);

            control.SetValue(UnitYProperty, value);
        }

        /// <summary>Reads <see cref="UnitWidthProperty"/> off <paramref name="control"/>.</summary>
        public static double GetUnitWidth(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);

            return control.GetValue(UnitWidthProperty);
        }

        /// <summary>Writes <see cref="UnitWidthProperty"/> onto <paramref name="control"/>.</summary>
        public static void SetUnitWidth(Control control, double value)
        {
            ArgumentNullException.ThrowIfNull(control);

            control.SetValue(UnitWidthProperty, value);
        }

        /// <summary>Reads <see cref="UnitHeightProperty"/> off <paramref name="control"/>.</summary>
        public static double GetUnitHeight(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);

            return control.GetValue(UnitHeightProperty);
        }

        /// <summary>Writes <see cref="UnitHeightProperty"/> onto <paramref name="control"/>.</summary>
        public static void SetUnitHeight(Control control, double value)
        {
            ArgumentNullException.ThrowIfNull(control);

            control.SetValue(UnitHeightProperty, value);
        }

        /// <summary>Width of the board being drawn, in key units.</summary>
        public double BoardWidth
        {
            get => GetValue(BoardWidthProperty);
            set => SetValue(BoardWidthProperty, value);
        }

        /// <summary>Height of the board being drawn, in key units.</summary>
        public double BoardHeight
        {
            get => GetValue(BoardHeightProperty);
            set => SetValue(BoardHeightProperty, value);
        }

        static KeyboardPanel()
        {
            AffectsParentArrange<KeyboardPanel>(UnitXProperty, UnitYProperty, UnitWidthProperty, UnitHeightProperty);
            AffectsMeasure<KeyboardPanel>(BoardWidthProperty, BoardHeightProperty);
        }

        /// <summary>
        /// Reports the board at the scale the offered space allows, so a constrained parent gets a
        /// board that already fits and an unconstrained one gets the natural size.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            if (!TryGetBoard(out var boardWidth, out var boardHeight))
            {
                MeasureChildren(default);

                return default;
            }

            var scale = ResolveScale(availableSize, boardWidth, boardHeight);

            MeasureChildren(scale);

            return new Size(boardWidth * scale, boardHeight * scale);
        }

        /// <summary>
        /// Scales the board uniformly into <paramref name="finalSize"/>, centres it, and places
        /// every child at its key-unit rectangle shrunk by <see cref="KeyGapUnits"/>.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!TryGetBoard(out var boardWidth, out var boardHeight))
            {
                ArrangeChildrenEmpty();

                return finalSize;
            }

            var scale = ResolveScale(finalSize, boardWidth, boardHeight);

            if (scale <= 0)
            {
                ArrangeChildrenEmpty();

                return finalSize;
            }

            var gap = KeyGapUnits * scale;
            var originX = (finalSize.Width - (boardWidth * scale)) / 2;
            var originY = (finalSize.Height - (boardHeight * scale)) / 2;

            foreach (var child in Children)
            {
                var left = originX + (GetUnitX(child) * scale) + (gap / 2);
                var top = originY + (GetUnitY(child) * scale) + (gap / 2);
                var width = Math.Max(0, (GetUnitWidth(child) * scale) - gap);
                var height = Math.Max(0, (GetUnitHeight(child) * scale) - gap);

                child.Arrange(new Rect(left, top, width, height));
            }

            return finalSize;
        }

        /// <summary>
        /// The uniform scale that fits the board into <paramref name="available"/>. A dimension
        /// that is infinite, NaN or non-positive contributes nothing, which leaves the natural
        /// size when neither dimension constrains the board.
        /// </summary>
        private static double ResolveScale(Size available, double boardWidth, double boardHeight)
        {
            var horizontal = IsUsable(available.Width) ? available.Width / boardWidth : double.PositiveInfinity;
            var vertical = IsUsable(available.Height) ? available.Height / boardHeight : double.PositiveInfinity;
            var scale = Math.Min(horizontal, vertical);

            return double.IsFinite(scale) && scale > 0 ? scale : NaturalUnitSize;
        }

        private static bool IsUsable(double length)
        {
            return double.IsFinite(length) && length > 0;
        }

        private bool TryGetBoard(out double boardWidth, out double boardHeight)
        {
            boardWidth = BoardWidth;
            boardHeight = BoardHeight;

            return IsUsable(boardWidth) && IsUsable(boardHeight);
        }

        private void MeasureChildren(double scale)
        {
            foreach (var child in Children)
            {
                child.Measure(new Size(GetUnitWidth(child) * scale, GetUnitHeight(child) * scale));
            }
        }

        private void ArrangeChildrenEmpty()
        {
            // A board with no size still has to arrange every child: an unarranged child keeps its
            // stale bounds and would be drawn on top of the empty picture.
            foreach (var child in Children)
            {
                child.Arrange(default);
            }
        }
    }
}
