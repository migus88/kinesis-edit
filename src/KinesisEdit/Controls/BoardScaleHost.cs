using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// Scales its one child up or down as a <b>single picture</b> and centres the result — the
    /// keyboard canvas's "scale the whole board up with available space; keep proportions"
    /// (docs/design/handoff.md § "Geometry").
    /// <para>
    /// The board underneath is authored entirely at <b>mock scale</b>: 34x30 cell pitch, 4 px gaps,
    /// 1 px borders, a 9/400 legend, 2 px badges, 9 px section padding and the 26 px split gutter.
    /// Every one of those is a fixed number, and every one of them has to grow together or the
    /// picture stops being the picture the design specifies. A layout-time scale would grow the
    /// cells and leave the hairlines, the badges and the type where they were; a
    /// <see cref="ScaleTransform"/> over the finished board grows all of it at once. That is the
    /// whole reason this control exists and the reason <see cref="KeyboardPanel"/> no longer scales
    /// anything.
    /// </para>
    /// <para>
    /// <b>The ceiling is <see cref="MaxScale"/>, and this control carries no policy about it.</b>
    /// It defaults to <see cref="double.PositiveInfinity"/> — the host on its own still grows the
    /// board with every pixel on offer — and the view that hosts a real board is what sets one.
    /// <c>Controls/KeyboardView.axaml</c> sets it from the <c>BoardScaleMax</c> geometry token
    /// (1.5), because the handoff's "scale the whole board up with available space" was written for
    /// a 1000px window and reads as absurd at 2560: the Freestyle Edge RGB's 695x196 picture was
    /// drawn 2238x631, a wall of caps with 40px legends. The board is vector all the way down, so a
    /// capped board is still <i>drawn</i> at its scale rather than magnified from a 1x raster; what
    /// the cap buys is a picture that stays a keyboard.
    /// </para>
    /// <para>
    /// <b><see cref="Decorator.Padding"/> is not honoured.</b> This host adds no chrome of its own;
    /// the board's inset is the section panel's <c>PaddingBoardSection</c>, which scales with the
    /// picture as everything else does. Padding here would be the one measurement that did not.
    /// </para>
    /// <para>
    /// <b><see cref="Visual.ClipToBounds"/> stays false</b>, here and on everything between a cap
    /// and the window: a focused cap casts a 3 px halo past its own bounds into a grid whose caps
    /// are 4 px apart, and a clip anywhere along the chain erases it.
    /// </para>
    /// </summary>
    public sealed class BoardScaleHost : Decorator
    {
        /// <summary>
        /// The factor the child is currently drawn at, published after each arrange pass. 1 is
        /// mock scale.
        /// </summary>
        public static readonly DirectProperty<BoardScaleHost, double> ScaleProperty =
            AvaloniaProperty.RegisterDirect<BoardScaleHost, double>(nameof(Scale), host => host.Scale);

        /// <summary>
        /// The largest factor the child may be drawn at. Infinity by default, so the control itself
        /// imposes nothing; a view sets it from the <c>BoardScaleMax</c> token.
        /// </summary>
        public static readonly StyledProperty<double> MaxScaleProperty =
            AvaloniaProperty.Register<BoardScaleHost, double>(nameof(MaxScale), double.PositiveInfinity);

        /// <summary>The scale that fits the child, or 0 when there is nothing to draw or no room.</summary>
        public double Scale
        {
            get => _scale;
            private set => SetAndRaise(ScaleProperty, ref _scale, value);
        }

        /// <summary>
        /// The ceiling the resolved scale is clamped to. A value that is not a positive, finite
        /// number caps nothing — which is what the infinite default means, and what keeps a host
        /// nobody has configured behaving exactly as it did before the property existed.
        /// </summary>
        public double MaxScale
        {
            get => GetValue(MaxScaleProperty);
            set => SetValue(MaxScaleProperty, value);
        }

        /// <summary>
        /// The transform the child is drawn through. One instance, mutated in place: a fresh
        /// <see cref="ScaleTransform"/> per arrange pass would allocate on every window resize.
        /// </summary>
        private readonly ScaleTransform _transform = new();

        private double _scale = 1;

        /// <summary>A different ceiling is a differently sized picture, so it has to re-measure.</summary>
        static BoardScaleHost()
        {
            AffectsMeasure<BoardScaleHost>(MaxScaleProperty);
        }

        /// <summary>Creates the host with clipping off, which is not the framework default.</summary>
        public BoardScaleHost()
        {
            ClipToBounds = false;
        }

        /// <summary>
        /// Reports the child's natural size shrunk to fit. The child is measured <b>unconstrained</b>
        /// — its mock-scale size is the thing being scaled, so it must never be talked down by the
        /// space on offer. A dimension that is infinite, NaN or non-positive constrains nothing,
        /// which leaves the natural size when neither does.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var child = Child;

            if (child is null)
            {
                return default;
            }

            child.Measure(Size.Infinity);

            var natural = child.DesiredSize;
            var scale = ResolveScale(availableSize, natural, MaxScale);

            return new Size(natural.Width * scale, natural.Height * scale);
        }

        /// <summary>
        /// Draws the child at its natural size through a uniform scale, and centres the scaled
        /// result. Arrange reads <paramref name="finalSize"/> as <i>what you get</i> rather than as
        /// <i>what constrains you</i>: a dimension that is zero or NaN here is no room at all, not
        /// an absent constraint, so the child is arranged away rather than drawn at mock scale
        /// through a slot that has none.
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            var child = Child;

            if (child is null)
            {
                return finalSize;
            }

            var natural = child.DesiredSize;

            if (!IsDrawable(natural) || !IsDrawable(finalSize))
            {
                // The child is still arranged, and its transform still cleared: an unarranged child
                // keeps its previous bounds, and with no clip anywhere on the chain it would be
                // painted over a picture that is meant to be empty.
                child.RenderTransform = null;

                child.Arrange(default);

                Scale = 0;

                return finalSize;
            }

            var scale = ResolveScale(finalSize, natural, MaxScale);

            _transform.ScaleX = scale;
            _transform.ScaleY = scale;

            // Top-left, so the scaled picture grows away from the corner it is arranged at and the
            // centring arithmetic below is the whole story.
            child.RenderTransformOrigin = RelativePoint.TopLeft;
            child.RenderTransform = _transform;

            var left = Math.Max(0, (finalSize.Width - (natural.Width * scale)) / 2);
            var top = Math.Max(0, (finalSize.Height - (natural.Height * scale)) / 2);

            child.Arrange(new Rect(left, top, natural.Width, natural.Height));

            Scale = scale;

            return finalSize;
        }

        /// <summary>
        /// The uniform factor that fits <paramref name="natural"/> into <paramref name="available"/>,
        /// never above <paramref name="maxScale"/>. A dimension that cannot constrain — infinite,
        /// NaN, non-positive, or measured against a natural extent of nothing — contributes no
        /// candidate, and with no candidate at all the answer is 1: mock scale, never infinity.
        /// <para>
        /// The ceiling is applied last, to the fallback as well as to a fitted candidate: a host
        /// that is told the board may never exceed 0.5x must not draw it at 1 because nothing
        /// happened to constrain it.
        /// </para>
        /// </summary>
        private static double ResolveScale(Size available, Size natural, double maxScale)
        {
            var horizontal = IsUsable(available.Width) && IsUsable(natural.Width)
                ? available.Width / natural.Width
                : double.PositiveInfinity;
            var vertical = IsUsable(available.Height) && IsUsable(natural.Height)
                ? available.Height / natural.Height
                : double.PositiveInfinity;
            var fitted = Math.Min(horizontal, vertical);
            var scale = double.IsFinite(fitted) && fitted > 0 ? fitted : 1;

            // A ceiling that is not itself a usable length caps nothing — the infinite default, and
            // anything nonsensical, both leave the fitted answer alone rather than erasing the board.
            return IsUsable(maxScale) ? Math.Min(scale, maxScale) : scale;
        }

        private static bool IsDrawable(Size size)
        {
            return IsUsable(size.Width) && IsUsable(size.Height);
        }

        private static bool IsUsable(double length)
        {
            return double.IsFinite(length) && length > 0;
        }
    }
}
