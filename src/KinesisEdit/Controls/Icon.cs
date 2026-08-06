using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// Draws one <see cref="Geometry"/> under the design's icon law — <b>16 px grid, 1.5 px stroke,
    /// square caps, geometry only</b> (docs/design/mockups.md, mockup 2b). It exists so that law is
    /// spelled once instead of at every call site: a view names a geometry and a brush, and the
    /// stroke thickness, the caps, the join, the sizing and the fit come from here.
    /// <para>
    /// <b>Colour is never resolved in here.</b> The call site binds <see cref="Stroke"/> and
    /// <see cref="Fill"/> to token brushes with <c>{DynamicResource}</c>, and this control simply
    /// uses whatever it is handed — a <c>StaticResource</c> lookup inside the control would snapshot
    /// one theme variant and freeze the icon at that colour when the OS flips. An icon with neither
    /// brush set draws nothing at all; that is deliberate, because the design has no default icon
    /// colour and a guessed one would be an invented token.
    /// </para>
    /// <para>
    /// <b>Fit: the authoring box, not the ink.</b> A mark is scaled by its <see cref="SourceBox"/>
    /// — the coordinate box it was drawn in, 16x16 by default — and not by its own
    /// <see cref="Geometry.Bounds"/>. That distinction is the whole point of the icon grid: the
    /// state marks are aligned on a shared centre and are deliberately inset from their box by
    /// different amounts (the eject mark spans y 2.3–13.7, the rings 1–15), so fitting each one's
    /// ink would blow every mark out to the same extent and destroy both the shared centre and the
    /// relative weights the set was drawn with. It would also spin the scanning mark about a point
    /// off its own centre. Fitting is <i>uniform</i> and centred, so a non-square
    /// <see cref="SourceBox"/> — the device silhouettes are drawn in plan view at true proportion —
    /// is letterboxed rather than stretched; a squashed keyboard reads as a bug immediately. A
    /// <see cref="SourceBox"/> with no extent falls back to the geometry's own bounds, which is the
    /// escape hatch for a mark that carries no box of its own.
    /// </para>
    /// <para>
    /// <b>The stroke-scaling rule: the geometry scales, the pen does not.</b>
    /// <see cref="StrokeThickness"/> is a device-independent thickness at the <i>rendered</i> size,
    /// so a 1.5 px stroke measures 1.5 px whether the mark is drawn at 16 px or blown up to a 96 px
    /// device-art card. Naively pushing the fit transform would scale the pen with everything else
    /// and make a large icon read as a fat one; the pen is therefore built at
    /// <c>StrokeThickness / scale</c> so that the transform cancels back out to exactly
    /// <see cref="StrokeThickness"/> on screen. <see cref="StrokeDashArray"/> inherits that rule for
    /// free, because Avalonia's dash lengths are multiples of the pen thickness.
    /// </para>
    /// <para>
    /// <b>Nothing here moves.</b> The two marks that need more than a geometry and a brush get it
    /// from a class in <c>Styles/Icons.axaml</c>: <c>.spinner</c> (the scanning mark, its 14x14 box
    /// and the 1.1 s rotation — which keeps running under reduce-motion, deliberately) and
    /// <c>.deviceArt</c> (the 92x56 box every silhouette is drawn in). Motion stays in the style
    /// layer where the motion budget can see it.
    /// </para>
    /// </summary>
    public sealed class Icon : Control
    {
        /// <summary>
        /// Default edge length of the icon box. Mirrors the <c>IconSize</c> resource in
        /// Themes/Geometry.axaml, which cannot be a styled property's default because a default is
        /// resolved before any resource dictionary is in scope.
        /// </summary>
        public const double DefaultSize = 16.0;

        /// <summary>
        /// Default stroke width. Mirrors <c>IconStrokeThickness</c> in Themes/Geometry.axaml, for
        /// the same reason as <see cref="DefaultSize"/>.
        /// </summary>
        public const double DefaultStrokeThickness = 1.5;

        /// <summary>
        /// "Square caps" is the law, not a call-site choice, so it is fixed here. The matching join
        /// is the mitre: a square-capped stroke that rounded its corners would read as two different
        /// pens in one mark.
        /// </summary>
        public const PenLineCap StrokeLineCap = PenLineCap.Square;

        /// <inheritdoc cref="StrokeLineCap" />
        public const PenLineJoin StrokeLineJoin = PenLineJoin.Miter;

        /// <summary>
        /// The icon law's grid — "16px grid, 1.5px stroke, square caps, geometry only" — as the
        /// coordinate box a mark is authored in. Declared before <see cref="SourceBoxProperty"/>
        /// because a static initialiser reads what is above it, not what is below.
        /// </summary>
        public static Rect DefaultSourceBox { get; } = new(0, 0, DefaultSize, DefaultSize);

        /// <summary>The mark to draw. Null renders nothing.</summary>
        public static readonly StyledProperty<Geometry?> DataProperty =
            AvaloniaProperty.Register<Icon, Geometry?>(nameof(Data));

        /// <summary>The outline brush — a token brush bound with <c>{DynamicResource}</c>.</summary>
        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<Icon, IBrush?>(nameof(Stroke));

        /// <summary>
        /// The fill brush, for the marks that are filled rather than outlined — the eject triangle,
        /// the connected disc, the register a device-art card sits on.
        /// </summary>
        public static readonly StyledProperty<IBrush?> FillProperty =
            AvaloniaProperty.Register<Icon, IBrush?>(nameof(Fill));

        /// <summary>Stroke width in device-independent pixels at the rendered size.</summary>
        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<Icon, double>(nameof(StrokeThickness), DefaultStrokeThickness);

        /// <summary>Edge length of the square box the geometry is fitted into.</summary>
        public static readonly StyledProperty<double> SizeProperty =
            AvaloniaProperty.Register<Icon, double>(nameof(Size), DefaultSize);

        /// <summary>
        /// The coordinate box <see cref="Data"/> was authored in. Defaults to the icon law's 16x16
        /// grid; the spinner declares its own 14x14 box (Themes/Icons.axaml), and each device-art
        /// silhouette declares the true-proportion box its plan view was drawn in.
        /// </summary>
        public static readonly StyledProperty<Rect> SourceBoxProperty =
            AvaloniaProperty.Register<Icon, Rect>(nameof(SourceBox), DefaultSourceBox);

        /// <summary>
        /// Dash pattern, in multiples of <see cref="StrokeThickness"/>. It has to be a pen property
        /// rather than something the geometry expresses: the "not detected" mark is a dashed ring,
        /// and a ring drawn as a dashed <i>path</i> would be a different geometry per dash length.
        /// </summary>
        public static readonly StyledProperty<AvaloniaList<double>?> StrokeDashArrayProperty =
            AvaloniaProperty.Register<Icon, AvaloniaList<double>?>(nameof(StrokeDashArray));

        /// <inheritdoc cref="DataProperty" />
        public Geometry? Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        /// <inheritdoc cref="StrokeProperty" />
        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        /// <inheritdoc cref="FillProperty" />
        public IBrush? Fill
        {
            get => GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        /// <inheritdoc cref="StrokeThicknessProperty" />
        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        /// <inheritdoc cref="SizeProperty" />
        public double Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <inheritdoc cref="SourceBoxProperty" />
        public Rect SourceBox
        {
            get => GetValue(SourceBoxProperty);
            set => SetValue(SourceBoxProperty, value);
        }

        /// <inheritdoc cref="StrokeDashArrayProperty" />
        public AvaloniaList<double>? StrokeDashArray
        {
            get => GetValue(StrokeDashArrayProperty);
            set => SetValue(StrokeDashArrayProperty, value);
        }

        static Icon()
        {
            AffectsRender<Icon>(
                DataProperty,
                StrokeProperty,
                FillProperty,
                StrokeThicknessProperty,
                StrokeDashArrayProperty,
                SourceBoxProperty);
            AffectsMeasure<Icon>(SizeProperty, SourceBoxProperty, DataProperty);
        }

        /// <summary>
        /// Draws the mark, fitted and centred, with a pen whose thickness survives the fit
        /// transform unchanged.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            base.Render(context);

            var data = Data;

            if (data is null || !TryResolveFit(data, out var box, out var scale))
            {
                return;
            }

            var pen = CreatePen(scale);
            var fill = Fill;

            if (fill is null && pen is null)
            {
                return;
            }

            using (context.PushTransform(CreateFitTransform(box, scale)))
            {
                context.DrawGeometry(fill, pen, data);
            }
        }

        /// <summary>
        /// Reports the fitted box, whatever the parent offers: an icon does not grow. For a square
        /// <see cref="SourceBox"/> — every mark on the 16px grid — that is
        /// <see cref="Size"/> by <see cref="Size"/>; a device silhouette reports the letterboxed
        /// rectangle instead of a square with dead margins baked into the layout.
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var data = Data;

            if (data is null || !TryResolveFit(data, out var box, out var scale))
            {
                return default;
            }

            return new Size(box.Width * scale, box.Height * scale);
        }

        /// <summary>
        /// The uniform scale that fits <paramref name="box"/> into an <paramref name="edge"/>-square
        /// box. An extent of zero — a mark that is a single horizontal or vertical line — contributes
        /// nothing rather than dividing by it, and a box with no extent at all is drawn unscaled.
        /// </summary>
        private static double ResolveScale(Rect box, double edge)
        {
            var horizontal = box.Width > 0 ? edge / box.Width : double.PositiveInfinity;
            var vertical = box.Height > 0 ? edge / box.Height : double.PositiveInfinity;
            var scale = Math.Min(horizontal, vertical);

            return double.IsFinite(scale) && scale > 0 ? scale : 1.0;
        }

        /// <summary>
        /// The box <paramref name="data"/> is fitted from and the uniform scale that takes it to
        /// <see cref="Size"/>. False when there is nothing to draw, which is a <see cref="Size"/>
        /// that is not a usable length or a mark with no extent in either axis.
        /// </summary>
        private bool TryResolveFit(Geometry data, out Rect box, out double scale)
        {
            var edge = Size;

            box = SourceBox;
            scale = 0;

            if (!double.IsFinite(edge) || edge <= 0)
            {
                return false;
            }

            // An empty box is the escape hatch: fit the ink instead. Every authored mark declares
            // its own box, so this is the path a caller takes deliberately.
            if (box.Width <= 0 || box.Height <= 0)
            {
                box = data.Bounds;
            }

            if (box.Width <= 0 && box.Height <= 0)
            {
                return false;
            }

            scale = ResolveScale(box, edge);

            return true;
        }

        /// <summary>
        /// Moves the authoring box's origin to zero, scales it, and centres the result in the space
        /// the control was arranged into — which is what letterboxes a device silhouette instead of
        /// stretching it.
        /// </summary>
        private Matrix CreateFitTransform(Rect box, double scale)
        {
            var offsetX = (Bounds.Width - (box.Width * scale)) / 2;
            var offsetY = (Bounds.Height - (box.Height * scale)) / 2;

            return Matrix.CreateTranslation(-box.X, -box.Y)
                * Matrix.CreateScale(scale, scale)
                * Matrix.CreateTranslation(offsetX, offsetY);
        }

        /// <summary>
        /// The pen to stroke with, pre-divided by <paramref name="scale"/> so the fit transform
        /// scales it back to exactly <see cref="StrokeThickness"/> on screen. Null when there is
        /// nothing to stroke with, so an icon that is pure fill costs no pen.
        /// </summary>
        private IPen? CreatePen(double scale)
        {
            var stroke = Stroke;
            var thickness = StrokeThickness;

            if (stroke is null || !double.IsFinite(thickness) || thickness <= 0)
            {
                return null;
            }

            return new ImmutablePen(
                stroke.ToImmutable(),
                thickness / scale,
                CreateDashStyle(),
                StrokeLineCap,
                StrokeLineJoin);
        }

        private ImmutableDashStyle? CreateDashStyle()
        {
            var dashes = StrokeDashArray;

            return dashes is { Count: > 0 } ? new ImmutableDashStyle(dashes, 0) : null;
        }
    }
}
