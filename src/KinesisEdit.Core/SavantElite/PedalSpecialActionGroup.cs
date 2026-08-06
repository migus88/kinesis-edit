namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// One section of the Special Actions menu (specs/12-savant-elite.md §6 "Section" column):
    /// MOUSE ACTIONS, EDITING TOOLS, MEDIA CONTROLS, COMMONLY USED SHORTCUTS, PEDAL RESPONSE. The
    /// group order and the item order inside it are the menu order — §6 says "all items are always
    /// shown", so nothing is filtered here.
    /// </summary>
    public sealed class PedalSpecialActionGroup
    {
        /// <summary>The §6 section heading, in its original upper case.</summary>
        public string Name { get; }

        /// <summary>The section's items in menu order.</summary>
        public IReadOnlyList<PedalSpecialAction> Actions { get; }

        internal PedalSpecialActionGroup(string name, IReadOnlyList<PedalSpecialAction> actions)
        {
            Name = name;
            Actions = actions;
        }
    }
}
