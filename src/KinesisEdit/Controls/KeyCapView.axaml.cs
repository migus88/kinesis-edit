using Avalonia;
using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// One key cap of the keyboard picture, over a <see cref="ViewModels.KeyboardKeyViewModel"/>.
    /// Everything it draws comes from that view model and the <c>keyCap</c> style classes; the two
    /// things it cannot read off its own data are <see cref="ShowsLedStrip"/> and
    /// <see cref="ShowsStateBadge"/>, which are facts about the <b>picture</b> rather than the key.
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

        /// <summary>
        /// Whether the cap draws the four state badges — the remap bar, the macro dot, the
        /// tap-and-hold corner and the advisory bar. Like <see cref="ShowsLedStrip"/> this is a
        /// fact about the <b>picture</b> and not about the key: both tabs render the very same
        /// <see cref="ViewModels.KeyboardKeyViewModel"/> instances, so no state on the
        /// <c>DataContext</c> could separate them, and the Lighting tab turns them off to stay
        /// about colour. Unlike the LED strip it defaults to <b>true</b> — a board says what its
        /// keys are unless it has a reason not to.
        /// <para>
        /// The cap still writes the four state classes either way; this only reaches the
        /// <c>stateBadges</c> class the <c>KeyCapButton</c> theme qualifies every badge with, so
        /// "what the key is" and "what this surface draws" stay separate answers.
        /// </para>
        /// </summary>
        public static readonly StyledProperty<bool> ShowsStateBadgeProperty =
            AvaloniaProperty.Register<KeyCapView, bool>(nameof(ShowsStateBadge), defaultValue: true);

        /// <inheritdoc cref="ShowsLedStripProperty" />
        public bool ShowsLedStrip
        {
            get => GetValue(ShowsLedStripProperty);
            set => SetValue(ShowsLedStripProperty, value);
        }

        /// <inheritdoc cref="ShowsStateBadgeProperty" />
        public bool ShowsStateBadge
        {
            get => GetValue(ShowsStateBadgeProperty);
            set => SetValue(ShowsStateBadgeProperty, value);
        }

        /// <summary>Creates the key cap.</summary>
        public KeyCapView()
        {
            InitializeComponent();
        }
    }
}
