using Avalonia;
using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// One key cap of the keyboard picture, over a <see cref="ViewModels.KeyboardKeyViewModel"/>.
    /// Everything it draws comes from that view model and the <c>keyCap</c> style classes; the one
    /// thing it cannot read off its own data is <see cref="ShowsLedStrip"/>.
    /// </summary>
    public partial class KeyCapView : UserControl
    {
        /// <summary>
        /// Whether the cap draws its LED strip at all. This answers "is lighting on screen?", which
        /// is a fact about the <b>picture</b> rather than about the key: the Keys tab and the
        /// Lighting tab render the very same <see cref="ViewModels.KeyboardKeyViewModel"/>
        /// instances, so the cap cannot read it off its <c>DataContext</c>. The containing
        /// <see cref="KeyboardView"/> pushes its own <see cref="KeyboardView.ShowsLedStrips"/> down
        /// here; a cap hosted on its own — a test, a design-time scene — draws no strip.
        /// </summary>
        public static readonly StyledProperty<bool> ShowsLedStripProperty =
            AvaloniaProperty.Register<KeyCapView, bool>(nameof(ShowsLedStrip));

        /// <inheritdoc cref="ShowsLedStripProperty" />
        public bool ShowsLedStrip
        {
            get => GetValue(ShowsLedStripProperty);
            set => SetValue(ShowsLedStripProperty, value);
        }

        /// <summary>Creates the key cap.</summary>
        public KeyCapView()
        {
            InitializeComponent();
        }
    }
}
