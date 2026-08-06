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
    /// keyboard grammar — the Escape route out of an open feature panel and out of the remap's
    /// listening state, the arrow/⌥n/⌘F/⌘S/⌘W table of docs/design/mockups.md <c>2b</c> — plus the
    /// two selection handlers that turn a segment or a tab being chosen back into the command the
    /// buttons they replaced used to run.
    /// </summary>
    public partial class KeyboardEditorView : UserControl
    {
        /// <summary>Creates the editor view.</summary>
        public KeyboardEditorView()
        {
            InitializeComponent();

            // Tunneling, as in MessageBoxWindow: Escape must leave the listening state whatever
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
        /// Takes focus unless something inside the editor already has it, so the grammar works from
        /// the moment the editor opens rather than only after the first click. Anything the user
        /// then focuses — a cap, a tab, a field — is a descendant of this view, so the tunnel route
        /// still passes through <see cref="OnPreviewKeyDown"/>.
        /// </summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

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

            if (!viewModel.CancelRemapCommand.CanExecute(null))
            {
                return;
            }

            e.Handled = true;

            viewModel.CancelRemapCommand.Execute(null);
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
    }
}
