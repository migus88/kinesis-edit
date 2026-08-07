using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// Drives the grid column the editor's right-hand rail sits in: its width, the band the drag is
    /// bounded by, whether the column exists at all, and the write back out of the seam.
    /// <para>
    /// <b>One mechanism, and since issue #128 one column.</b> The key inspector and the Lighting
    /// tab's mode rail were given one stored width by issue #124, but they were still two columns
    /// in two grids — so the Lighting rail started inside its own tab's padding and the two matched
    /// only by arithmetic. They are two <c>IsVisible</c>-gated children of a single
    /// <see cref="ColumnDefinition"/> now (<see cref="KeyboardEditorView"/>), which is what makes
    /// switching between the two tabs move nothing.
    /// </para>
    /// <para>
    /// <b>Why the width is pushed from code at all, rather than bound.</b> A
    /// <see cref="ColumnDefinition"/> is a bare <c>AvaloniaObject</c>: it is in neither the logical
    /// nor the visual tree, so it inherits no <c>DataContext</c> and a <c>{Binding}</c> written on
    /// it in XAML has nothing to resolve a path against. There are therefore <b>four</b> trigger
    /// points, and a view that drops one has a rail that is right until something moves it: attach
    /// (<see cref="Sync"/>, because the columns do not exist before the XAML is loaded), a
    /// <c>DataContext</c> change (<see cref="Follow"/>), the model's own <c>PropertyChanged</c>,
    /// and the rail host's <c>IsVisible</c> — the last three subscribed here, so the view has to do
    /// nothing but hand over the grid and report the seam's drag.
    /// </para>
    /// <para>
    /// <b>A section with no rail collapses the column outright.</b> Macros and Settings take the
    /// whole content width, and a fixed-pixel column keeps its width however invisible its contents
    /// are — so "hide the rail" has to be spelled on the column, not only on the control. The host
    /// control's <c>IsVisible</c> is the single source of that answer: the view gates it
    /// declaratively on the open tab, and this class follows.
    /// </para>
    /// <para>
    /// <b>A local <c>Width</c> on the rail control is not an alternative.</b> The layout pass reads
    /// it and a <see cref="GridSplitter"/> does not — a splitter resizes a <i>column</i> — so the
    /// two would disagree the moment the seam was dragged. That is why <c>Border.inspectorRail</c>
    /// lost its width setter in #119 and why <see cref="LightingModeRailView"/> dropped its own
    /// local one in #124.
    /// </para>
    /// </summary>
    internal sealed class InspectorRailColumn
    {
        /// <summary>
        /// The narrowest the column may be dragged to (<c>Themes/Geometry.axaml</c>). Resolved off
        /// the host rather than restated, so the band the seam obeys and the band the view model
        /// clamps to are the same two numbers.
        /// </summary>
        private const string MinimumWidthKey = "WidthInspectorRailMin";

        /// <summary>The widest the column may be dragged to.</summary>
        private const string MaximumWidthKey = "WidthInspectorRailMax";

        private readonly Control _host;
        private readonly Grid _grid;
        private readonly int _columnIndex;
        private readonly Control _railHost;
        private readonly WidthSource _widthSource;
        private InspectorRailWidthViewModel? _rail;

        /// <summary>
        /// Binds this driver to one column of <paramref name="grid"/>, showing
        /// <paramref name="railHost"/>.
        /// <para>
        /// <paramref name="host"/> is only ever asked for the two geometry tokens, so a view hosted
        /// without the app's resources — every design-time preview — keeps whatever the column was
        /// authored with instead of collapsing. <paramref name="railHost"/> is asked one question,
        /// continuously: is there a rail in this column at all.
        /// </para>
        /// </summary>
        public InspectorRailColumn(
            Control host,
            Grid grid,
            int columnIndex,
            Control railHost,
            WidthSource widthSource)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _columnIndex = columnIndex;
            _railHost = railHost ?? throw new ArgumentNullException(nameof(railHost));
            _widthSource = widthSource;

            // Never unsubscribed: the host is a child of the very view that owns this driver, so
            // the two die together and a subscription that outlived either would have nothing left
            // to point at.
            _railHost.PropertyChanged += OnRailHostPropertyChanged;
        }

        /// <summary>
        /// Points the column at <paramref name="rail"/> and syncs it, dropping the subscription to
        /// whatever it was following first: the shell reuses views, and a rail left subscribed would
        /// go on moving a column it no longer owns.
        /// </summary>
        public void Follow(InspectorRailWidthViewModel? rail)
        {
            if (!ReferenceEquals(_rail, rail))
            {
                if (_rail is not null)
                {
                    _rail.PropertyChanged -= OnRailPropertyChanged;
                }

                _rail = rail;

                if (_rail is not null)
                {
                    _rail.PropertyChanged += OnRailPropertyChanged;
                }
            }

            Sync();
        }

        /// <summary>
        /// Puts the followed width, and the band it lives in, onto the column.
        /// <para>
        /// The Macro panel's 300 px floor is deliberately <b>not</b> spelled here: it is folded into
        /// <see cref="InspectorRailWidthViewModel.EffectiveWidth"/>, which only the Keys tab reads
        /// (<see cref="WidthSource"/>). One owner for the floor, not two — and the Lighting tab,
        /// which has no macro panel, does not inherit it.
        /// </para>
        /// </summary>
        public void Sync()
        {
            if (_grid.ColumnDefinitions.Count <= _columnIndex)
            {
                return;
            }

            var column = _grid.ColumnDefinitions[_columnIndex];

            // No rail in this section: the column goes to nothing, floor and all. A fixed-pixel
            // column holds its width however invisible its contents are, so the width has to be
            // taken off it explicitly or Macros and Settings would keep a rail-shaped hole.
            // MaxWidth is left where it is — it only caps, so it cannot hold the column open.
            if (!_railHost.IsVisible)
            {
                column.MinWidth = 0;
                column.Width = new GridLength(0, GridUnitType.Pixel);

                return;
            }

            if (_rail is null)
            {
                return;
            }

            column.MinWidth = Measure(MinimumWidthKey, column.MinWidth);
            column.MaxWidth = Measure(MaximumWidthKey, column.MaxWidth);
            column.Width = new GridLength(WidthOf(_rail), GridUnitType.Pixel);
        }

        /// <summary>
        /// Reads the column back off the grid and stores it as the user's width. The splitter has
        /// already moved the column by the time this runs, so all that is left is to tell the model
        /// — which clamps, persists, and pushes the clamped result back through
        /// <see cref="Sync"/>.
        /// <para>
        /// Both tabs write <see cref="InspectorRailWidthViewModel.Width"/> however they read it: a
        /// drag is the user's own number, and the macro floor is a thing done to it afterwards
        /// rather than a thing stored.
        /// </para>
        /// </summary>
        public void Store()
        {
            // A collapsed column is zero wide, and zero is not a width the user chose. The seam is
            // hidden with the rail so this cannot normally fire, but the guard is what keeps the
            // stored number honest if it ever does.
            if (_rail is null || !_railHost.IsVisible || _grid.ColumnDefinitions.Count <= _columnIndex)
            {
                return;
            }

            var column = _grid.ColumnDefinitions[_columnIndex];

            // The splitter always writes an absolute length; ActualWidth is the fallback for the one
            // frame before the grid has re-measured.
            _rail.Width = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        }

        private double WidthOf(InspectorRailWidthViewModel rail)
        {
            return _widthSource == WidthSource.Effective ? rail.EffectiveWidth : rail.Width;
        }

        /// <summary>
        /// The rail's width moved: the user dragged a seam on either tab and the store clamped what
        /// they asked for, or the showing panel raised the macro floor. An empty property name is
        /// "everything changed", which a view model is entitled to raise.
        /// </summary>
        private void OnRailPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is not (null
                or ""
                or nameof(InspectorRailWidthViewModel.Width)
                or nameof(InspectorRailWidthViewModel.EffectiveWidth)))
            {
                return;
            }

            Sync();
        }

        /// <summary>
        /// The rail host appeared or went away — the user opened a section that has a rail, or one
        /// that has none. The column follows, because a fixed-pixel column does not.
        /// </summary>
        private void OnRailHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.IsVisibleProperty)
            {
                Sync();
            }
        }

        /// <summary>
        /// Resolves a geometry token, or keeps <paramref name="fallback"/> where the view is hosted
        /// without the app's resources — which is every design-time preview.
        /// </summary>
        private double Measure(string key, double fallback)
        {
            return _host.TryFindResource(key, out var value) && value is double measure ? measure : fallback;
        }

        /// <summary>
        /// Which of the model's two numbers a column draws its rail at.
        /// <para>
        /// It used to be what separated two call sites, one per tab. Issue #128 left one column and
        /// therefore one call site, and it reads <see cref="Effective"/>: a single column has a
        /// single width, and picking a different number per tab would move the seam — and with it
        /// the board — every time the user switched section. The distinction is kept rather than
        /// folded away because the two numbers are still two different questions, and a future
        /// second column (an editor with a rail on both sides, another board's screen) would have to
        /// answer one of them.
        /// </para>
        /// </summary>
        internal enum WidthSource
        {
            /// <summary>
            /// The user's own width, verbatim, with no panel floor over it.
            /// </summary>
            Stored,

            /// <summary>
            /// The user's width, floored at the handoff's 300 px while the Macro panel is showing.
            /// </summary>
            Effective
        }
    }
}
