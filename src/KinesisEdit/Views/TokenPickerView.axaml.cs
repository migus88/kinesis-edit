using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The token picker (mockups <c>1e</c>/<c>2a</c>), resolved from
    /// <see cref="TokenPickerViewModel"/> by <see cref="ViewLocator"/>. Everything it shows is bound;
    /// what is left here is the three things a view model cannot own — where the caret is, what a
    /// pointer gesture means, and how wide the control turned out to be.
    /// </summary>
    /// <remarks>
    /// <para><b>Focus is asked for, never taken.</b> The picker does <b>not</b> grab the caret when
    /// it appears, and that is deliberate: in the key inspector rail it appears whenever the user
    /// picks the Remap tab, and focus inside a <c>TextBox</c> suspends keystroke capture — a picker
    /// that stole the caret would quietly break "click a key, press a key", which is the fast path
    /// this panel exists to sit beside. It focuses only when somebody asks
    /// (<see cref="TokenPickerViewModel.RequestFocus"/>): ⌘F, and the macro-insertion modal, which
    /// asks in its own constructor. A request made before this view exists survives as
    /// <see cref="TokenPickerViewModel.IsFocusPending"/> and is answered on attach — because
    /// <see cref="ViewLocator"/> materialises the view a layout pass after the command that asked
    /// ran, so the caller has nothing to focus at the moment it runs. That is the same reason the
    /// Search Keys overlay this replaced focused on attach rather than in its constructor.</para>
    ///
    /// <para><b>The keyboard grammar of a list.</b> ↑/↓ walk the results and ↵ takes the highlighted
    /// row; all three are marked handled so the editor's own arrow grammar does not move the board
    /// selection out from under a user who is typing a query. The editor's tunnelled handler already
    /// yields arrows while focus sits in a text input, so the two agree rather than race.</para>
    ///
    /// <para><b>The chip row's width is measured, not assumed.</b> Mockup <c>2a</c> reduces the chips
    /// at a narrow width, which is a fact about the control's arranged size and therefore has to come
    /// from here; the view model decides <em>what</em> reducing means.</para>
    /// </remarks>
    public partial class TokenPickerView : UserControl
    {
        private readonly EventHandler _focusRequestedHandler;

        private TokenPickerViewModel? _picker;

        /// <summary>Creates the picker.</summary>
        public TokenPickerView()
        {
            InitializeComponent();

            _focusRequestedHandler = (_, _) => FocusQueryField();

            AddHandler(KeyDownEvent, OnPickerKeyDown);

            SizeChanged += (_, _) => PushWidth();
        }

        /// <inheritdoc />
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            Subscribe(DataContext as TokenPickerViewModel);

            PushWidth();

            if (_picker?.IsFocusPending == true)
            {
                FocusQueryField();
            }
        }

        /// <inheritdoc />
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            Subscribe(null);

            base.OnDetachedFromVisualTree(e);
        }

        /// <inheritdoc />
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            Subscribe(DataContext as TokenPickerViewModel);

            PushWidth();
        }

        private void Subscribe(TokenPickerViewModel? picker)
        {
            if (ReferenceEquals(picker, _picker))
            {
                return;
            }

            if (_picker is not null)
            {
                _picker.FocusRequested -= _focusRequestedHandler;
            }

            _picker = picker;

            if (_picker is not null)
            {
                _picker.FocusRequested += _focusRequestedHandler;
            }
        }

        private void PushWidth()
        {
            _picker?.SetAvailableWidth(Bounds.Width);
        }

        private void OnPickerKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not TokenPickerViewModel picker)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Enter:
                    picker.ChooseCommand.Execute(null);

                    break;

                case Key.Down:
                    picker.SelectNextCommand.Execute(null);

                    break;

                case Key.Up:
                    picker.SelectPreviousCommand.Execute(null);

                    break;

                default:
                    return;
            }

            e.Handled = true;
        }

        /// <summary>A single click highlights the row; it never assigns.</summary>
        private void OnRowTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: TokenPickerRowViewModel row }
                || DataContext is not TokenPickerViewModel picker)
            {
                return;
            }

            e.Handled = true;

            picker.SelectRowCommand.Execute(row);
        }

        /// <summary>
        /// §11.6's "double-clicking an item accepts immediately". The gesture stays here, so the
        /// view model never learns what a double click is.
        /// </summary>
        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: TokenPickerRowViewModel row }
                || DataContext is not TokenPickerViewModel picker)
            {
                return;
            }

            e.Handled = true;

            picker.ChooseCommand.Execute(row);
        }

        /// <summary>
        /// Puts the caret in the one text field this control declares. It is found by type rather
        /// than by name so nothing here depends on the markup's shape, and it is this view's own
        /// content, never somebody's template part. A window that has not laid out yet refuses
        /// focus, so the attempt is repeated on the first idle pass after it has.
        /// </summary>
        private void FocusQueryField()
        {
            if (TryFocusQueryField())
            {
                return;
            }

            Dispatcher.UIThread.Post(() => TryFocusQueryField(), DispatcherPriority.Loaded);
        }

        private bool TryFocusQueryField()
        {
            if (this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is not { } field
                || !field.Focus())
            {
                return false;
            }

            _picker?.ClearPendingFocus();

            return true;
        }
    }
}
