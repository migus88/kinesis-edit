using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

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

        /// <summary>What a click on a key cap runs; the clicked key is the command parameter.</summary>
        public ICommand? KeySelectedCommand
        {
            get => GetValue(KeySelectedCommandProperty);
            set => SetValue(KeySelectedCommandProperty, value);
        }

        /// <summary>Creates the keyboard picture.</summary>
        public KeyboardView()
        {
            InitializeComponent();
        }
    }
}
