using System.ComponentModel;
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
    /// replaced used to run, and the one thing a binding cannot do: the key inspector rail's
    /// column width (see <see cref="SyncRailColumn"/>).
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
        /// The view model the rail column is currently following, so its notification can be dropped
        /// again when the editor is replaced.
        /// </summary>
        private KeyboardEditorViewModel? _railWidthSource;

        /// <summary>Creates the editor view.</summary>
        public KeyboardEditorView()
        {
            InitializeComponent();

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
        /// that editor stores. The subscription is dropped from the outgoing one first: the shell
        /// reuses views, and an editor left subscribed would go on moving a column it no longer
        /// owns.
        /// </summary>
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_railWidthSource is not null)
            {
                _railWidthSource.PropertyChanged -= OnEditorPropertyChanged;
                _railWidthSource = null;
            }

            if (DataContext is not KeyboardEditorViewModel viewModel)
            {
                return;
            }

            _railWidthSource = viewModel;

            viewModel.PropertyChanged += OnEditorPropertyChanged;

            SyncRailColumn(viewModel);
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

            if (DataContext is KeyboardEditorViewModel editor)
            {
                // The column definitions do not exist until the XAML has been loaded and this view
                // has been attached to something, so a DataContext that arrived before either is
                // applied here instead.
                SyncRailColumn(editor);
            }

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
        /// narrower. The latch covers it exactly as it covers a panel: a rail record button disarms
        /// as it takes the Escape it was recording, and without the latch that one keypress would
        /// both fill the field and drop the selection.
        /// </para>
        /// <para>
        /// Nothing is handled when there is no selection, so Escape falls through untouched on an
        /// editor the user has not clicked into — an accelerator that swallows a key it does nothing
        /// with is worse than one that does not fire.
        /// </para>
        /// </summary>
        private static void HandleEscape(KeyboardEditorViewModel viewModel, KeyEventArgs e, bool takenByOverlay)
        {
            if (!takenByOverlay
                && !viewModel.IsOverlayAwaitingKeystroke
                && viewModel.CloseOverlayCommand.CanExecute(null))
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
        /// The rail's width moved: either the user dragged the seam and the store clamped what they
        /// asked for, or the showing panel raised the macro floor.
        /// </summary>
        private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not KeyboardEditorViewModel viewModel)
            {
                return;
            }

            // An empty name is "everything changed", which a view model is entitled to raise.
            if (e.PropertyName is not (null
                or ""
                or nameof(KeyboardEditorViewModel.InspectorRailWidth)
                or nameof(KeyboardEditorViewModel.EffectiveInspectorRailWidth)))
            {
                return;
            }

            SyncRailColumn(viewModel);
        }

        /// <summary>
        /// Puts the editor's width onto the rail's column.
        /// <para>
        /// <b>Why this is not a binding.</b> A <see cref="ColumnDefinition"/> is a bare
        /// <c>AvaloniaObject</c>: it is in neither the logical nor the visual tree, so it inherits no
        /// <c>DataContext</c> and a <c>{Binding}</c> written on it in XAML has nothing to resolve a
        /// path against. The alternatives are worse — a local <c>Width</c> on the rail control would
        /// be read by the layout pass but not by the <see cref="GridSplitter"/>, which resizes
        /// columns, so the two would disagree the moment the seam was dragged.
        /// </para>
        /// <para>
        /// The column's own minimum and maximum are the geometry tokens, which is what bounds the
        /// drag itself. The Macro panel's 300px floor is <em>not</em> spelled here: it is folded into
        /// <c>EffectiveInspectorRailWidth</c> by the editor, so a drag that ends below it is pushed
        /// back up by the very next notification — one owner for the floor, not two.
        /// </para>
        /// </summary>
        private void SyncRailColumn(KeyboardEditorViewModel viewModel)
        {
            if (LayoutColumns.ColumnDefinitions.Count <= RailColumnIndex)
            {
                return;
            }

            var column = LayoutColumns.ColumnDefinitions[RailColumnIndex];

            column.MinWidth = Measure("WidthInspectorRailMin", column.MinWidth);
            column.MaxWidth = Measure("WidthInspectorRailMax", column.MaxWidth);
            column.Width = new GridLength(viewModel.EffectiveInspectorRailWidth, GridUnitType.Pixel);
        }

        /// <summary>
        /// The seam is being dragged. The splitter has already moved the column, so all that is left
        /// is to tell the editor — which clamps it, applies the showing panel's floor and pushes the
        /// result back through <see cref="SyncRailColumn"/>.
        /// </summary>
        private void OnRailSplitterDragged(object? sender, VectorEventArgs e)
        {
            StoreRailWidth();
        }

        /// <summary>
        /// The seam was let go. The same write as the drag's, because a drag that is a single flick
        /// can end without a delta ever being raised.
        /// </summary>
        private void OnRailSplitterReleased(object? sender, VectorEventArgs e)
        {
            StoreRailWidth();
        }

        /// <summary>Reads the rail column back off the grid and stores it as the user's width.</summary>
        private void StoreRailWidth()
        {
            if (DataContext is not KeyboardEditorViewModel viewModel
                || LayoutColumns.ColumnDefinitions.Count <= RailColumnIndex)
            {
                return;
            }

            var column = LayoutColumns.ColumnDefinitions[RailColumnIndex];

            // The splitter always writes an absolute length; ActualWidth is the fallback for the one
            // frame before the grid has re-measured.
            viewModel.InspectorRailWidth = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
        }

        /// <summary>
        /// Resolves a geometry token, or keeps <paramref name="fallback"/> where the view is hosted
        /// without the app's resources — which is every design-time preview.
        /// </summary>
        private double Measure(string key, double fallback)
        {
            return this.TryFindResource(key, out var value) && value is double measure ? measure : fallback;
        }
    }
}
