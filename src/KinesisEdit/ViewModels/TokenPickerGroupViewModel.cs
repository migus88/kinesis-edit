using System.Globalization;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One counted block of the token picker's results — mockup <c>1e</c>'s <c>Navigation · 3</c>
    /// header and the rows beneath it.
    ///
    /// <para><b>The grouping is Core's; the wording is this class's.</b>
    /// <see cref="KeySearchCatalog.Group"/> keys groups by the specs/05-key-model.md §3 sub-table
    /// each row comes from and counts them, which is the datum. What a §3 table is <em>called</em> in
    /// front of a user is presentation — "Letters and digits", not <c>LettersAndDigits</c> — so it is
    /// spelled here, once, where a test can read it.</para>
    ///
    /// <para><b>The header is sans, including the count.</b> A group name is the app talking, and the
    /// count is the app counting; neither is a value any config file contains, so the mono law
    /// (docs/design/README.md) keeps them out of Plex Mono however tempting a right-aligned number
    /// looks.</para>
    ///
    /// <para><b>Groups are finer than chips.</b> <see cref="KeySearchCategory"/> folds thirteen
    /// tables onto six chips plus <c>Other</c>; the headers keep all thirteen, because a result list
    /// that merged punctuation, modifiers and layer keys under one "Other" heading would tell the
    /// user less than the rows already tell them.</para>
    ///
    /// <para><b>It is drawn as a line of the flat result list, not as a container round its rows</b>
    /// (issue #133). It still <em>holds</em> <see cref="Rows"/> — that is what the count is taken
    /// over — but <see cref="TokenPickerViewModel.Items"/> interleaves the header and those rows
    /// into one sequence so the view can virtualize it. The header stays outside the row container
    /// either way, which is the point: one drawn inside a row would take the row's selection fill
    /// along with it.</para>
    /// </summary>
    public sealed class TokenPickerGroupViewModel : ViewModelBase, ITokenPickerItem
    {
        /// <summary>The middle dot between a group's name and its count, as the mockups draw it.</summary>
        public const string HeaderSeparator = " · ";

        /// <summary>What the picker calls a §3 sub-table. The whole mapping, in one place.</summary>
        public static string TitleFor(KeyTable table)
        {
            return table switch
            {
                KeyTable.LettersAndDigits => "Letters and digits",
                KeyTable.Punctuation => "Punctuation",
                KeyTable.Navigation => "Navigation",
                KeyTable.FunctionKeys => "Function keys",
                KeyTable.Modifiers => "Modifiers",
                KeyTable.KeypadKeys => "Keypad keys",
                KeyTable.MediaKeys => "Media keys",
                KeyTable.MouseActions => "Mouse actions",
                KeyTable.SpecialActions => "Special actions",
                KeyTable.LayerKeys => "Layer keys",
                KeyTable.ProfilesAndHotkeys => "Profiles and hotkeys",
                KeyTable.MacroTiming => "Macro timing",
                KeyTable.EdgeZones => "Edge zones",
                _ => "Other"
            };
        }

        /// <inheritdoc />
        public bool IsHeader => true;

        /// <summary>The §3 sub-table every row here belongs to.</summary>
        public KeyTable Table { get; }

        /// <summary>Its name, as the header reads it.</summary>
        public string Title { get; }

        /// <summary>How many rows are beneath the header — exactly the rows, never a catalog total.</summary>
        public int Count => Rows.Count;

        /// <summary>The whole header: <c>Navigation · 3</c>.</summary>
        public string Header { get; }

        /// <summary>The group's rows, in catalog order.</summary>
        public IReadOnlyList<TokenPickerRowViewModel> Rows { get; }

        /// <summary>Builds the block over one of <see cref="KeySearchCatalog.Group"/>'s groups.</summary>
        public TokenPickerGroupViewModel(KeySearchGroup group, IReadOnlyList<TokenPickerRowViewModel> rows)
        {
            ArgumentNullException.ThrowIfNull(group);

            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Table = group.Table;
            Title = TitleFor(group.Table);
            Header = Title + HeaderSeparator + Rows.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
