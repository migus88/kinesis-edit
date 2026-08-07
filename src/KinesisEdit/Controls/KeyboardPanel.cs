using Avalonia;
using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// Arranges its children from board-absolute <b>key-unit</b> rectangles (1.0 = one 1U cap,
    /// <see cref="Core.Geometry.Visual.KeyVisual"/>) onto the design's <b>mock-scale cell</b>. It is
    /// the only piece of the keyboard picture that does arithmetic, and it knows nothing about keys,
    /// layers or remaps.
    /// <para>
    /// The cell is not square. Every 1U cell is <see cref="PitchX"/> wide and <see cref="PitchY"/>
    /// tall, and the cap inside it is inset by <see cref="Gap"/> — so pitch minus gap is the cap the
    /// handoff specifies, "keycaps at mock scale: 30x26 (1u), gap 4" (34 - 4 = 30 wide, 30 - 4 = 26
    /// tall). The gap is a single inset rather than half a gap on each side, which is what makes a
    /// wide cap its span in pitches minus <i>one</i> gap: a 2U Backspace is 2 x 34 - 4 = 64, and a
    /// row of six 1U caps is 6 x 34 - 4 = 200.
    /// </para>
    /// <para>
    /// <b>It scales nothing.</b> The whole board — caps, hairlines, legends, badges, section padding
    /// and the split gutter — is authored at mock scale and grown as one picture by
    /// <see cref="BoardScaleHost"/>. A panel that scaled its own cells would grow the cells and
    /// leave everything drawn on top of them behind.
    /// </para>
    /// <para>
    /// <see cref="UnitOriginX"/>/<see cref="UnitOriginY"/> are what let one panel draw a
    /// <i>section</i> of a board without any coordinate being re-based: the keys keep the
    /// board-absolute units arrow navigation needs (<c>KeyAdjacency</c>) and the panel subtracts its
    /// own origin as it places them.
    /// </para>
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
        /// The <c>KeycapPitchX</c> token's value, as the fallback for a panel nothing sets it on —
        /// a design-time preview or a targeted test. The view passes the token itself.
        /// </summary>
        public const double DefaultPitchX = 34.0;

        /// <summary>The <c>KeycapPitchY</c> token's value; see <see cref="DefaultPitchX"/>.</summary>
        public const double DefaultPitchY = 30.0;

        /// <summary>The <c>KeycapGap</c> token's value; see <see cref="DefaultPitchX"/>.</summary>
        public const double DefaultGap = 4.0;

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

        /// <summary>Width of the section being drawn, in key units.</summary>
        public static readonly StyledProperty<double> BoardWidthProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(BoardWidth));

        /// <summary>Height of the section being drawn, in key units.</summary>
        public static readonly StyledProperty<double> BoardHeightProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(BoardHeight));

        /// <summary>Board-absolute key-unit X this panel's left edge stands at.</summary>
        public static readonly StyledProperty<double> UnitOriginXProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(UnitOriginX));

        /// <summary>Board-absolute key-unit Y this panel's top edge stands at.</summary>
        public static readonly StyledProperty<double> UnitOriginYProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(UnitOriginY));

        /// <summary>Pixels per key unit horizontally; the <c>KeycapPitchX</c> token.</summary>
        public static readonly StyledProperty<double> PitchXProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(PitchX), DefaultPitchX);

        /// <summary>Pixels per key unit vertically; the <c>KeycapPitchY</c> token.</summary>
        public static readonly StyledProperty<double> PitchYProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(PitchY), DefaultPitchY);

        /// <summary>Pixels a cap is inset by inside its cell; the <c>KeycapGap</c> token.</summary>
        public static readonly StyledProperty<double> GapProperty =
            AvaloniaProperty.Register<KeyboardPanel, double>(nameof(Gap), DefaultGap);

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

        /// <summary>Width of the section being drawn, in key units.</summary>
        public double BoardWidth
        {
            get => GetValue(BoardWidthProperty);
            set => SetValue(BoardWidthProperty, value);
        }

        /// <summary>Height of the section being drawn, in key units.</summary>
        public double BoardHeight
        {
            get => GetValue(BoardHeightProperty);
            set => SetValue(BoardHeightProperty, value);
        }

        /// <inheritdoc cref="UnitOriginXProperty" />
        public double UnitOriginX
        {
            get => GetValue(UnitOriginXProperty);
            set => SetValue(UnitOriginXProperty, value);
        }

        /// <inheritdoc cref="UnitOriginYProperty" />
        public double UnitOriginY
        {
            get => GetValue(UnitOriginYProperty);
            set => SetValue(UnitOriginYProperty, value);
        }

        /// <inheritdoc cref="PitchXProperty" />
        public double PitchX
        {
            get => GetValue(PitchXProperty);
            set => SetValue(PitchXProperty, value);
        }

        /// <inheritdoc cref="PitchYProperty" />
        public double PitchY
        {
            get => GetValue(PitchYProperty);
            set => SetValue(PitchYProperty, value);
        }

        /// <inheritdoc cref="GapProperty" />
        public double Gap
        {
            get => GetValue(GapProperty);
            set => SetValue(GapProperty, value);
        }

        static KeyboardPanel()
        {
            AffectsParentArrange<KeyboardPanel>(UnitXProperty, UnitYProperty, UnitWidthProperty, UnitHeightProperty);
            AffectsArrange<KeyboardPanel>(UnitOriginXProperty, UnitOriginYProperty);
            AffectsMeasure<KeyboardPanel>(
                BoardWidthProperty,
                BoardHeightProperty,
                PitchXProperty,
                PitchYProperty,
                GapProperty);
        }

        /// <summary>
        /// Reports the section at mock scale, whatever space is on offer: the picture is scaled as a
        /// whole by <see cref="BoardScaleHost"/>, so shrinking here would scale it twice.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            MeasureChildren();

            if (!TryGetBoard(out var boardWidth, out var boardHeight))
            {
                return default;
            }

            return new Size(CellSpan(boardWidth, PitchX), CellSpan(boardHeight, PitchY));
        }

        /// <summary>
        /// Places every child on the cell grid, relative to this panel's own key-unit origin.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!TryGetBoard(out _, out _))
            {
                ArrangeChildrenEmpty();

                return finalSize;
            }

            var pitchX = PitchX;
            var pitchY = PitchY;
            var originX = UnitOriginX;
            var originY = UnitOriginY;

            foreach (var child in Children)
            {
                var left = (GetUnitX(child) - originX) * pitchX;
                var top = (GetUnitY(child) - originY) * pitchY;
                var width = CellSpan(GetUnitWidth(child), pitchX);
                var height = CellSpan(GetUnitHeight(child), pitchY);

                child.Arrange(new Rect(left, top, width, height));
            }

            return finalSize;
        }

        private static bool IsUsable(double length)
        {
            return double.IsFinite(length) && length > 0;
        }

        /// <summary>
        /// How many pixels <paramref name="units"/> key units occupy at <paramref name="pitch"/>,
        /// less the one gap that is never drawn — the trailing edge of a cap, and of the section.
        /// Clamped at 0, because a negative <see cref="Rect"/> throws.
        /// </summary>
        private double CellSpan(double units, double pitch)
        {
            return Math.Max(0, (units * pitch) - Gap);
        }

        private bool TryGetBoard(out double boardWidth, out double boardHeight)
        {
            boardWidth = BoardWidth;
            boardHeight = BoardHeight;

            return IsUsable(boardWidth) && IsUsable(boardHeight);
        }

        private void MeasureChildren()
        {
            var pitchX = PitchX;
            var pitchY = PitchY;

            foreach (var child in Children)
            {
                child.Measure(new Size(
                    CellSpan(GetUnitWidth(child), pitchX),
                    CellSpan(GetUnitHeight(child), pitchY)));
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
