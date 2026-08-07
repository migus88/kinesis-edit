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
    /// <para>
    /// <b>4. What the drag looks like (issue #128).</b> It used to show only where the drop would
    /// land, so the step being carried was invisible and nothing in the list moved until the pointer
    /// came up — "the view is only updated after the movement". From the threshold the source row now
    /// wears <see cref="MacroInspectorStepViewModel.IsDragSource"/> and dims in place, and a
    /// <b>ghost</b> — the same row face, on the popover elevation — is written to <c>DragLayer</c>
    /// at the pointer on every move. The ghost is <c>IsHitTestVisible="False"</c>, which is not a
    /// nicety: the drop row is found by hit-testing the release <em>position</em>, so a hittable
    /// ghost would sit under the pointer and answer every one of those tests itself. Neither state
    /// animates — the ghost is pinned to the pointer, and a transition on its position would make it
    /// lag the very gesture it exists to show — so nothing was added to <c>Themes/Motion.axaml</c>.
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

        /// <summary>
        /// Where inside the pressed row that press landed. The ghost is offset by it, so the row
        /// keeps the same relation to the pointer it had when it was picked up rather than jumping
        /// its own corner under the cursor.
        /// </summary>
        private Point _dragGrabOffset;

        /// <summary>The row currently wearing the drop ring, or null.</summary>
        private MacroInspectorStepViewModel? _dropTarget;

        /// <summary>The row being carried — the one wearing <c>IsDragSource</c> — or null.</summary>
        private MacroInspectorStepViewModel? _dragSource;

        /// <summary>The row the pointer went down on, or null. Not yet carried; see <see cref="_dragSource"/>.</summary>
        private MacroInspectorStepViewModel? _pressedStep;

        /// <summary>How wide that row was, so the ghost is a copy of it rather than a shrink-wrapped label.</summary>
        private double _pressedRowWidth;

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

            var position = e.GetPosition(this);

            _dragFromPosition = step.Position;
            _dragOrigin = position;
            _pressedStep = step;

            // Measured on the press rather than on the threshold: by the time the gesture is a drag
            // the pointer has left the row, and the ghost has to be the row as it was picked up.
            if (FindStepRoot(e.Source as Control) is { } row
                && row.TranslatePoint(new Point(0, 0), this) is { } origin)
            {
                _dragGrabOffset = position - origin;
                _pressedRowWidth = row.Bounds.Width;
            }
        }

        /// <summary>
        /// Follows the carried step: takes the capture once the press has travelled far enough to be
        /// a drag, lifts the source row into the ghost, keeps the ghost under the pointer, and rings
        /// the row the drop would land on.
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

            // Idempotent: the first move past the threshold lifts the row, every later one only
            // moves the ghost it produced.
            ShowDragSource(_pressedStep);
            MoveGhostTo(position);

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

        /// <summary>
        /// Drops the armed position, the ring and the carried row; every end of a gesture goes
        /// through it — the release, a capture lost to anything else, and the next press.
        /// </summary>
        private void Disarm()
        {
            _dragFromPosition = 0;
            _dragOrigin = default;
            _dragGrabOffset = default;
            _pressedStep = null;
            _pressedRowWidth = 0;

            ShowDropTarget(null);
            ShowDragSource(null);
        }

        /// <summary>
        /// Lifts <paramref name="step"/> out of the list: the row itself dims through
        /// <see cref="MacroInspectorStepViewModel.IsDragSource"/> and the ghost takes its face and
        /// its width. Null puts it back. Called on every move, so it does nothing when the state has
        /// not changed.
        /// </summary>
        private void ShowDragSource(MacroInspectorStepViewModel? step)
        {
            if (ReferenceEquals(_dragSource, step))
            {
                return;
            }

            if (_dragSource is not null)
            {
                _dragSource.IsDragSource = false;
            }

            _dragSource = step;

            if (_dragSource is not null)
            {
                _dragSource.IsDragSource = true;
            }

            DragGhostContent.Content = _dragSource;

            if (_pressedRowWidth > 0)
            {
                DragGhost.Width = _pressedRowWidth;
            }

            DragGhost.IsVisible = _dragSource is not null;
        }

        /// <summary>
        /// Pins the ghost to the pointer, keeping the grab point the press had inside the row. Set
        /// directly rather than through a transition: the ghost <em>is</em> the pointer's position
        /// made visible, and anything easing towards it would lag the gesture.
        /// </summary>
        private void MoveGhostTo(Point position)
        {
            if (_dragSource is null)
            {
                return;
            }

            Canvas.SetLeft(DragGhost, position.X - _dragGrabOffset.X);
            Canvas.SetTop(DragGhost, position.Y - _dragGrabOffset.Y);
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

        /// <summary>
        /// The whole row a hit landed in — the <b>outermost</b> control still bound to that step, and
        /// so the one whose bounds are the row's. <see cref="FindStep"/> stops at the first match,
        /// which inside a templated button is a text run; the ghost needs the row.
        /// </summary>
        private static Control? FindStepRoot(Control? hit)
        {
            Control? root = null;

            for (var control = hit; control is not null; control = control.GetVisualParent() as Control)
            {
                if (control.DataContext is MacroInspectorStepViewModel)
                {
                    root = control;
                }
            }

            return root;
        }
    }
}
