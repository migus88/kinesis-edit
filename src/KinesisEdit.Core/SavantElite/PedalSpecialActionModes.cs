namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// The editor modes a <see cref="PedalSpecialAction"/> may be used in
    /// (specs/12-savant-elite.md §6: "each menu item is restricted to single mode, macro mode, or
    /// both; selecting an item automatically switches the editor to the required mode"). An entry
    /// valid in both sets both bits; every entry sets at least one, so there is no <c>None</c>.
    /// </summary>
    [Flags]
    public enum PedalSpecialActionModes
    {
        /// <summary>Offered while the editor is in <see cref="PedalInputMode.Single"/>.</summary>
        Single = 1 << 0,

        /// <summary>Offered while the editor is in <see cref="PedalInputMode.Macro"/>.</summary>
        Macro = 1 << 1
    }
}
