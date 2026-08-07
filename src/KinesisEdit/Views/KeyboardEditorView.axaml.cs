using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Input;
using KinesisEdit.ViewModels;

// Avalonia's own NavigationDirection is the tab-order one; the board's is Core's. The grammar's
// arrows move "across the physical grid, not tab order", which is exactly this distinction.
using NavigationDirection = KinesisEdit.Core.Geometry.Visual.NavigationDirection;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The keyboard editor view, resolved from <see cref="KeyboardEditorViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the editor's
    /// keyboard grammar — the Escape route out of an open feature panel, out of the remap's
    /// listening state, out of an armed <c>Copy key…</c> and finally out of the selection itself,
    /// the arrow/⌥n/⌘F/⌘S/⌘W table of docs/design/mockups.md <c>2b</c> — plus the two selection
    /// handlers that turn a segment or a tab being chosen back into the command the buttons they
    /// replaced used to run, the three <b>recording stand-down</b> handlers this view puts on the
    /// window (see <see cref="AttachShellHandlers"/>), and the one thing a binding cannot do: the
    /// key inspector rail's column width (see <see cref="InspectorRailColumn"/>).
    /// </summary>
    public partial class KeyboardEditorView : UserControl
    {
        /// <summary>
        /// Which column of the Layout tab's grid the rail sits in — board, seam, rail. The width is
        /// carried by the <b>column</b> since issue #119, because that is what a
        /// <see cref="GridSplitter"/> resizes.
        /// </summary>
        private const int RailColumnIndex = 2;

        /// <summary>
        /// Phases the shell's focus handler is attached for. <see cref="InputElement.GotFocusEvent"/>
        /// is registered as a bubbling event, which is the phase that actually delivers it to the
        /// <see cref="TopLevel"/>; the tunnel flag is defensive, exactly as
        /// <see cref="AvaloniaKeystrokeCaptureService"/> attaches its own.
        /// </summary>
        private const RoutingStrategies FocusRoutes = RoutingStrategies.Tunnel | RoutingStrategies.Bubble;

        /// <summary>
        /// The rail column's driver. It is the <b>same</b> mechanism the Lighting tab's mode rail
        /// runs on since issue #124 — the two rails are two contents of one resizable column — and
        /// it reads <see cref="InspectorRailColumn.WidthSource.Effective"/> here, because this is
        /// the tab whose Macro panel is entitled to the handoff's 300 px.
        /// </summary>
        private readonly InspectorRailColumn _railColumn;

        /// <summary>
        /// The window the stand-down handlers are attached to, so the same instance is unhooked
        /// again when this view leaves the tree — the shell reuses views, and a handler left on an
        /// old window would keep answering for an editor that is gone.
        /// </summary>
        private TopLevel? _shell;

        private readonly EventHandler<PointerPressedEventArgs> _shellPointerPressedHandler;
        private readonly EventHandler<GotFocusEventArgs> _shellGotFocusHandler;
        private readonly EventHandler _shellDeactivatedHandler;

        /// <summary>Creates the editor view.</summary>
        public KeyboardEditorView()
        {
            InitializeComponent();

            _shellPointerPressedHandler = OnShellPointerPressed;
            _shellGotFocusHandler = OnShellGotFocus;
            _shellDeactivatedHandler = OnShellDeactivated;

            _railColumn = new InspectorRailColumn(
                this,
                LayoutColumns,
                RailColumnIndex,
                InspectorRailColumn.WidthSource.Effective);

            // Tunneling, as in MessageBoxView: Escape must leave the listening state whatever
            // has focus, instead of being swallowed by the focused key cap. handledEventsToo is
            // set because the keystroke-capture service previews the same event on the window
            // above us and marks it handled while a key is listening
            // (docs/app/keystroke-capture.md).
            //
            // One handler carries the whole grammar. There are deliberately no KeyBindings and no
            // KeyGestures anywhere in this app: an accelerator fires wherever it is in scope, and
            // the three gates below — capture owns the keyboard; an open modal owns it too; the
            // focused surface owns its own arrows — are decisions no gesture can express.
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

            // A tunnelling key event only travels the chain between the window and whatever holds
            // focus, so a handler on this control sees nothing at all while focus is outside it —
            // and a freshly opened editor has focus nowhere inside it. The view therefore takes
            // focus itself on the way in (see OnAttachedToVisualTree), which is why it has to be
            // focusable. It is deliberately NOT a tab stop: Tab still walks the toolbar, the tabs,
            // the layer switcher and the board exactly as before.
            Focusable = true;
            IsTabStop = false;
        }

        /// <summary>
        /// Follows whichever editor the shell hands this view, so the rail column keeps the width
        /// that editor stores. Dropping the outgoing subscription is
        /// <see cref="InspectorRailColumn.Follow"/>'s own job.
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            _railColumn.Follow((DataContext as KeyboardEditorViewModel)?.Rail);
        }

        /// <summary>
        /// Takes focus unless something inside the editor already has it, so the grammar works from
        /// the moment the editor opens rather than only after the first click. Anything the user
        /// then focuses — a cap, a tab, a field — is a descendant of this view, so the tunnel route
        /// still passes through <see cref="OnPreviewKeyDown"/>.
        /// </summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            AttachShellHandlers();

            // A DataContext that arrived before this view was in a tree could not resolve the two
            // geometry tokens, so the band is put on the column here as well.
            _railColumn.Sync();

            if (IsKeyboardFocusWithin || Focus())
            {
                return;
            }

            // A window that has not laid out yet refuses focus; the first idle pass after it has.
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (!IsKeyboardFocusWithin)
                    {
                        Focus();
                    }
                },
                DispatcherPriority.Loaded);
        }

        /// <summary>Drops the stand-down handlers with the window they were put on.</summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DetachShellHandlers();

            base.OnDetachedFromVisualTree(e);
        }

        /// <summary>
        /// Puts the three <b>recording stand-down</b> handlers on the window (issue #122, AC 3).
        /// <para>
        /// On the window rather than on this view, because two of the three interactions happen
        /// outside it: the editor draws the shell's one bar itself (<c>IShellChrome</c>), and a
        /// window losing focus is a fact about the window. A tunnelled pointer press is what makes
        /// the seam see the press <em>before</em> the control it lands on acts on it, and
        /// <c>handledEventsToo</c> is what keeps a button's own handling from hiding it.
        /// </para>
        /// <para>
        /// Nothing here consumes anything: the press still selects the cap, presses the button or
        /// moves the caret it was aimed at. The handlers only answer "the keyboard is not the
        /// macro's any more".
        /// </para>
        /// </summary>
        private void AttachShellHandlers()
        {
            if (_shell is not null || TopLevel.GetTopLevel(this) is not { } shell)
            {
                return;
            }

            _shell = shell;

            shell.AddHandler(
                InputElement.PointerPressedEvent,
                _shellPointerPressedHandler,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            shell.AddHandler(InputElement.GotFocusEvent, _shellGotFocusHandler, FocusRoutes, handledEventsToo: true);

            if (shell is WindowBase window)
            {
                window.Deactivated += _shellDeactivatedHandler;
            }
        }

        /// <summary>Takes them off again. Idempotent, and always off the window they went on.</summary>
        private void DetachShellHandlers()
        {
            if (_shell is not { } shell)
            {
                return;
            }

            _shell = null;

            shell.RemoveHandler(InputElement.PointerPressedEvent, _shellPointerPressedHandler);
            shell.RemoveHandler(InputElement.GotFocusEvent, _shellGotFocusHandler);

            if (shell is WindowBase window)
            {
                window.Deactivated -= _shellDeactivatedHandler;
            }
        }

        /// <summary>
        /// A pointer press anywhere in the app ends a macro recording — <b>except</b> on the
        /// panel's own record control. Without that exemption the seam stands the recording down on
        /// the very press that was meant to start it, and fights <c>Stop</c> into needing two
        /// clicks: this handler runs on the tunnel, before the button has done anything, so the
        /// toggle it then runs would simply turn recording back on.
        /// </summary>
        private void OnShellPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel
                || IsRecordingControlUnder(e.Source as Visual, viewModel))
            {
                return;
            }

            viewModel.StopRecordingOnInteraction();
        }

        /// <summary>
        /// Focus landing in a text input ends the recording too — <b>an explicit stand-down in
        /// place of a silent suspension</b>. The capture service's own answer is to suspend while a
        /// <see cref="TextBox"/> has focus (docs/app/keystroke-capture.md), which is right for a
        /// dialog that needs real typing and wrong here: the panel would still say it was
        /// recording while the keystrokes went into the box. Reachable in this editor through the
        /// §11.3 delay editor's millisecond field, by click and by Tab alike — which is why it is a
        /// focus handler and not merely a pointer one.
        /// </summary>
        private void OnShellGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel || e.Source is not Visual focused)
            {
                return;
            }

            if (focused.GetSelfAndVisualAncestors().Any(element => element is TextBox))
            {
                viewModel.StopRecordingOnInteraction();
            }
        }

        /// <summary>
        /// The app lost focus. Capture is focused-window only, so a recording left armed here would
        /// come back to life pointing at whatever the user does next — and the OS-reserved chords
        /// that cause it (⌘Tab, ⌘Q, ⌘Space, ⌘H) never reach the app at all and can be neither
        /// captured nor swallowed. This is what recovers from one cleanly.
        /// </summary>
        private void OnShellDeactivated(object? sender, EventArgs e)
        {
            (DataContext as KeyboardEditorViewModel)?.StopRecordingOnInteraction();
        }

        /// <summary>
        /// Whether the press landed on the showing panel's record control. The question is asked of
        /// the <b>command</b> a <see cref="Button"/> under the pointer runs rather than of a named
        /// part: naming a template part outside <c>Themes/ControlThemes/</c> is a bug in this
        /// codebase, and a panel is the only thing that knows which of its buttons arm capture.
        /// </summary>
        private static bool IsRecordingControlUnder(Visual? source, KeyboardEditorViewModel viewModel)
        {
            if (source is null)
            {
                return false;
            }

            foreach (var ancestor in source.GetSelfAndVisualAncestors())
            {
                if (ancestor is Button button && viewModel.IsRecordingControl(button.Command))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The editor's whole keyboard grammar, in tunnel order, behind three gates.
        /// <para>
        /// <b>Gate 1 — capture wins, always.</b> While a key is listening, a macro is recording or
        /// a Tap and Hold field is armed (<see cref="KeyboardEditorViewModel.IsCaptureActive"/>)
        /// <em>no</em> shortcut is handled: the keystroke belongs to exactly one consumer
        /// (docs/app/keyboard-editor.md, invariant 5) and the capture service on the window above
        /// has already marked it handled. A user assigning ⌘S to a key gets <c>s</c>-with-Meta
        /// recorded, not a save.
        /// </para>
        /// <para>
        /// <b>Gate 2 — an open modal owns the keyboard.</b> While a feature panel is up
        /// (<see cref="KeyboardEditorViewModel.HasActiveOverlay"/>) <em>no</em> shortcut fires
        /// either — Escape excepted, which is what closes the panel. Gate 1 does not cover this: a
        /// panel with no field armed is not capturing, so ⌘S would otherwise start serializing the
        /// model on a background thread while the panel above the scrim is still writing to it.
        /// </para>
        /// <para>
        /// <b>Gate 3 — the focused surface keeps what is its own.</b> Arrows are left untouched
        /// when focus sits in a text input, a one-of-N or a range control
        /// (<see cref="BoardOwnsArrows"/>); ⌥digits only when focus sits in a text input
        /// (<see cref="BoardOwnsLayerShortcuts"/>), because nothing else consumes Alt+digit.
        /// ⌘S/⌘W/⌘F pass this gate entirely by design: a save accelerator that stops working while
        /// the caret is in a field is not an accelerator.
        /// </para>
        /// </summary>
        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel)
            {
                return;
            }

            // Read on every key down, not only on Escape: the latch is about the keystroke being
            // handled right now, and one left standing would answer for a later key.
            var takenByOverlay = viewModel.TryTakeOverlayKeystroke();

            if (e.Key == Key.Escape)
            {
                HandleEscape(viewModel, e, takenByOverlay);

                return;
            }

            if (viewModel.IsCaptureActive || viewModel.HasActiveOverlay)
            {
                return;
            }

            var shortcut = EditorShortcuts.Map(e.Key, e.KeyModifiers, KeyCaption.IsMacOs);

            if (shortcut == EditorShortcut.None)
            {
                return;
            }

            if (Run(viewModel, shortcut))
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Escape is a remappable key, not a shortcut: while a key is listening — or while a Tap
        /// and Hold field is armed — the capture service consumes this event on the window above
        /// us and assigns it.
        /// <para>
        /// An open feature panel is therefore dismissed on Escape <b>whatever <c>e.Handled</c>
        /// says</b>, unless the panel itself is the thing waiting for the keystroke — capture may
        /// be running for something else entirely, and a panel a user cannot close with the
        /// keyboard is worse than an Escape that also fills an armed field.
        /// </para>
        /// <para>
        /// "Waiting for the keystroke" is <b>two</b> questions, not one.
        /// <see cref="KeyboardEditorViewModel.IsOverlayAwaitingKeystroke"/> answers "is a field
        /// armed <em>now</em>", and <paramref name="takenByOverlay"/> answers "did the panel take
        /// <em>this</em> key already" — because an armed field disarms as it takes the key, and the
        /// capture service runs above us, so by the time this handler sees the Escape the panel can
        /// look idle. Without the second question one Escape both filled the field and destroyed
        /// the panel. It stands down for that keystroke, so the <em>next</em> Escape closes the
        /// panel.
        /// </para>
        /// <para>
        /// <b>The order is explicit: panel, then capture, then an armed copy, then the selection.</b>
        /// `2b`'s grammar says Escape "leaves capture mode first", so the listening key is cancelled
        /// before the armed <c>Copy key…</c> pick — even though the editor never lets both be live at
        /// once (arming a copy ends a listen, and starting a remap ends a copy), which is what keeps
        /// this a stated order rather than a lucky one. A copy is armed by a click and finished by a
        /// click, so nothing swallows the Escape that cancels it. <b>The first three stages are
        /// unchanged by issue #119; only the fourth is.</b>
        /// </para>
        /// <para>
        /// <b>The last stage clears the key selection (issue #119).</b> It used to close the rail —
        /// which was the widest thing on screen Escape could plausibly mean, back when the rail was
        /// a thing that could be closed. It cannot be any more: it is a permanent column, so
        /// dismissing it would have to collapse a column and shove the board sideways, which is the
        /// very defect this stage now avoids. Deselecting reaches the same end from the other side
        /// — the cap loses its ring, the rail falls to its empty state, and Escape still means
        /// "back out of the narrowest thing I am in". It is still last, and still behind everything
        /// narrower.
        /// </para>
        /// <para>
        /// <b>The latch is checked once, for the whole method (issue #122).</b> It used to gate only
        /// the first stage, while the doc claimed it covered the last one "exactly as it covers a
        /// panel" — and the last stage clears the selection, which refreshes the rail with a new
        /// (null) key, which stands the recording down. So one Escape was appended as a macro step
        /// <em>and</em> ended the recording that took it. Escape is a remappable position like any
        /// other, so a macro must be able to record one: a keystroke somebody has already consumed
        /// is not also a command, and nothing below this line runs for it.
        /// </para>
        /// <para>
        /// Nothing is handled when there is no selection, so Escape falls through untouched on an
        /// editor the user has not clicked into — an accelerator that swallows a key it does nothing
        /// with is worse than one that does not fire.
        /// </para>
        /// </summary>
        private static void HandleEscape(KeyboardEditorViewModel viewModel, KeyEventArgs e, bool takenByOverlay)
        {
            if (takenByOverlay)
            {
                return;
            }

            if (!viewModel.IsOverlayAwaitingKeystroke && viewModel.CloseOverlayCommand.CanExecute(null))
            {
                e.Handled = true;

                viewModel.CloseOverlayCommand.Execute(null);

                return;
            }

            if (viewModel.CancelRemapCommand.CanExecute(null))
            {
                e.Handled = true;

                viewModel.CancelRemapCommand.Execute(null);

                return;
            }

            if (viewModel.CancelCopyKeyCommand.CanExecute(null))
            {
                e.Handled = true;

                viewModel.CancelCopyKeyCommand.Execute(null);

                return;
            }

            if (viewModel.SelectedKey is null)
            {
                return;
            }

            e.Handled = true;

            // The editor's own "nothing is selected" path: it cancels a listen, drops the cap's
            // ring and pushes the rail — and the Macros tab's slot branch — through the same funnel
            // a click on empty board space does. Reimplementing any of that here would be a second
            // set of rules to keep in step.
            viewModel.SelectKeyCommand.Execute(null);
        }

        /// <summary>
        /// Carries out one intent, and reports whether it did anything — an intent whose command
        /// cannot run is not "handled", so the key falls through untouched rather than vanishing.
        /// </summary>
        private bool Run(KeyboardEditorViewModel viewModel, EditorShortcut shortcut)
        {
            var direction = EditorShortcuts.ToDirection(shortcut);

            if (direction != NavigationDirection.None)
            {
                return BoardOwnsArrows() && TryMoveSelection(viewModel, direction);
            }

            var layerNumber = EditorShortcuts.ToLayerNumber(shortcut);

            if (layerNumber > 0)
            {
                return BoardOwnsLayerShortcuts() && TrySelectLayer(viewModel, layerNumber);
            }

            return shortcut switch
            {
                // ⌥↑↓ reorders the key inspector's selected macro step (mockup 2i). It is NOT gated
                // like the four plain arrows: the step list is deliberately not a
                // SelectingItemsControl (see MacroInspectorStepsViewModel), and nothing else in the
                // editor consumes Alt+arrow — so gating it on BoardOwnsArrows would only make it die
                // the moment the user clicked a tab or a layer segment. A focused text input still
                // wins, exactly as it does for ⌥n.
                EditorShortcut.MoveStepUp => BoardOwnsLayerShortcuts() && TryRun(viewModel.MoveMacroStepUpCommand),
                EditorShortcut.MoveStepDown => BoardOwnsLayerShortcuts() && TryRun(viewModel.MoveMacroStepDownCommand),
                EditorShortcut.FocusSearch => TryRun(viewModel.OpenSearchCommand),
                EditorShortcut.Save => TryRun(viewModel.SaveCommand),

                // Shell is null wherever the editor is hosted without one — every headless scene
                // that does not assign it, and the design-time preview.
                EditorShortcut.GoHome => TryRun(viewModel.Shell?.HomeCommand),
                _ => false
            };
        }

        /// <summary>
        /// Gate 3 for the <b>arrows</b>: whether the arrow reaching us is the board's to take. It
        /// is not, wherever the focused surface moves itself with arrows — a text input's caret, a
        /// <see cref="SelectingItemsControl"/>'s selection (the tab strip and the layer switcher
        /// are two of them, and stealing their arrows would break the segmented control outright),
        /// a <see cref="RangeBase"/>'s value.
        /// </summary>
        private bool BoardOwnsArrows()
        {
            return !IsFocusInside(typeof(TextBox), typeof(NumericUpDown), typeof(AutoCompleteBox), typeof(SelectingItemsControl), typeof(RangeBase));
        }

        /// <summary>
        /// Gate 3 for <b>⌥1</b>–<b>⌥5</b>, which is deliberately narrower than
        /// <see cref="BoardOwnsArrows"/>: no <see cref="SelectingItemsControl"/> or
        /// <see cref="RangeBase"/> consumes Alt+digit, so gating the layer jumps on them made the
        /// shortcuts die silently the moment the user clicked a layer segment or a tab with the
        /// mouse — the very controls the shortcut is a legend on. Only a focused text input still
        /// wins, because ⌥1 types <c>¡</c> on macOS.
        /// </summary>
        private bool BoardOwnsLayerShortcuts()
        {
            return !IsFocusInside(typeof(TextBox), typeof(NumericUpDown), typeof(AutoCompleteBox));
        }

        /// <summary>
        /// Whether the focused element is, or sits inside, one of <paramref name="surfaces"/>. The
        /// test is on the focused element's <b>ancestry</b> rather than on a list of control names,
        /// so a field nested in some future composite is covered without either gate learning about
        /// it; the walk stops at this view, because an ancestor above the editor is not what has
        /// focus in any meaningful sense.
        /// </summary>
        private bool IsFocusInside(params Type[] surfaces)
        {
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is not Visual focused)
            {
                return false;
            }

            foreach (var ancestor in focused.GetSelfAndVisualAncestors())
            {
                foreach (var surface in surfaces)
                {
                    if (surface.IsInstanceOfType(ancestor))
                    {
                        return true;
                    }
                }

                if (ReferenceEquals(ancestor, this))
                {
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves the selection one step across the physical board and puts the focus ring on the
        /// cap it landed on, so selection and focus coexist and stay distinguishable (mockup
        /// <c>2b</c>, "one ring, three surfaces").
        /// </summary>
        private bool TryMoveSelection(KeyboardEditorViewModel viewModel, NavigationDirection direction)
        {
            if (!viewModel.MoveSelection(direction))
            {
                return false;
            }

            if (viewModel.SelectedKey is { } key)
            {
                FocusCap(key.Index);
            }

            return true;
        }

        /// <summary>
        /// Focuses the landed cap on whichever board is on screen — the Layout/Macros picture and
        /// the Lighting tab's are two <see cref="KeyboardView"/>s over the same layer, and only one
        /// of them is ever visible.
        /// </summary>
        private void FocusCap(int keyIndex)
        {
            foreach (var picture in this.GetVisualDescendants().OfType<KeyboardView>())
            {
                if (picture.IsEffectivelyVisible && picture.TryFocusKey(keyIndex))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// ⌥n. A number past the open device's layer count is a no-op — the grammar is the same on
        /// a two-layer Freestyle Edge as on a five-layer Advantage 360 — and it stays unhandled, so
        /// nothing is swallowed on a board that has no such layer.
        /// </summary>
        private static bool TrySelectLayer(KeyboardEditorViewModel viewModel, int layerNumber)
        {
            if (layerNumber > viewModel.Layers.Count)
            {
                return false;
            }

            var layer = viewModel.Layers[layerNumber - 1];

            if (!viewModel.SelectLayerCommand.CanExecute(layer))
            {
                return false;
            }

            viewModel.SelectLayerCommand.Execute(layer);

            return true;
        }

        /// <summary>Runs a command if it can run, and says whether it did.</summary>
        private static bool TryRun(ICommand? command)
        {
            if (command is null || !command.CanExecute(null))
            {
                return false;
            }

            command.Execute(null);

            return true;
        }

        /// <summary>
        /// The layer switch is a <see cref="ListBox"/>, so choosing a layer is a selection rather
        /// than a click — but the editor's rules for switching (cancel the listening key, stop a
        /// recording, move the macro trigger) all live in
        /// <see cref="KeyboardEditorViewModel.SelectLayerCommand"/>, so that is still what runs.
        /// The command is idempotent, which is what makes it safe to fire on the selection the
        /// control makes for itself while the profile is loading.
        /// </summary>
        private void OnLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel
                || (sender as SelectingItemsControl)?.SelectedItem is not KeyboardLayerViewModel layer)
            {
                return;
            }

            viewModel.SelectLayerCommand.Execute(layer);
        }

        /// <inheritdoc cref="OnLayerSelectionChanged" />
        private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel || sender is not SelectingItemsControl strip)
            {
                return;
            }

            if (strip.SelectedItem is EditorTabViewModel tab)
            {
                viewModel.SelectTabCommand.Execute(tab);
            }

            // A tab with nothing behind it stays shut whichever way it is asked for, so the strip is
            // put back on the section that is actually open rather than left showing one that never
            // opened. Guarded by the comparison, which is what stops the assignment re-entering.
            if ((strip.SelectedItem as EditorTabViewModel)?.Tab != viewModel.SelectedTab)
            {
                strip.SelectedValue = viewModel.SelectedTab;
            }
        }

        /// <summary>
        /// The seam is being dragged. The splitter has already moved the column, so all that is left
        /// is to tell the model — which clamps it, applies the showing panel's floor and pushes the
        /// result back onto the column.
        /// </summary>
        private void OnRailSplitterDragged(object? sender, VectorEventArgs e)
        {
            _railColumn.Store();
        }

        /// <summary>
        /// The seam was let go. The same write as the drag's, because a drag that is a single flick
        /// can end without a delta ever being raised.
        /// </summary>
        private void OnRailSplitterReleased(object? sender, VectorEventArgs e)
        {
            _railColumn.Store();
        }
    }
}
