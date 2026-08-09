using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The editor's Lighting tab (design mockup 2f): the board rendering the selected mode and the
    /// paint line under it. Since issue #128 that is <b>all</b> of it — the mode rail beside it is
    /// the editor's own rail column, the seam that resizes it belongs to that column, and the
    /// lighting layer switch is in the tab bar's one switcher slot
    /// (<see cref="KeyboardEditorView"/>). Everything it shows is bound; the code here is two
    /// behaviours markup cannot express.
    /// <para>
    /// <b>The frame timer.</b> The board animates, which is the one per-frame repaint in the app
    /// (<c>DurationLightingPreviewFrame</c>, ~30 fps — Themes/Motion.axaml). The timer does nothing
    /// but hand the real elapsed seconds to <see cref="LightingTabViewModel.AdvancePreview"/>: the
    /// clock, the sampler and the reduce-motion decision are all the view model's, and it re-reads
    /// the motion preference on every call. <b>Nothing here reads it</b> — a timer that decided for
    /// itself would be a second answer to a question that already has one.
    /// </para>
    /// <para>
    /// <b>Not ticking off screen.</b> The tab is hosted in a <see cref="ContentControl"/> whose own
    /// <c>IsVisible</c> is bound, so switching sections does <i>not</i> detach this view — it merely
    /// stops being effectively visible, and a preview repainting ~76 caps behind the Settings tab
    /// or behind the dashboard is a defect rather than a cost. Avalonia's
    /// <c>IsEffectivelyVisible</c> is not an <c>AvaloniaProperty</c> and cannot be observed, so the
    /// view watches <c>IsVisible</c> on itself <i>and on every visual ancestor</i> — which is what
    /// the hosting <see cref="ContentControl"/> actually flips — and recomputes from the chain.
    /// </para>
    /// <para>
    /// <b>Shift-click.</b> <see cref="KeyboardView"/> exposes one command per click and knows
    /// nothing about modifiers, so the extend gesture is routed here: a tunnelling
    /// <c>PointerPressed</c> handler catches a shifted press on a cap before the cap's own button
    /// sees it and runs <see cref="LightingTabViewModel.ExtendSelectionCommand"/> instead of the
    /// toggle. Marking it handled is what stops the plain click running as well.
    /// </para>
    /// <para>
    /// <b>The mode rail's column is no longer this view's</b> (issue #128). The rail was drawn here
    /// with a <see cref="InspectorRailColumn"/> of its own, over a column of its own, reading the
    /// width the Keys tab's rail also read; there is one column now and
    /// <see cref="KeyboardEditorView"/> owns it, so two grids can no longer disagree about where a
    /// rail of one width starts.
    /// </para>
    /// </summary>
    public partial class LightingTabView : UserControl
    {
        /// <summary>
        /// The swatch rule 2f prints beside the paint actions, verbatim. It is the legend for the
        /// board: an unlit key wears the hatch tile, and a key painted black is an <i>unpainted</i>
        /// key (black is "no colour", specs/07-lighting.md §2.1), so the two are the same picture.
        /// </summary>
        public const string HatchRule = "hatched = off, not black";

        /// <summary>
        /// The frame interval's key in Themes/Motion.axaml. The budget owns the number; this view
        /// reads it rather than restating it, and <see cref="DefaultFrameInterval"/> is only what a
        /// host with no application resources falls back to.
        /// </summary>
        private const string FrameIntervalKey = "DurationLightingPreviewFrame";

        /// <summary>~30 fps, the budget's own value, used only if the resource cannot be resolved.</summary>
        public static readonly TimeSpan DefaultFrameInterval = TimeSpan.FromMilliseconds(33);

        /// <summary>How often the preview is advanced; read off the motion budget on attach.</summary>
        public TimeSpan FrameInterval => _frameTimer.Interval;

        /// <summary>
        /// Whether the preview is being advanced right now. False whenever the tab is off screen —
        /// hidden, behind another section, or detached — and false again the moment it goes.
        /// </summary>
        public bool IsPreviewRunning => _frameTimer.IsEnabled;

        private readonly DispatcherTimer _frameTimer;
        private readonly List<Visual> _watchedVisuals = [];

        private long _lastTimestamp;

        /// <summary>Creates the lighting view and its (stopped) frame timer.</summary>
        public LightingTabView()
        {
            InitializeComponent();

            _frameTimer = new DispatcherTimer { Interval = DefaultFrameInterval };
            _frameTimer.Tick += OnFrame;

            // Tunnelling, because the cap's own Button would otherwise consume the press and run
            // the plain toggle: the modifier has to be read on the way down.
            AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);

            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// Reads the frame interval off the motion budget, starts watching the visibility chain, and
        /// runs the preview if the tab is actually on screen.
        /// </summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _frameTimer.Interval = ResolveFrameInterval();

            WatchVisibilityChain();
            UpdatePreviewState();
        }

        /// <summary>
        /// Stops the preview and lets go of the chain. A timer left running on a detached view keeps
        /// the view model — and the whole board — alive and repainting for nobody.
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            StopWatchingVisibilityChain();
            StopPreview();

            base.OnDetachedFromVisualTree(e);
        }

        /// <summary>
        /// A shifted press on a key cap extends the paint selection over the layer's key order
        /// instead of toggling one cap. It is handled here rather than in
        /// <see cref="KeyboardView"/> because the picture reports a click and deliberately knows
        /// nothing about modifiers — the Layout tab has no use for one.
        /// </summary>
        private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not LightingTabViewModel viewModel || !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                return;
            }

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.Source is not Visual source
                || source.FindAncestorOfType<KeyCapView>(includeSelf: true) is not { } cap
                || cap.DataContext is not KeyboardKeyViewModel key)
            {
                return;
            }

            viewModel.ExtendSelectionCommand.Execute(key);

            // Without this the cap's Button would run the plain toggle on release as well, and a
            // shift-click would extend the run and then take one key back out of it.
            e.Handled = true;
        }

        /// <summary>Advances the preview by however long has really passed since the last frame.</summary>
        private void OnFrame(object? sender, EventArgs e)
        {
            var now = Stopwatch.GetTimestamp();
            var elapsedSeconds = (now - _lastTimestamp) / (double)Stopwatch.Frequency;

            _lastTimestamp = now;

            (DataContext as LightingTabViewModel)?.AdvancePreview(elapsedSeconds);
        }

        private void OnDataContextChanged(object? sender, EventArgs e)
        {
            UpdatePreviewState();
        }

        private void OnWatchedVisualPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty)
            {
                UpdatePreviewState();
            }
        }

        private void UpdatePreviewState()
        {
            if (DataContext is LightingTabViewModel && IsOnScreen())
            {
                StartPreview();

                return;
            }

            StopPreview();
        }

        /// <summary>
        /// Whether the tab is really being drawn: attached, and visible at every step between here
        /// and the root. The chain is walked rather than <c>IsEffectivelyVisible</c> read, because
        /// the order in which Avalonia updates that flag against our own change notification is not
        /// something a view should depend on.
        /// </summary>
        private bool IsOnScreen()
        {
            if (this.GetVisualRoot() is null)
            {
                return false;
            }

            foreach (var visual in this.GetSelfAndVisualAncestors())
            {
                if (!visual.IsVisible)
                {
                    return false;
                }
            }

            return true;
        }

        private void StartPreview()
        {
            if (_frameTimer.IsEnabled)
            {
                return;
            }

            // Reset rather than carry: the gap since the tab was last on screen is not elapsed
            // animation time, and handing it over would make the effect jump on the way back.
            _lastTimestamp = Stopwatch.GetTimestamp();

            _frameTimer.Start();
        }

        private void StopPreview()
        {
            _frameTimer.Stop();
        }

        private void WatchVisibilityChain()
        {
            StopWatchingVisibilityChain();

            foreach (var visual in this.GetSelfAndVisualAncestors())
            {
                visual.PropertyChanged += OnWatchedVisualPropertyChanged;

                _watchedVisuals.Add(visual);
            }
        }

        private void StopWatchingVisibilityChain()
        {
            foreach (var visual in _watchedVisuals)
            {
                visual.PropertyChanged -= OnWatchedVisualPropertyChanged;
            }

            _watchedVisuals.Clear();
        }

        private TimeSpan ResolveFrameInterval()
        {
            if (this.TryFindResource(FrameIntervalKey, out var value)
                && value is TimeSpan interval
                && interval > TimeSpan.Zero)
            {
                return interval;
            }

            return DefaultFrameInterval;
        }
    }
}
