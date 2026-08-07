using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector's Macro panel, resolved from <see cref="MacroInspectorPanelViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the <b>drag</b>
    /// half of mockup <c>2i</c>'s "drag ⠿ · ⌥↑↓". The keyboard half is the editor's own grammar
    /// (<c>Input/EditorShortcuts</c> → <c>MoveMacroStepUp/DownCommand</c>) and needs nothing from
    /// this file.
    /// <para>
    /// <b>It is a press-and-release gesture, not <c>DragDrop.DoDragDrop</c>.</b> The platform drag
    /// source is a shell service — it does not exist on the headless platform the UI suite runs on,
    /// and its API is obsolete in this Avalonia version — while a reorder inside one list needs
    /// nothing from the OS. Press on a row, drag to another, release: the two 1-based positions are
    /// handed to <see cref="MacroInspectorStepsViewModel.MoveStep"/>, which is the same one method
    /// <c>⌥↑↓</c> runs a step at a time. Every reorder rule (a step moves with the delay folded
    /// behind it) is the view model's and is covered without a pointer.
    /// </para>
    /// <para>
    /// <b>Three things about the pointer make or break it, and the first two shipped wrong.</b>
    /// </para>
    /// <para>
    /// 1. <b>The arm is attached here, not in the markup, and it takes handled events.</b> Column 1
    /// of a row is a <c>Button.macroStepRow</c>, and a button sets <c>e.Handled = true</c> on a left
    /// press — so a <c>PointerPressed="…"</c> written in the <c>DataTemplate</c> attaches with
    /// <c>handledEventsToo: false</c> and never sees a press on the row <em>body</em>. Only the
    /// 12 px grip and the column gaps armed the drag, which is most of why it read as "I can't drag
    /// anything".
    /// </para>
    /// <para>
    /// 2. <b>The drop row is resolved from the release <em>position</em>, never from
    /// <c>e.Source</c>.</b> Avalonia implicitly captures the pointer to the hit-tested control on
    /// press, so <c>PointerReleased</c> is raised on the captured element and its source is the row
    /// the drag <em>started</em> from — never whatever is under the pointer at the end. Trusting it
    /// made every drag a <c>MoveStep(from, from)</c>, which the view model's own guard answers false
    /// to. <c>InputHitTest</c> over <c>e.GetPosition(this)</c> is what actually names the row
    /// released on.
    /// </para>
    /// <para>
    /// 3. <b>The capture is taken over once the gesture is a drag.</b> Past
    /// <see cref="DragThresholdPixels"/> the pointer is captured to this panel: the row button loses
    /// its own capture and therefore does not fire the click that would select the source row, the
    /// moves keep arriving while the pointer is outside the row, and — the part a stale gesture
    /// depends on — a capture lost to anything else raises
    /// <see cref="InputElement.PointerCaptureLostEvent"/> <em>on this panel</em>, which is where the
    /// drag state is dropped. Below the threshold nothing is taken and the press is the ordinary
    /// click that selects the step.
    /// </para>
    /// </summary>
    public partial class MacroInspectorPanelView : UserControl
    {
        /// <summary>
        /// How far the pointer must travel before a press becomes a drag. Under it the gesture is a
        /// click — the row button keeps its capture, fires, and selects the step — so a user who
        /// taps a row to point <c>⌥↑↓</c> at it never silently reorders anything.
        /// </summary>
        private const double DragThresholdPixels = 4;

        /// <summary>1-based position of the row the pointer went down on, or 0 while none is held.</summary>
        private int _dragFromPosition;

        /// <summary>Where that press landed, in this panel's coordinates — what the threshold measures from.</summary>
        private Point _dragOrigin;

        /// <summary>The row currently wearing the drop ring, or null.</summary>
        private MacroInspectorStepViewModel? _dropTarget;

        /// <summary>Creates the panel view.</summary>
        public MacroInspectorPanelView()
        {
            InitializeComponent();

            // All three on this control and all three with handledEventsToo: the row button handles
            // the press and the release itself, and a handler that declines handled events sees
            // neither anywhere on the row's body.
            AddHandler(PointerPressedEvent, OnPointerPressedOverStep, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerMovedEvent, OnPointerMovedOverStep, RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnPointerReleasedOverStep, RoutingStrategies.Bubble, handledEventsToo: true);

            // Direct, so it is only ever this panel's own capture being reported — which is exactly
            // the one the drag takes at the threshold. Without it a capture stolen mid-gesture
            // (a flyout, a window deactivation) would leave an armed position to fire on the next
            // unrelated release.
            AddHandler(PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
        }

        /// <summary>
        /// Arms the drag. Left button only, and on any part of a real step row — the grip is what
        /// says the row can be dragged, but the whole row is the target, because a 12 px mark is not
        /// one.
        /// </summary>
        private void OnPointerPressedOverStep(object? sender, PointerPressedEventArgs e)
        {
            Disarm();

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
                || FindStep(e.Source as Control) is not { } step)
            {
                return;
            }

            _dragFromPosition = step.Position;
            _dragOrigin = e.GetPosition(this);
        }

        /// <summary>
        /// Follows the carried step: takes the capture once the press has travelled far enough to be
        /// a drag, and rings the row the drop would land on.
        /// </summary>
        private void OnPointerMovedOverStep(object? sender, PointerEventArgs e)
        {
            if (_dragFromPosition == 0)
            {
                return;
            }

            var position = e.GetPosition(this);

            if (!IsPastThreshold(position))
            {
                return;
            }

            if (!ReferenceEquals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(this);
            }

            ShowDropTarget(FindStepAt(position));
        }

        /// <summary>
        /// Lands the drag on the row under the pointer — found by hit-testing the release
        /// <em>position</em>, because the source of a released captured pointer is the row the drag
        /// started from. A press that never moved is not a reorder at all; it is the click that
        /// selects the step, and the row button has already run it.
        /// </summary>
        private void OnPointerReleasedOverStep(object? sender, PointerReleasedEventArgs e)
        {
            var fromPosition = _dragFromPosition;
            var origin = _dragOrigin;
            var position = e.GetPosition(this);

            Disarm();

            if (fromPosition == 0
                || !IsPastThreshold(position, origin)
                || DataContext is not MacroInspectorPanelViewModel panel
                || FindStepAt(position) is not { } target)
            {
                return;
            }

            panel.Steps.MoveStep(fromPosition - 1, target.Position - 1);
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            Disarm();
        }

        private bool IsPastThreshold(Point position)
        {
            return IsPastThreshold(position, _dragOrigin);
        }

        private static bool IsPastThreshold(Point position, Point origin)
        {
            var delta = position - origin;

            return Math.Abs(delta.X) >= DragThresholdPixels || Math.Abs(delta.Y) >= DragThresholdPixels;
        }

        /// <summary>Drops the armed position and the ring; every end of a gesture goes through it.</summary>
        private void Disarm()
        {
            _dragFromPosition = 0;
            _dragOrigin = default;

            ShowDropTarget(null);
        }

        /// <summary>
        /// Rings <paramref name="step"/> and un-rings whatever wore it before. The row the drag
        /// started from is not a drop target: releasing on it moves nothing.
        /// </summary>
        private void ShowDropTarget(MacroInspectorStepViewModel? step)
        {
            var target = step is null || step.Position == _dragFromPosition ? null : step;

            if (ReferenceEquals(_dropTarget, target))
            {
                return;
            }

            if (_dropTarget is not null)
            {
                _dropTarget.IsDropTarget = false;
            }

            _dropTarget = target;

            if (_dropTarget is not null)
            {
                _dropTarget.IsDropTarget = true;
            }
        }

        /// <summary>The step row under <paramref name="position"/> in this panel, or null.</summary>
        private MacroInspectorStepViewModel? FindStepAt(Point position)
        {
            return FindStep(this.InputHitTest(position) as Control);
        }

        /// <summary>
        /// The step row a hit landed in, found by walking up from whatever was hit — the grip, a
        /// caption or the row itself. It reads the <c>DataContext</c> rather than a template part,
        /// which is what keeps this out of somebody else's template. The walk is over the
        /// <b>visual</b> tree: a hit inside a templated button lands on a presenter whose logical
        /// parent is the template, not the row.
        /// </summary>
        private static MacroInspectorStepViewModel? FindStep(Control? hit)
        {
            for (var control = hit; control is not null; control = control.GetVisualParent() as Control)
            {
                if (control.DataContext is MacroInspectorStepViewModel step)
                {
                    return step;
                }
            }

            return null;
        }
    }
}
