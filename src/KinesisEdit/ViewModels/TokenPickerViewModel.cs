using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The token picker — the search field, the live counter, the filter chips and the grouped
    /// result rows of mockups <c>1e</c>/<c>2a</c>. It answers one question: <em>which action does the
    /// user mean?</em> What is then done with that action belongs to whoever hosts the picker.
    ///
    /// <para><b>One implementation, two call sites.</b> <see cref="RemapPanelViewModel"/> hosts it in
    /// the key inspector rail, where a pick is an assignment; <see cref="TokenPickerOverlayViewModel"/>
    /// hosts the same instance shape in a modal for §11.6's <c>Search Keys (Macro)</c> insertion.
    /// Neither behaviour is in here — the picker raises <see cref="Chosen"/> and stops — which is
    /// what keeps the two from drifting into two pickers.</para>
    ///
    /// <para><b>Every number is derived.</b> The counter's denominator is
    /// <c>KeySearchCatalog.Build(dialect).Count</c> and nothing else. Mockup <c>1e</c> draws
    /// <c>18/1204</c>; neither figure is reachable — the Gen1 searchable catalog is far smaller than
    /// the registry's 1 282 rows, and a query for <c>esc</c> matches a handful — so the mock's numbers
    /// are illustrative and a literal here would be a lie that no test could distinguish from a
    /// regression.</para>
    ///
    /// <para><b>The filter rules are Core's.</b> <see cref="KeySearchCatalog.Filter(IReadOnlyList{KeySearchEntry}, string?)"/>,
    /// <see cref="KeySearchCatalog.Filter(IReadOnlyList{KeySearchEntry}, KeySearchCategory)"/> and
    /// <see cref="KeySearchCatalog.Group"/> are pure functions this class composes; the only filter
    /// it owns is <c>Recent</c>, which is session state Core has no business knowing about.</para>
    ///
    /// <para><b>Rows are rebuilt, not reused.</b> A result set is a few hundred small objects at
    /// worst and is rebuilt per keystroke, which is what the pre-redesign picker did too. Selection
    /// therefore travels as a <see cref="KeyDefinition"/> rather than as a row instance — every
    /// definition comes out of <see cref="KeyRegistry.Entries"/>, so reference identity survives the
    /// rebuild exactly.</para>
    ///
    /// <para><b>It never focuses anything itself.</b> ⌘F reaches the caret through
    /// <see cref="RequestFocus"/> and <see cref="FocusRequested"/>: a view model that touched focus
    /// would need Avalonia types, and the field it wants only exists once
    /// <c>ViewLocator</c> has materialised the view — a layout pass after the command ran.</para>
    /// </summary>
    public sealed class TokenPickerViewModel : ViewModelBase
    {
        /// <summary>
        /// Label and watermark over the query box. Verbatim from specs/11-feature-dialogs.md §11.6,
        /// where it has been pinned since the Search Keys panel; the picker inherits the wording
        /// along with the behaviour.
        /// </summary>
        public const string SearchLabel = "Search key";

        /// <summary>How the counter is spelled: matches over the dialect's whole catalog.</summary>
        public const string CounterFormat = "{0}/{1}";

        /// <summary>What the picker says when the query matches nothing at all.</summary>
        public const string NoMatchesMessage = "No action matches this search.";

        /// <summary>
        /// Below this width the chip row reduces to <c>All, Letters, Nav, Media, Recent</c>
        /// (mockup <c>2a</c>). It is the rail's content width that puts it under the threshold — 268
        /// px less the inspector's own padding — while the modal picker is comfortably above it, so
        /// one rule produces both of the mockups' chip rows.
        /// </summary>
        public const double NarrowWidth = 280;

        /// <summary>The dialect this picker lists, fixed for its lifetime.</summary>
        public TokenDialect Dialect { get; }

        /// <summary>
        /// This session's assignment history, behind the <c>Recent</c> chip. Shared with whoever
        /// created the picker: the rail's picker and the macro-insertion picker of one editor
        /// remember the same things.
        /// </summary>
        public RecentTokenStore Recent { get; }

        /// <summary>
        /// Every row of the dialect's catalog — the counter's denominator, derived from
        /// <see cref="KeySearchCatalog.Build"/> and never a literal.
        /// </summary>
        public int TotalCount => _allEntries.Count;

        /// <summary>How many rows the current query and chip leave.</summary>
        public int MatchCount
        {
            get => _matchCount;
            private set
            {
                if (SetProperty(ref _matchCount, value))
                {
                    OnPropertyChanged(nameof(CounterText));
                    OnPropertyChanged(nameof(HasMatches));
                }
            }
        }

        /// <summary>The counter as it is drawn: <c>18/200</c>.</summary>
        public string CounterText =>
            string.Format(CultureInfo.InvariantCulture, CounterFormat, MatchCount, TotalCount);

        /// <summary>Whether anything survived the filter.</summary>
        public bool HasMatches => _matchCount > 0;

        /// <summary>
        /// The query. Every change re-runs §11.6's incremental filter, which matches the composed
        /// item text or the file token, case-insensitively.
        /// </summary>
        public string Query
        {
            get => _query;
            set
            {
                if (SetProperty(ref _query, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>The chips, in the mockups' order, <c>Recent</c> last.</summary>
        public IReadOnlyList<TokenPickerCategoryViewModel> Categories { get; }

        /// <summary>Which chip is live. Exactly one is, always.</summary>
        public TokenPickerCategoryViewModel SelectedCategory
        {
            get => _selectedCategory;
            private set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    OnPropertyChanged(nameof(IsRecentSelected));
                }
            }
        }

        /// <summary>Whether the live chip is <c>Recent</c> rather than a category.</summary>
        public bool IsRecentSelected => _selectedCategory.IsRecent;

        /// <summary>The results, under their counted headers.</summary>
        public IReadOnlyList<TokenPickerGroupViewModel> Groups
        {
            get => _groups;
            private set => SetProperty(ref _groups, value);
        }

        /// <summary>
        /// The results again, flat and in the order they are drawn — what ↑/↓ walks and what
        /// <see cref="SelectedRow"/> is picked out of.
        /// </summary>
        public IReadOnlyList<TokenPickerRowViewModel> Rows
        {
            get => _rows;
            private set => SetProperty(ref _rows, value);
        }

        /// <summary>
        /// The row <c>↵</c> would take, or null when nothing is highlighted. Settable, because the
        /// view's rows are hit targets; setting it moves the <c>↵ assign</c> hint with it.
        /// </summary>
        public TokenPickerRowViewModel? SelectedRow
        {
            get => _selectedRow;
            set => Select(value);
        }

        /// <summary>The action the highlighted row assigns, or null.</summary>
        public KeyDefinition? SelectedKey => _selectedRow?.Definition;

        /// <summary>Whether there is anything to assign.</summary>
        public bool HasSelection => _selectedRow is not null;

        /// <summary>
        /// Whether the chip row is drawn in its reduced form. Pushed in from the view through
        /// <see cref="SetAvailableWidth"/>; a view model has no other way to know how wide it is.
        /// </summary>
        public bool IsNarrow
        {
            get => _isNarrow;
            private set
            {
                if (SetProperty(ref _isNarrow, value))
                {
                    RefreshChipVisibility();
                }
            }
        }

        /// <summary>Chooses a chip.</summary>
        public IRelayCommand<TokenPickerCategoryViewModel?> SelectCategoryCommand { get; }

        /// <summary>Highlights a row without taking it — the pointer's single click.</summary>
        public IRelayCommand<TokenPickerRowViewModel?> SelectRowCommand { get; }

        /// <summary>
        /// Takes a row: highlights it if one is passed, then raises <see cref="Chosen"/>. The view
        /// binds it to <c>↵</c> and to the double click §11.6 accepts on, so neither gesture is
        /// known here.
        /// </summary>
        public IRelayCommand<TokenPickerRowViewModel?> ChooseCommand { get; }

        /// <summary>Moves the highlight one row down, wrapping at the end.</summary>
        public IRelayCommand SelectNextCommand { get; }

        /// <summary>Moves it one row up, wrapping at the start.</summary>
        public IRelayCommand SelectPreviousCommand { get; }

        /// <summary>
        /// Whether a focus request is still waiting for a view to answer it. <c>ViewLocator</c>
        /// materialises the picker a layout pass <em>after</em> the command that asked for it ran, so
        /// a request made before there is a field to focus has to survive until there is one; the
        /// view clears it as it takes the caret.
        /// </summary>
        public bool IsFocusPending { get; private set; }

        /// <summary>Raised when a row is taken. The host decides what taking one means.</summary>
        public event Action<KeyDefinition>? Chosen;

        /// <summary>Raised by <see cref="RequestFocus"/>; the view puts the caret in its field.</summary>
        public event EventHandler? FocusRequested;

        private readonly IReadOnlyList<KeySearchEntry> _allEntries;
        private readonly EventHandler _recentChangedHandler;
        private IReadOnlyList<TokenPickerGroupViewModel> _groups = [];
        private IReadOnlyList<TokenPickerRowViewModel> _rows = [];
        private TokenPickerCategoryViewModel _selectedCategory;
        private TokenPickerRowViewModel? _selectedRow;
        private KeyDefinition? _selectedDefinition;
        private string _query = string.Empty;
        private int _matchCount;
        private bool _isNarrow;

        /// <summary>
        /// Builds the picker over one dialect's catalog. <paramref name="recent"/> is shared when the
        /// caller has a session store; without one the picker keeps its own, which is what a test and
        /// a standalone modal both want.
        /// </summary>
        public TokenPickerViewModel(TokenDialect dialect, RecentTokenStore? recent = null)
        {
            Dialect = dialect;
            Recent = recent ?? new RecentTokenStore();

            _allEntries = KeySearchCatalog.Build(dialect);

            Categories =
            [
                TokenPickerCategoryViewModel.For(KeySearchCategory.All, survivesNarrow: true),
                TokenPickerCategoryViewModel.For(KeySearchCategory.Letters, survivesNarrow: true),
                TokenPickerCategoryViewModel.For(KeySearchCategory.Nav, survivesNarrow: true),
                TokenPickerCategoryViewModel.For(KeySearchCategory.Media, survivesNarrow: true),
                TokenPickerCategoryViewModel.For(KeySearchCategory.Mouse, survivesNarrow: false),
                TokenPickerCategoryViewModel.For(KeySearchCategory.Hotkeys, survivesNarrow: false),
                TokenPickerCategoryViewModel.Recent()
            ];

            _selectedCategory = Categories[0];
            _selectedCategory.IsSelected = true;

            SelectCategoryCommand = new RelayCommand<TokenPickerCategoryViewModel?>(SelectCategory);
            SelectRowCommand = new RelayCommand<TokenPickerRowViewModel?>(row => Select(row));
            ChooseCommand = new RelayCommand<TokenPickerRowViewModel?>(Choose);
            SelectNextCommand = new RelayCommand(() => Step(1));
            SelectPreviousCommand = new RelayCommand(() => Step(-1));

            // The store outlives no picker it does not own, and the two die together where it is
            // shared, so there is nothing here to unsubscribe from later.
            _recentChangedHandler = (_, _) => OnRecentChanged();
            Recent.Changed += _recentChangedHandler;

            ApplyFilter();
        }

        /// <summary>
        /// Asks the view for the caret — ⌘F, and the panel's own "start typing" affordance. It is a
        /// request rather than a property, because focus is an event in the view's life and not a
        /// state the picker holds.
        /// </summary>
        public void RequestFocus()
        {
            IsFocusPending = true;

            FocusRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Marks the pending request answered. Called by the view once the caret is actually in the
        /// field — never by whoever asked, which has no way of knowing whether a field existed.
        /// </summary>
        public void ClearPendingFocus()
        {
            IsFocusPending = false;
        }

        /// <summary>
        /// Takes the width the picker was given, and reduces the chip row below
        /// <see cref="NarrowWidth"/>. A width of zero — a control that has not been arranged yet —
        /// changes nothing, so a picker never flickers through its narrow form on the way to being
        /// laid out.
        /// </summary>
        public void SetAvailableWidth(double width)
        {
            if (width <= 0 || double.IsNaN(width))
            {
                return;
            }

            IsNarrow = width < NarrowWidth;
        }

        /// <summary>
        /// Empties the query and the highlight, leaving the chip alone. Called when the host reopens
        /// the picker on something else: a stale highlight would let <c>↵</c> assign an action the
        /// user chose for a different key.
        /// </summary>
        public void Clear()
        {
            _query = string.Empty;

            OnPropertyChanged(nameof(Query));

            _selectedDefinition = null;

            ApplyFilter();
        }

        /// <summary>Records an assignment in the session history behind the <c>Recent</c> chip.</summary>
        public void Remember(KeyDefinition key)
        {
            Recent.Remember(key);
        }

        private void SelectCategory(TokenPickerCategoryViewModel? category)
        {
            if (category is null || ReferenceEquals(category, _selectedCategory))
            {
                return;
            }

            foreach (var chip in Categories)
            {
                chip.IsSelected = ReferenceEquals(chip, category);
            }

            SelectedCategory = category;

            ApplyFilter();
        }

        private void Choose(TokenPickerRowViewModel? row)
        {
            if (row is not null)
            {
                Select(row);
            }

            if (_selectedRow is { } chosen)
            {
                Chosen?.Invoke(chosen.Definition);
            }
        }

        private void Step(int offset)
        {
            if (_rows.Count == 0)
            {
                return;
            }

            var current = _selectedRow is null ? -1 : IndexOf(_selectedRow);
            var next = current < 0
                ? (offset > 0 ? 0 : _rows.Count - 1)
                : ((current + offset) % _rows.Count + _rows.Count) % _rows.Count;

            Select(_rows[next]);
        }

        private int IndexOf(TokenPickerRowViewModel row)
        {
            for (var index = 0; index < _rows.Count; index++)
            {
                if (ReferenceEquals(_rows[index], row))
                {
                    return index;
                }
            }

            return -1;
        }

        private void Select(TokenPickerRowViewModel? row)
        {
            _selectedDefinition = row?.Definition;

            ApplySelection(row);
        }

        /// <summary>
        /// Moves the highlight, keeping every row's own flag in step. The flag is on the row because
        /// the <c>↵ assign</c> hint is drawn inside it; the picker holds the one that carries it so a
        /// list rebuilt under a new query cannot leave two rows marked.
        /// </summary>
        private void ApplySelection(TokenPickerRowViewModel? row)
        {
            if (ReferenceEquals(row, _selectedRow))
            {
                return;
            }

            if (_selectedRow is { } previous)
            {
                previous.IsSelected = false;
            }

            _selectedRow = row;

            if (row is not null)
            {
                row.IsSelected = true;
            }

            OnPropertyChanged(nameof(SelectedRow));
            OnPropertyChanged(nameof(SelectedKey));
            OnPropertyChanged(nameof(HasSelection));
        }

        /// <summary>
        /// Re-runs the whole filter: the chip, then the query, then the grouping. The order is free —
        /// Core's two filters compose either way — and the chip goes first because it is the cheaper
        /// cut on every chip but <c>All</c>.
        /// </summary>
        private void ApplyFilter()
        {
            var entries = _selectedCategory.IsRecent
                ? FilterToRecent()
                : KeySearchCatalog.Filter(_allEntries, _selectedCategory.Category);

            entries = KeySearchCatalog.Filter(entries, _query);

            var groups = new List<TokenPickerGroupViewModel>();
            var rows = new List<TokenPickerRowViewModel>(entries.Count);

            foreach (var group in KeySearchCatalog.Group(entries))
            {
                var groupRows = new List<TokenPickerRowViewModel>(group.Count);

                foreach (var entry in group.Entries)
                {
                    var row = new TokenPickerRowViewModel(entry);

                    groupRows.Add(row);
                    rows.Add(row);
                }

                groups.Add(new TokenPickerGroupViewModel(group, groupRows));
            }

            Groups = groups;
            Rows = rows;
            MatchCount = rows.Count;

            RestoreSelection(rows);
        }

        /// <summary>
        /// The <c>Recent</c> chip's own filter, in assignment order rather than catalog order — the
        /// list is a history, and the thing just assigned belongs at the top of it. Core knows
        /// nothing about it: a session's history is not a property of the key table.
        /// </summary>
        private IReadOnlyList<KeySearchEntry> FilterToRecent()
        {
            var matches = new List<KeySearchEntry>(Recent.Entries.Count);

            foreach (var definition in Recent.Entries)
            {
                foreach (var entry in _allEntries)
                {
                    if (ReferenceEquals(entry.Definition, definition))
                    {
                        matches.Add(entry);

                        break;
                    }
                }
            }

            return matches;
        }

        /// <summary>
        /// Puts the highlight back after a rebuild. The rule is: keep what the user chose while it is
        /// still on screen; otherwise highlight the first row, but only once the user has actually
        /// narrowed something. An unfiltered list opens with nothing highlighted, which is what keeps
        /// §11.6's "You must select a key" reachable and what stops <c>↵</c> assigning whichever
        /// action happens to sort first.
        /// </summary>
        private void RestoreSelection(IReadOnlyList<TokenPickerRowViewModel> rows)
        {
            if (_selectedDefinition is { } definition)
            {
                foreach (var row in rows)
                {
                    if (ReferenceEquals(row.Definition, definition))
                    {
                        ApplySelection(row);

                        return;
                    }
                }
            }

            var isNarrowed = _query.Length > 0
                             || _selectedCategory.IsRecent
                             || _selectedCategory.Category != KeySearchCategory.All;

            var fallback = isNarrowed && rows.Count > 0 ? rows[0] : null;

            _selectedDefinition = fallback?.Definition;

            ApplySelection(fallback);
        }

        private void OnRecentChanged()
        {
            if (_selectedCategory.IsRecent)
            {
                ApplyFilter();
            }
        }

        private void RefreshChipVisibility()
        {
            foreach (var chip in Categories)
            {
                chip.IsShown = !_isNarrow || chip.SurvivesNarrow;
            }

            // A chip that just left the row must not stay the live filter, or the picker would be
            // narrowed by something the user can no longer see or undo.
            if (!_selectedCategory.IsShown)
            {
                SelectCategory(Categories[0]);
            }
        }
    }
}
