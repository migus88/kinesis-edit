using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The device dashboard: a two-column grid of cards, or the troubleshoot empty state of
    /// specs/11-feature-dialogs.md §11.8 when nothing has ever been detected.
    /// <para>
    /// It also owns one behaviour rather than just markup — "a refresh that arrives while a card's
    /// own button is under the cursor or focused is deferred until focus leaves; the list never
    /// steals a click" (docs/design/mockups.md §2b). The <em>rule</em> lives in
    /// <see cref="DashboardViewModel.SuspendRefresh"/>; noticing that the pointer or the keyboard is
    /// inside the card grid is Avalonia's business and so belongs here, because view models carry no
    /// toolkit types (docs/app/app-shell.md, invariant 8).
    /// </para>
    /// </summary>
    public partial class DashboardView : UserControl
    {
        /// <summary>Creates the dashboard view.</summary>
        public DashboardView()
        {
            InitializeComponent();
        }

        /// <summary>Starts watching the card grid for the pointer and for keyboard focus.</summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            Cards.PropertyChanged += OnCardsPropertyChanged;

            UpdateRefreshDeferral();
        }

        /// <summary>
        /// Stops watching, and resumes refreshes. A dashboard that left the tree while the pointer
        /// was on a card would otherwise leave the list parked for good — nothing else ever calls
        /// <see cref="DashboardViewModel.ResumeRefresh"/>.
        /// </summary>
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            Cards.PropertyChanged -= OnCardsPropertyChanged;

            (DataContext as DashboardViewModel)?.ResumeRefresh();

            base.OnDetachedFromVisualTree(e);
        }

        /// <summary>
        /// Both flags are read off the grid rather than off an individual card: Avalonia raises
        /// <c>IsPointerOver</c> and <c>IsKeyboardFocusWithin</c> on every ancestor of the element
        /// that has the pointer or the focus, so the container answers "is the user inside any card"
        /// without the view having to subscribe to each one as it comes and goes.
        /// </summary>
        private void OnCardsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == InputElement.IsPointerOverProperty
                || e.Property == InputElement.IsKeyboardFocusWithinProperty)
            {
                UpdateRefreshDeferral();
            }
        }

        private void UpdateRefreshDeferral()
        {
            if (DataContext is not DashboardViewModel dashboard)
            {
                return;
            }

            if (Cards.IsPointerOver || Cards.IsKeyboardFocusWithin)
            {
                dashboard.SuspendRefresh();

                return;
            }

            dashboard.ResumeRefresh();
        }
    }
}
