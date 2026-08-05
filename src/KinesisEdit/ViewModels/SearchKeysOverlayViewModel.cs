using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Search Keys overlay of specs/11-feature-dialogs.md §11.6: a type-to-filter list over
    /// every assignable action, whose result is one <see cref="KeyDefinition"/>. The catalog and
    /// the filter rule are Core's (<see cref="KeySearchCatalog"/>); this view model owns only the
    /// query, the selection, and the §11.6 validation.
    /// </summary>
    /// <remarks>
    /// The title is caller-supplied because §11.6 names four of them — the four
    /// <c>...Title</c> constants below — one per call site.
    /// </remarks>
    public sealed class SearchKeysOverlayViewModel : EditorOverlayViewModel
    {
        /// <summary>Title of the plain picker (§11.6).</summary>
        public const string DefaultTitle = "Search Keys";

        /// <summary>Title when picking a keystroke for a macro (§11.6).</summary>
        public const string MacroTitle = "Search Keys (Macro)";

        /// <summary>Title when picking the Tap Action of a tap-and-hold (§11.1, §11.6).</summary>
        public const string TapActionTitle = "Search Keys (Tap Action)";

        /// <summary>Title when picking the Hold Action of a tap-and-hold (§11.1, §11.6).</summary>
        public const string HoldActionTitle = "Search Keys (Hold Action)";

        /// <summary>Label over the filter box (§11.6).</summary>
        public const string SearchLabel = "Search key";

        /// <summary>Validation shown when Ok is pressed with nothing selected (§11.6).</summary>
        public const string NoSelectionMessage = "You must select a key";

        /// <summary>The filter text; every change re-runs the incremental filter of §11.6.</summary>
        public string Query
        {
            get => _query;
            set
            {
                if (SetProperty(ref _query, value))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>The rows currently listed — every entry when <see cref="Query"/> is blank.</summary>
        public IReadOnlyList<KeySearchEntry> Results
        {
            get => _results;
            private set => SetProperty(ref _results, value);
        }

        /// <summary>The highlighted row, or null when the list has no selection.</summary>
        public KeySearchEntry? SelectedEntry
        {
            get => _selectedEntry;
            set => SetProperty(ref _selectedEntry, value);
        }

        /// <summary>The picked action once the overlay was accepted; null until then.</summary>
        public KeyDefinition? SelectedKey
        {
            get => _selectedKey;
            private set => SetProperty(ref _selectedKey, value);
        }

        /// <summary>
        /// Selects the given row and accepts in one step. The view binds it to its
        /// double-click gesture (§11.6: "double-clicking an item accepts immediately") — the
        /// gesture stays in the view, so this view model never learns what a double-click is.
        /// </summary>
        public IRelayCommand<KeySearchEntry?> ChooseCommand { get; }

        /// <summary>Raised once with the picked action when the overlay is accepted.</summary>
        public event Action<KeyDefinition>? Selected;

        private readonly IReadOnlyList<KeySearchEntry> _allEntries;

        private string _query = string.Empty;
        private IReadOnlyList<KeySearchEntry> _results;
        private KeySearchEntry? _selectedEntry;
        private KeyDefinition? _selectedKey;

        /// <summary>Creates the overlay under <paramref name="title"/> over <paramref name="dialect"/>'s actions.</summary>
        public SearchKeysOverlayViewModel(string title, TokenDialect dialect)
            : base(title)
        {
            _allEntries = KeySearchCatalog.Build(dialect);
            _results = _allEntries;
            ChooseCommand = new RelayCommand<KeySearchEntry?>(Choose);
        }

        /// <inheritdoc />
        protected override bool TryAccept()
        {
            if (SelectedEntry is not { } entry)
            {
                ErrorMessage = NoSelectionMessage;

                return false;
            }

            SelectedKey = entry.Definition;
            Selected?.Invoke(entry.Definition);

            return true;
        }

        private void Choose(KeySearchEntry? entry)
        {
            if (entry is not null)
            {
                SelectedEntry = entry;
            }

            Accept();
        }

        /// <summary>
        /// Narrows the list to the current query and drops a selection the query filtered out, so
        /// Ok can never accept a row the user can no longer see.
        /// </summary>
        private void ApplyFilter()
        {
            Results = KeySearchCatalog.Filter(_allEntries, _query);

            if (SelectedEntry is not null && !Results.Contains(SelectedEntry))
            {
                SelectedEntry = null;
            }
        }
    }
}
