using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The token picker as a modal — specs/11-feature-dialogs.md §11.6's <c>Search Keys (Macro)</c>,
    /// the one call site that still wants a panel rather than the rail: a keystroke being
    /// <em>inserted into a macro</em> is not an assignment to the selected position, so it cannot be
    /// the inspector's Remap panel.
    ///
    /// <para><b>It wraps the picker; it does not re-implement it.</b> Everything the user sees —
    /// the catalog, the query, the chips, the counted groups, <c>Recent</c> — is
    /// <see cref="TokenPickerViewModel"/>, exactly the instance shape the rail hosts. This class adds
    /// only what a modal needs: a title, §11.6's validation, and the accept/cancel pair
    /// <see cref="EditorOverlayViewModel"/> defines.</para>
    ///
    /// <para><b>The strings are §11.6's, verbatim.</b> They are pinned by their own spec tests and
    /// travel with the behaviour rather than with the class that used to carry them.</para>
    ///
    /// <para><b>This one <em>is</em> modal, and that is not a contradiction.</b> The rail is
    /// deliberately not — the board stays clickable while it is open — but a macro insertion is a
    /// question with one answer that has to come back to the caller, which is what an overlay is
    /// for.</para>
    /// </summary>
    public sealed class TokenPickerOverlayViewModel : EditorOverlayViewModel
    {
        /// <summary>Title of the plain picker (§11.6).</summary>
        public const string DefaultTitle = "Search Keys";

        /// <summary>Title when picking a keystroke for a macro (§11.6) — this panel's one call site.</summary>
        public const string MacroTitle = "Search Keys (Macro)";

        /// <summary>Label over the filter box (§11.6).</summary>
        public const string SearchLabel = TokenPickerViewModel.SearchLabel;

        /// <summary>Validation shown when Ok is pressed with nothing selected (§11.6).</summary>
        public const string NoSelectionMessage = "You must select a key";

        /// <summary>The picker itself — the same one the key inspector's Remap panel hosts.</summary>
        public TokenPickerViewModel Picker { get; }

        /// <summary>The picked action once the overlay was accepted; null until then.</summary>
        public KeyDefinition? SelectedKey
        {
            get => _selectedKey;
            private set => SetProperty(ref _selectedKey, value);
        }

        /// <summary>Raised once with the picked action when the overlay is accepted.</summary>
        public event Action<KeyDefinition>? Selected;

        private readonly Action<KeyDefinition> _chosenHandler;

        private KeyDefinition? _selectedKey;

        /// <summary>
        /// Creates the overlay under <paramref name="title"/> over <paramref name="dialect"/>'s
        /// actions. <paramref name="recent"/> is the editor's session history, so an action inserted
        /// into a macro here is offered by the rail's <c>Recent</c> chip too.
        /// </summary>
        public TokenPickerOverlayViewModel(string title, TokenDialect dialect, RecentTokenStore? recent = null)
            : base(title)
        {
            Picker = new TokenPickerViewModel(dialect, recent);

            // Taking a row is an accept: §11.6's "double-clicking an item accepts immediately", and
            // ↵ on the highlighted row. The picker and this overlay live and die together, so there
            // is nothing to detach later.
            _chosenHandler = _ => Accept();
            Picker.Chosen += _chosenHandler;

            // A type-to-filter modal opens with its caret in the field. It is asked for here rather
            // than in the view because the request survives until a view exists to answer it —
            // ViewLocator materialises the picker a layout pass after this constructor runs.
            Picker.RequestFocus();
        }

        /// <summary>Puts the caret in the search field — ⌘F, and the panel's own opening state.</summary>
        public void FocusSearch()
        {
            Picker.RequestFocus();
        }

        /// <inheritdoc />
        protected override bool TryAccept()
        {
            if (Picker.SelectedKey is not { } definition)
            {
                ErrorMessage = NoSelectionMessage;

                return false;
            }

            SelectedKey = definition;

            Picker.Remember(definition);

            Selected?.Invoke(definition);

            return true;
        }
    }
}
