using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the Special Actions menu (specs/12-savant-elite.md §6) as the view sees it: the
    /// caption, whether it is offered in the mode the entry box is in, whether applying it would be
    /// refused right now, and — for "Windows Combination (Win + …)" — whether the sticky modifier
    /// is currently latched.
    /// <para>
    /// It carries the editor's <see cref="ApplyCommand"/> so a menu item needs no ancestor binding:
    /// the menu lives in a flyout, whose popup is not part of the view's own tree.
    /// </para>
    /// </summary>
    public sealed class PedalSpecialActionViewModel : ViewModelBase
    {
        /// <summary>The catalog entry behind the row.</summary>
        public PedalSpecialAction Action { get; }

        /// <summary>The §6 menu caption.</summary>
        public string Caption => Action.Caption;

        /// <summary>
        /// Whether the item is a latching one — only the Win-modifier toggle is (§6). The view binds
        /// the menu item's <c>ToggleType</c> from it, because a check-box menu item marks itself
        /// checked the moment it is clicked, whatever <see cref="IsChecked"/> says.
        /// </summary>
        public bool IsCheckable => Action.Kind == PedalSpecialActionKind.ToggleWinModifier;

        /// <summary>
        /// Whether the item belongs to the mode the entry is in. A menu item outside it still
        /// applies (§6 switches the mode for the user), but that clears the entry — so the view
        /// mutes these rather than hiding them.
        /// </summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            private set => SetProperty(ref _isAvailable, value);
        }

        /// <summary>Whether applying the item right now would do something (<see cref="PedalEditSession.CanApply"/>).</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            private set => SetProperty(ref _isEnabled, value);
        }

        /// <summary>Whether a latching item is on; always false for every other item.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            private set => SetProperty(ref _isChecked, value);
        }

        /// <summary>The editor's apply command; the menu passes this row as its parameter.</summary>
        public IRelayCommand<PedalSpecialActionViewModel> ApplyCommand { get; }

        private bool _isAvailable;
        private bool _isEnabled;
        private bool _isChecked;

        /// <summary>Creates the row for <paramref name="action"/>.</summary>
        public PedalSpecialActionViewModel(PedalSpecialAction action, IRelayCommand<PedalSpecialActionViewModel> applyCommand)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(applyCommand);

            Action = action;
            ApplyCommand = applyCommand;
        }

        /// <summary>
        /// Re-reads the item's state from <paramref name="session"/>; a null session is "no edit in
        /// progress", where nothing in the menu is offered.
        /// </summary>
        public void Refresh(PedalEditSession? session)
        {
            IsAvailable = session is not null && Action.SupportsMode(session.Mode);
            IsEnabled = session is not null && session.CanApply(Action);
            IsChecked = session is not null && IsCheckable && session.HoldWinModifier;
        }
    }
}
