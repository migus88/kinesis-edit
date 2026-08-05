using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The modal "Check for Updates" window of specs/09-firmware.md §3. The check is kicked off
    /// when the window is shown — the legacy form used a timer for the same reason: the rows must
    /// be visible on "Checking for update..." before the request starts — and cancelled when it
    /// closes, so a slow endpoint cannot outlive the dialog.
    /// </summary>
    public partial class FirmwareUpdateWindow : Window
    {
        private readonly FirmwareUpdateViewModel? _viewModel;

        /// <summary>Parameterless constructor for the XAML designer.</summary>
        public FirmwareUpdateWindow()
        {
            InitializeComponent();
        }

        /// <summary>Creates the dialog over <paramref name="viewModel"/>.</summary>
        public FirmwareUpdateWindow(FirmwareUpdateViewModel viewModel) : this()
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            DataContext = viewModel;

            // Tunneling, exactly as in MessageBoxWindow: Escape must close the dialog whatever has
            // focus, instead of being swallowed by the focused row or Close button.
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        /// <summary>Starts the check once the window is on screen.</summary>
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            _viewModel?.CheckCommand.Execute(null);
        }

        /// <summary>Cancels an in-flight request when the dialog goes away.</summary>
        protected override void OnClosed(EventArgs e)
        {
            _viewModel?.Dispose();

            base.OnClosed(e);
        }

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;

            Close();
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
