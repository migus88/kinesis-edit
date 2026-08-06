using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One section of the Special Actions menu (specs/12-savant-elite.md §6) — MOUSE ACTIONS,
    /// EDITING TOOLS, MEDIA CONTROLS, COMMONLY USED SHORTCUTS, PEDAL RESPONSE — in catalog order.
    /// Nothing here filters the catalog: §6's OS gating is deliberately not reproduced
    /// (docs/app/savant-elite.md), so every group shows every item it has.
    /// </summary>
    public sealed class PedalSpecialActionGroupViewModel : ViewModelBase
    {
        /// <summary>The §6 section heading.</summary>
        public string Name { get; }

        /// <summary>The section's items, in menu order.</summary>
        public IReadOnlyList<PedalSpecialActionViewModel> Actions { get; }

        /// <summary>Creates the section for <paramref name="group"/>.</summary>
        public PedalSpecialActionGroupViewModel(
            PedalSpecialActionGroup group,
            IRelayCommand<PedalSpecialActionViewModel> applyCommand)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(applyCommand);

            var actions = new PedalSpecialActionViewModel[group.Actions.Count];

            for (var index = 0; index < actions.Length; index++)
            {
                actions[index] = new PedalSpecialActionViewModel(group.Actions[index], applyCommand);
            }

            Name = group.Name;
            Actions = new ReadOnlyCollection<PedalSpecialActionViewModel>(actions);
        }

        /// <summary>Builds every section of <see cref="PedalSpecialActions.Groups"/>.</summary>
        public static IReadOnlyList<PedalSpecialActionGroupViewModel> BuildAll(
            IRelayCommand<PedalSpecialActionViewModel> applyCommand)
        {
            var groups = new PedalSpecialActionGroupViewModel[PedalSpecialActions.Groups.Count];

            for (var index = 0; index < groups.Length; index++)
            {
                groups[index] = new PedalSpecialActionGroupViewModel(PedalSpecialActions.Groups[index], applyCommand);
            }

            return new ReadOnlyCollection<PedalSpecialActionGroupViewModel>(groups);
        }

        /// <summary>Re-reads every item's state from <paramref name="session"/>.</summary>
        public void Refresh(PedalEditSession? session)
        {
            foreach (var action in Actions)
            {
                action.Refresh(session);
            }
        }
    }
}
