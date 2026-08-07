using Avalonia.Controls;
using Avalonia.Input;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Macros tab (mockup <c>1i</c>), resolved from <see cref="MacroLibraryViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the one thing
    /// a binding cannot do — <b>selecting a slot card by clicking anywhere on it</b>.
    /// <para>
    /// The card is a <see cref="Border"/> with a pointer handler rather than a <see cref="Button"/>
    /// for a reason this codebase has already paid for: it holds buttons of its own
    /// (<c>Make active</c>, <c>＋ Record a macro</c>), and a button nested in a button fires both
    /// commands on one click.
    /// </para>
    /// </summary>
    public partial class MacroLibraryView : UserControl
    {
        /// <summary>Creates the tab's view.</summary>
        public MacroLibraryView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Points the <c>this macro</c> meter at the card that was clicked. It is a reading and not
        /// an edit — nothing is written, and the card's own buttons handle their events before this
        /// one bubbles, so clicking <c>Make active</c> does not also count as a selection of some
        /// other card.
        /// </summary>
        private void OnSlotPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control card
                || card.DataContext is not MacroSlotViewModel slot
                || DataContext is not MacroLibraryViewModel library
                || !e.GetCurrentPoint(card).Properties.IsLeftButtonPressed)
            {
                return;
            }

            library.SelectSlotCommand.Execute(slot);
        }
    }
}
