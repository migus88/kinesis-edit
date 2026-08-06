using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// One dashboard device card (docs/design/mockups.md §1b, §2e): the device art, the name, the
    /// meta line, the status line and the Configure / secondary / Eject actions, all at one fixed
    /// height so a refresh cannot move a button under the pointer.
    /// </summary>
    public partial class DeviceCardView : UserControl
    {
        /// <summary>
        /// The class the card is born wearing, which pins its height to zero. Dropped once the card
        /// is attached, which is what makes the 160 ms insert actually run.
        /// </summary>
        private const string CollapsedClass = "collapsed";

        /// <summary>Creates the card view.</summary>
        public DeviceCardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Releases the card to its full height, one dispatcher turn after it joins the tree.
        /// <para>
        /// A transition never animates an <em>initial</em> value: a card that is simply born at
        /// <c>HeightDeviceCard</c> appears rather than animating in, and the design asks for "a
        /// newly detected device animates in at the end of the list, height-only, 160 ms ease-out"
        /// (docs/design/mockups.md §2b). So the card arrives collapsed and the height is changed
        /// afterwards, which is the change the transition has something to run on.
        /// </para>
        /// <para>
        /// Through a class rather than <c>IsVisible</c>, and posted rather than run inline: setting
        /// the height in the same layout pass that created the control is still the initial value
        /// as far as the transition is concerned.
        /// </para>
        /// <para>
        /// <b>Only the first time this card is shown.</b> The insert animation belongs to a newly
        /// detected device, and a card view is rebuilt from scratch whenever the shell swaps the
        /// dashboard back in — <c>ViewLocator</c> constructs a fresh view per
        /// <c>CurrentView</c> change — so returning Home would otherwise grow the entire grid in
        /// from zero. A card that has been presented before is released <em>synchronously</em>,
        /// which is before this view's own children are attached and therefore before their
        /// transitions are live: the height is an initial value again and nothing animates.
        /// </para>
        /// </summary>
        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (DataContext is DashboardCardViewModel card)
            {
                var hasBeenPresented = card.HasBeenPresented;

                card.MarkPresented();

                if (hasBeenPresented)
                {
                    Release();

                    return;
                }
            }

            Dispatcher.UIThread.Post(Release, DispatcherPriority.Loaded);
        }

        private void Release()
        {
            Card.Classes.Remove(CollapsedClass);
        }
    }
}
