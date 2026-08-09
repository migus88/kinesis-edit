using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One result row of the token picker (mockups <c>1e</c>/<c>2a</c>): the file token in mono, the
    /// action's friendly name in sans, whatever scope or duplication the row carries, and the
    /// <c>↵ assign</c> hint the selected row shows.
    ///
    /// <para><b>The token is mono and the rest is not.</b> <c>[esc]</c> is literally what
    /// <c>layoutN.txt</c> contains (docs/design/README.md's mono law); "Escape", "Keypad layer" and
    /// every other word here is the app talking about it.</para>
    ///
    /// <para><b>Aliases are labelled by their canonical, never by their token.</b> Mockup <c>1e</c>
    /// draws the label as <c>alias of [esc]</c>, which cannot be rendered: a real alias is a
    /// duplicate <em>registration</em> that differs from its canonical only in numeric code (05 §7),
    /// so both rows spell the same token and the label would read "alias of [numlk]" on a row that
    /// is itself <c>[numlk]</c>. The canonical's <see cref="KeySearchEntry.SearchName"/> and this
    /// row's <see cref="KeySearchEntry.Scope"/> say the useful thing instead — "Num Lock — keypad
    /// layer duplicate". The mock's other example, <c>[escape]</c> as an alias of <c>[esc]</c>, is
    /// not an alias at all: those are the Legacy and Gen1 tokens of one key-table entry, and no
    /// catalog ever lists both.</para>
    ///
    /// <para><b>It holds no command.</b> Selecting and assigning are the picker's
    /// (<see cref="TokenPickerViewModel"/>); a row that could assign itself would be a second write
    /// path into the model.</para>
    ///
    /// <para><b>It is one line of a flat, virtualized list</b> (issue #133) — see
    /// <see cref="ITokenPickerItem"/>. Only the rows the viewport can show are realized, so a row
    /// view model is now cheaper than the container that draws it, and both are built for ten rows
    /// rather than for two hundred.</para>
    /// </summary>
    public sealed class TokenPickerRowViewModel : ViewModelBase, ITokenPickerItem
    {
        /// <summary>What the selected row offers, beside the <c>↵</c> mark (mockup <c>1e</c>).</summary>
        public const string AssignHint = "assign";

        /// <summary>Separates the canonical's name from what kind of duplicate this row is.</summary>
        public const string LabelSeparator = " — ";

        /// <summary>What a duplicate of a §3.6 keypad row is called.</summary>
        public const string KeypadDuplicateLabel = "keypad layer duplicate";

        /// <summary>What a duplicate of one of the board's own hotkeys is called (§3.11).</summary>
        public const string HotkeyDuplicateLabel = "device hotkey duplicate";

        /// <summary>What a duplicate of a profile selector is called (§3.11).</summary>
        public const string ProfileDuplicateLabel = "profile selector duplicate";

        /// <summary>What every other duplicate is called — the row exists twice and says so.</summary>
        public const string PlainDuplicateLabel = "duplicate";

        /// <summary>How the picker names a §3.6 keypad row (mockup <c>1e</c>: "Keypad layer Esc").</summary>
        public const string KeypadScopeLabel = "Keypad layer";

        /// <summary>How it names one of the board's own hotkeys (mockup <c>1e</c>: "Device hotkey").</summary>
        public const string HotkeyScopeLabel = "Device hotkey";

        /// <summary>How it names a profile selector.</summary>
        public const string ProfileScopeLabel = "Profile selector";

        /// <summary>
        /// How the picker spells a token — bracketed, as the mockups and the layout file both do.
        /// Shared with <see cref="KeyboardKeyViewModel.FormatToken"/>'s spelling deliberately: the
        /// inspector's assignment line and the row the user picks from must read as the same thing.
        /// </summary>
        public static string FormatToken(string fileToken)
        {
            ArgumentNullException.ThrowIfNull(fileToken);

            return "[" + fileToken + "]";
        }

        /// <summary>What the picker calls a row that lives somewhere in particular on the board.</summary>
        public static string ScopeLabelFor(KeySearchScope scope)
        {
            return scope switch
            {
                KeySearchScope.Keypad => KeypadScopeLabel,
                KeySearchScope.DeviceHotkey => HotkeyScopeLabel,
                KeySearchScope.Profile => ProfileScopeLabel,
                _ => string.Empty
            };
        }

        /// <inheritdoc />
        public bool IsHeader => false;

        /// <summary>The catalog row this one draws.</summary>
        public KeySearchEntry Entry { get; }

        /// <summary>The action a pick assigns.</summary>
        public KeyDefinition Definition => Entry.Definition;

        /// <summary>The file token, bracketed: <c>[esc]</c>. Mono, because the file contains it.</summary>
        public string Token { get; }

        /// <summary>The action's single-line name: <c>Escape</c>.</summary>
        public string Name { get; }

        /// <summary>
        /// The row's second line: where it lives on the board, or — for a duplicate registration —
        /// which row it duplicates and in what sense. Empty for an ordinary, unscoped, canonical
        /// action, which is most of the catalog.
        /// </summary>
        public string Detail { get; }

        /// <summary>Whether there is a second line at all.</summary>
        public bool HasDetail => Detail.Length > 0;

        /// <summary>Whether an earlier row already offers this action (05 §7).</summary>
        public bool IsAlias => Entry.IsAlias;

        /// <summary>Whether this is the row <c>↵</c> would assign.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Builds the row over one catalog entry.</summary>
        public TokenPickerRowViewModel(KeySearchEntry entry)
        {
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));

            Token = FormatToken(entry.FileToken);
            Name = entry.SearchName;
            Detail = BuildDetail(entry);
        }

        /// <summary>
        /// The second line. A duplicate says what it duplicates; anything else says only where it
        /// sits, and an ordinary key says nothing rather than repeating its own name.
        /// </summary>
        private static string BuildDetail(KeySearchEntry entry)
        {
            if (entry.AliasOf is { } canonical)
            {
                return canonical.SearchName + LabelSeparator + DuplicateLabelFor(entry.Scope);
            }

            return ScopeLabelFor(entry.Scope);
        }

        private static string DuplicateLabelFor(KeySearchScope scope)
        {
            return scope switch
            {
                KeySearchScope.Keypad => KeypadDuplicateLabel,
                KeySearchScope.DeviceHotkey => HotkeyDuplicateLabel,
                KeySearchScope.Profile => ProfileDuplicateLabel,
                _ => PlainDuplicateLabel
            };
        }
    }
}
