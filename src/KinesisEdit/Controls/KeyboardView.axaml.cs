using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// The keyboard picture of one <see cref="ViewModels.KeyboardLayerViewModel"/>. It is the
    /// generic component of the editor: it draws whatever layer it is given and reports clicks
    /// through <see cref="KeySelectedCommand"/>, which is how a cap reaches the editor's
    /// <c>SelectKeyCommand</c> without either the picture or the cap knowing the editor exists.
    /// </summary>
    public partial class KeyboardView : UserControl
    {
        /// <summary>
        /// The command a key cap runs when it is clicked, with the clicked
        /// <see cref="ViewModels.KeyboardKeyViewModel"/> as its parameter.
        /// </summary>
        public static readonly StyledProperty<ICommand?> KeySelectedCommandProperty =
            AvaloniaProperty.Register<KeyboardView, ICommand?>(nameof(KeySelectedCommand));

        /// <summary>
        /// Whether this picture is a <b>lighting</b> surface, and its caps should therefore draw
        /// their LED strip. Off by default, so a board asks for the row rather than opting out of
        /// it: the Keys tab edits assignments and shows no LED row at all.
        /// <para>
        /// It has to be asked of the picture rather than of the layer view model, because the two
        /// tabs render the <b>same</b> <see cref="ViewModels.KeyboardLayerViewModel"/> — see the
        /// note on <see cref="ViewModels.KeyboardLayerViewModel.ApplyColorOverlays"/>. The
        /// <see cref="KeyCapView"/> instances are per picture, which makes this the one place the
        /// two surfaces can be told apart.
        /// </para>
        /// </summary>
        public static readonly StyledProperty<bool> ShowsLedStripsProperty =
            AvaloniaProperty.Register<KeyboardView, bool>(nameof(ShowsLedStrips));

        /// <summary>What a click on a key cap runs; the clicked key is the command parameter.</summary>
        public ICommand? KeySelectedCommand
        {
            get => GetValue(KeySelectedCommandProperty);
            set => SetValue(KeySelectedCommandProperty, value);
        }

        /// <inheritdoc cref="ShowsLedStripsProperty" />
        public bool ShowsLedStrips
        {
            get => GetValue(ShowsLedStripsProperty);
            set => SetValue(ShowsLedStripsProperty, value);
        }

        /// <summary>Creates the keyboard picture.</summary>
        public KeyboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Puts the keyboard focus on the cap of the position with ordinal
        /// <paramref name="keyIndex"/>, as an arrow key reaching it would — the
        /// <see cref="NavigationMethod.Directional"/> hint is what raises
        /// <c>:focus-visible</c>, so the ring appears (mockup <c>2b</c>: "mouse clicks suppress it,
        /// arrow/Tab summon it"), and selection and focus then coexist on one cap as the design
        /// requires. Returns false when the picture carries no such cap, or has not been laid out
        /// yet.
        /// <para>
        /// It is the narrowest thing the editor could be given: the picture already owns the caps
        /// it builds, so nothing outside has to know how a cap is put together. It reaches a cap's
        /// own <see cref="ContentControl.Content"/> — the <c>Button</c> the cap view declares — and
        /// never a template part, which stays the exclusive business of
        /// <c>Themes/ControlThemes/</c>.
        /// </para>
        /// </summary>
        public bool TryFocusKey(int keyIndex)
        {
            foreach (var cap in this.GetVisualDescendants().OfType<KeyCapView>())
            {
                if (cap.DataContext is not KeyboardKeyViewModel key || key.Index != keyIndex)
                {
                    continue;
                }

                return cap.Content is Control target && target.Focus(NavigationMethod.Directional);
            }

            return false;
        }
    }
}
