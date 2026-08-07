using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Macros tab (mockup <c>1i</c>), which since issue #93 is a <b>library and not an
    /// editor</b>: "every macro has a name, so the tab lists them once — rename, see which keys and
    /// layers fire each one, duplicate, delete" (<c>2i</c>). Editing a macro happens on the key
    /// inspector rail beside the board; this tab never records a keystroke.
    ///
    /// <para><b>One view model, two branches, chosen by the capability record and nothing else.</b>
    /// <see cref="MacroCapability.UsesFlatMacroList"/> picks between the slot branch (<c>1i</c> left:
    /// "Macros on F3 · Freestyle Edge RGB", one card per slot of the selected key) and the flat
    /// branch (<c>1i</c> right: "Macro library · Advantage 360 · one flat list per profile", a table
    /// of every macro). There is no device-specific view and no device id anywhere in this class —
    /// "all screens render from this record" (docs/design/handoff.md).</para>
    ///
    /// <para><b>Both branches list <see cref="MacroLibrary.Entries"/>, never a walk over the
    /// keys.</b> That is the whole point of the redesign: a macro on two keys is one row with two
    /// sites, and the entry <em>is</em> the answer to "which keys and layers fire it". The snapshot
    /// is rebuilt by every mutation, so the rows are rebuilt with it — holding one across an edit
    /// makes it stale and Core refuses it.</para>
    ///
    /// <para><b>It owns no library and no dirty flag.</b> There is exactly one
    /// <see cref="MacroLibrary"/> per open profile and it is the editor's; this class reaches it
    /// through a function, because it arrives with the profile and is replaced by a load or an
    /// import. Renames go through <see cref="IMacroLibraryHost.RenameMacro"/> — the app's one rename
    /// path — and a delete or a duplicate ends in
    /// <see cref="IMacroLibraryHost.CommitMacroLibraryEdit"/>.</para>
    ///
    /// <para><b>Nothing here blocks.</b> A macro past the device's per-macro cap keeps its row, its
    /// name and its save; the card carries mockup <c>1i</c>'s amber sentence and the meter goes
    /// amber with it. The one question this tab asks is the delete confirmation, which exists to
    /// <em>name the sites</em> before they go, not to refuse anything.</para>
    /// </summary>
    public sealed partial class MacroLibraryViewModel : ViewModelBase
    {
        /// <summary>The slot branch's header (mockup <c>1i</c>: "Macros on F3 · Freestyle Edge RGB").</summary>
        public const string SlotHeaderFormat = "Macros on {0} · {1}";

        /// <summary>The same with nothing selected on the board.</summary>
        public const string SlotHeaderWithoutKeyFormat = "Macros · {0}";

        /// <summary>The slot branch's subtitle (<c>1i</c>: "3 of 5 slots used · 1 active").</summary>
        public const string SlotSubtitleFormat = "{0} of {1} slots used · {2} active";

        /// <summary>What the slot branch says while no position is selected. This app's wording.</summary>
        public const string NoKeySubtitle = "Pick a key on the Layout tab to see its macro slots.";

        /// <summary>The slot branch's action (<c>1i</c>, verbatim).</summary>
        public const string PickAnotherKeyCaption = "Pick another key";

        /// <summary>The flat branch's header (<c>1i</c>: "Macro library · Advantage 360 · one flat list per profile").</summary>
        public const string FlatHeaderFormat = "Macro library · {0} · one flat list per profile";

        /// <summary>The flat branch's subtitle (<c>1i</c>: "24 macros · trigger + layer set per macro").</summary>
        public const string FlatSubtitleFormat = "{0} · trigger + layer set per macro";

        /// <summary>The flat branch's action (<c>1i</c>, verbatim).</summary>
        public const string NewMacroCaption = "New macro";

        /// <summary>The search field's placeholder (<c>1i</c>, verbatim).</summary>
        public const string SearchPlaceholder = "Search name, trigger, or contents…";

        /// <summary>The table's first column (<c>1i</c>: "Macro / Trigger / Layer / Length").</summary>
        public const string MacroColumnCaption = "Macro";

        /// <summary>Its second.</summary>
        public const string TriggerColumnCaption = "Trigger";

        /// <summary>Its third.</summary>
        public const string LayerColumnCaption = "Layer";

        /// <summary>Its fourth.</summary>
        public const string LengthColumnCaption = "Length";

        /// <summary>
        /// The heading over the profile's whole library on the <b>slot</b> branch. Mockup <c>1i</c>
        /// draws only the selected key's four cards there; every macro of the profile still has to be
        /// reachable by name, and this is where. See docs/app/design-system.md for the deviation.
        /// </summary>
        public const string LibrarySectionTitle = "Every macro in this profile";

        /// <summary>The layout keystroke meter's label (<c>1i</c>, verbatim).</summary>
        public const string LayoutBudgetMeterLabel = "layout keystroke budget";

        /// <summary>The per-macro meter's label (<c>1i</c>, verbatim).</summary>
        public const string MacroLengthMeterLabel = "this macro";

        /// <summary>The macro-count meter's label. This app's wording; <c>1i</c> draws two meters.</summary>
        public const string MacroCountMeterLabel = "macros";

        /// <summary>Singular of the flat branch's count.</summary>
        public const string OneMacroCaption = "1 macro";

        /// <summary>Plural of it.</summary>
        public const string ManyMacrosFormat = "{0} macros";

        /// <summary>The per-row rename action. Sentence case, as the redesign draws its actions.</summary>
        public const string RenameCaption = "Rename";

        /// <summary>Commits the name being typed.</summary>
        public const string SaveNameCaption = "Save name";

        /// <summary>Abandons it.</summary>
        public const string CancelRenameCaption = "Cancel";

        /// <summary>The per-row duplicate action.</summary>
        public const string DuplicateCaption = "Duplicate";

        /// <summary>The per-row delete action.</summary>
        public const string DeleteCaption = "Delete";

        /// <summary>Opens the macro where it is actually edited — the key inspector rail.</summary>
        public const string EditCaption = "Edit on the board";

        /// <summary>What a rename field says it is for. This app's wording.</summary>
        public const string RenameWatermark = "Name this macro";

        /// <summary>Shown on a device without macro support (<see cref="MacroCapability.IsSupported"/>).</summary>
        public const string NotSupportedMessage = "This device does not support macros.";

        /// <summary>Shown when the profile holds no macro at all. This app's wording.</summary>
        public const string NoMacrosMessage = "No macros in this profile yet.";

        /// <summary>Shown when the search and the layer filter agree on nothing. This app's wording.</summary>
        public const string NoMatchesMessage = "No macro matches this search.";

        /// <summary>Refusal when a duplicate has nowhere to go (06 §1: all slots taken).</summary>
        public const string DuplicateRefusedMessage = "Every macro slot of this key is taken.";

        /// <summary>The delete confirmation's title. This app's wording.</summary>
        public const string DeleteTitle = "Delete this macro?";

        /// <summary>
        /// Its body: <c>{0}</c> is the macro's name, <c>{1}</c> the sites it fires from and
        /// <c>{2}</c> what it is removed from. <b>The sites are named in the question</b>, because a
        /// macro on two keys disappears from both.
        /// </summary>
        public const string DeleteMessageFormat = "{0} fires from {1}. Deleting it removes the macro from {2}.";

        /// <summary>What a one-site macro is removed from.</summary>
        public const string DeleteOneSiteCaption = "that key";

        /// <summary>What a several-site macro is removed from.</summary>
        public const string DeleteManySitesFormat = "all {0} keys";

        /// <summary>The affirmative, labelled by outcome (mockup <c>1k</c>'s rule).</summary>
        public const string DeleteConfirmCaption = "Delete macro";

        /// <summary>The way out, labelled by outcome too.</summary>
        public const string DeleteDeclineCaption = "Keep it";

        /// <summary>Builds the flat branch's macro count: "1 macro" / "24 macros".</summary>
        public static string BuildMacroCountCaption(int count)
        {
            return count == 1
                ? OneMacroCaption
                : string.Format(CultureInfo.InvariantCulture, ManyMacrosFormat, AdvisoryText.Number(count));
        }

        /// <summary>Builds the delete confirmation's body for a macro with <paramref name="siteCount"/> sites.</summary>
        public static string BuildDeleteMessage(string name, string sites, int siteCount)
        {
            var removedFrom = siteCount <= 1
                ? DeleteOneSiteCaption
                : string.Format(CultureInfo.InvariantCulture, DeleteManySitesFormat, AdvisoryText.Number(siteCount));

            return string.Format(CultureInfo.InvariantCulture, DeleteMessageFormat, name, sites, removedFrom);
        }

        /// <summary>Whether the device supports macros at all. False draws the refusal instead.</summary>
        public bool IsSupported { get; }

        /// <summary>
        /// Whether the profile's macros live in one flat list rather than in the trigger key's
        /// numbered slots (Advantage360, 06 §1) — the <b>one</b> fact that picks the branch.
        /// </summary>
        public bool UsesFlatMacroList { get; }

        /// <summary>Whether the slot branch is the one drawn. Bound directly; a view cannot negate.</summary>
        public bool UsesMacroSlots { get; }

        /// <summary>"Macros on F3 · Freestyle Edge RGB", or the flat branch's own header.</summary>
        public string Header
        {
            get => _header;
            private set => SetProperty(ref _header, value);
        }

        /// <summary>"3 of 5 slots used · 1 active", or "24 macros · trigger + layer set per macro".</summary>
        public string Subtitle
        {
            get => _subtitle;
            private set => SetProperty(ref _subtitle, value);
        }

        /// <summary>"Pick another key" on the slot branch, "New macro" on the flat one.</summary>
        public string PrimaryActionCaption { get; }

        /// <summary>The selected key's slot cards, in slot order; empty on the flat branch.</summary>
        public IReadOnlyList<MacroSlotViewModel> Slots
        {
            get => _slots;
            private set
            {
                if (SetProperty(ref _slots, value))
                {
                    OnPropertyChanged(nameof(HasSlots));
                }
            }
        }

        /// <summary>Whether there are slot cards to draw.</summary>
        public bool HasSlots => _slots.Count > 0;

        /// <summary>
        /// The profile's macros, one row per name, narrowed by <see cref="SearchQuery"/> and
        /// <see cref="SelectedLayerFilter"/>.
        /// </summary>
        public IReadOnlyList<MacroLibraryRowViewModel> Rows
        {
            get => _rows;
            private set
            {
                if (SetProperty(ref _rows, value))
                {
                    OnPropertyChanged(nameof(HasRows));
                    OnPropertyChanged(nameof(HasNoMatches));
                }
            }
        }

        /// <summary>Whether the filters left anything to draw.</summary>
        public bool HasRows => _rows.Count > 0;

        /// <summary>
        /// Whether the profile holds macros but the search and the layer filter agree on none — a
        /// different thing to say from "there are no macros yet", and neither is an error.
        /// </summary>
        public bool HasNoMatches => _rows.Count == 0 && _totalCount > 0;

        /// <summary>How many macros the profile holds in total, before any filter.</summary>
        public int TotalCount
        {
            get => _totalCount;
            private set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    OnPropertyChanged(nameof(IsEmpty));
                    OnPropertyChanged(nameof(HasNoMatches));
                }
            }
        }

        /// <summary>Whether the profile holds no macro at all.</summary>
        public bool IsEmpty => _totalCount == 0;

        /// <summary>
        /// The search box's text (mockup <c>1i</c>: "Search name, trigger, or contents…"). It
        /// narrows <see cref="Rows"/> on every keystroke and writes nothing.
        /// </summary>
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value ?? string.Empty))
                {
                    ApplyFilters();
                }
            }
        }

        /// <summary>The <c>All layers ▾</c> options: everything, then one per layer.</summary>
        public IReadOnlyList<MacroLayerFilter> LayerFilters
        {
            get => _layerFilters;
            private set => SetProperty(ref _layerFilters, value);
        }

        /// <summary>Which of them is chosen. The "everything" row is the default and never null.</summary>
        public MacroLayerFilter? SelectedLayerFilter
        {
            get => _selectedLayerFilter;
            set
            {
                if (SetProperty(ref _selectedLayerFilter, value ?? _layerFilters[0]))
                {
                    ApplyFilters();
                }
            }
        }

        /// <summary>"layout keystroke budget 5 140 / 7 200" (04 §5.3, mockup <c>1i</c>).</summary>
        public MacroMeterViewModel LayoutKeystrokeMeter { get; }

        /// <summary>"this macro 512 / 500" — the selected slot card's macro (06 §6, mockup <c>1i</c>).</summary>
        public MacroMeterViewModel MacroLengthMeter { get; }

        /// <summary>"macros 24 / 100" — the profile against the firmware-resolved count (06 §6, 09 §2).</summary>
        public MacroMeterViewModel MacroCountMeter { get; }

        /// <summary>The tab's own refusal or status line, or an empty string.</summary>
        public string Message
        {
            get => _message;
            private set
            {
                if (SetProperty(ref _message, value))
                {
                    OnPropertyChanged(nameof(HasMessage));
                }
            }
        }

        /// <summary>Whether the tab has something to say about the last action.</summary>
        public bool HasMessage => _message.Length > 0;

        /// <summary>"Pick another key" / "New macro" — see <see cref="PrimaryActionCaption"/>.</summary>
        public IRelayCommand PrimaryActionCommand { get; }

        /// <summary>Points the <c>this macro</c> meter at one slot card. Writes nothing.</summary>
        public IRelayCommand<MacroSlotViewModel> SelectSlotCommand { get; }

        /// <summary>
        /// <c>Make active</c>: moves <c>KeyboardKey.ActiveMacroIndex</c> to this card's slot. An
        /// in-memory field only (05 §1.3) — it is never serialized, so it does not dirty the session.
        /// </summary>
        public IRelayCommand<MacroSlotViewModel> MakeActiveCommand { get; }

        /// <summary>
        /// <c>＋ Record a macro</c>: jumps to the key inspector — board, position, Macro panel,
        /// Record armed. <b>The tab never captures a keystroke itself.</b>
        /// </summary>
        public IRelayCommand<MacroSlotViewModel> RecordMacroCommand { get; }

        /// <summary>Opens a row's macro where it is edited: the board's rail, on the macro's own site.</summary>
        public IRelayCommand<MacroLibraryRowViewModel> EditMacroCommand { get; }

        /// <summary>Opens the in-place rename field on one row. Only ever one at a time.</summary>
        public IRelayCommand<MacroLibraryRowViewModel> BeginRenameCommand { get; }

        /// <summary>Writes the typed name through the editor's one rename path. Blank clears it.</summary>
        public IRelayCommand<MacroLibraryRowViewModel> CommitRenameCommand { get; }

        /// <summary>Closes the rename field without writing.</summary>
        public IRelayCommand<MacroLibraryRowViewModel> CancelRenameCommand { get; }

        /// <summary>Makes an independent second macro with the same content (<see cref="MacroLibrary.Duplicate"/>).</summary>
        public IRelayCommand<MacroLibraryRowViewModel> DuplicateCommand { get; }

        /// <summary>Removes the macro from <b>every</b> site, after naming them in a question.</summary>
        public IAsyncRelayCommand<MacroLibraryRowViewModel> DeleteCommand { get; }

        private readonly IMacroLibraryHost _host;
        private readonly Func<MacroLibrary?> _resolveLibrary;
        private readonly MacroCapability _capability;
        private readonly string _deviceName;
        private readonly int? _maxMacroCount;
        private IReadOnlyList<MacroLibraryRowViewModel> _allRows = [];
        private IReadOnlyList<MacroLibraryRowViewModel> _rows = [];
        private IReadOnlyList<MacroSlotViewModel> _slots = [];
        private IReadOnlyList<MacroLayerFilter> _layerFilters = [MacroLayerFilter.All()];
        private MacroLayerFilter? _selectedLayerFilter;
        private KeyboardKeyViewModel? _key;
        private KeyboardLayerViewModel? _layer;
        private KeyboardLayout? _layout;
        private string _header = string.Empty;
        private string _subtitle = string.Empty;
        private string _searchQuery = string.Empty;
        private string _message = string.Empty;
        private int _totalCount;

        /// <summary>Which slot card the <c>this macro</c> meter reads; 0 while none is chosen.</summary>
        private int _selectedSlot;

        /// <summary>
        /// The name of the row whose rename field is open, so the field survives the rebuild that
        /// every refresh does. Rows are snapshots; the rename is the user's.
        /// </summary>
        private string? _renamingName;

        /// <summary>
        /// Builds the tab for one open device. Everything it needs at construction is a
        /// <em>device</em> fact — the macro capability, the display name and the firmware-resolved
        /// macro count — which is what lets it be built once, eagerly, before any profile has been
        /// read; <paramref name="resolveLibrary"/> is how it reaches the editor's <b>one</b>
        /// <see cref="MacroLibrary"/>, which arrives with the profile and is replaced by a load or an
        /// import.
        /// </summary>
        public MacroLibraryViewModel(DeviceSnapshot device, IMacroLibraryHost host, Func<MacroLibrary?> resolveLibrary)
        {
            ArgumentNullException.ThrowIfNull(device);

            _host = host ?? throw new ArgumentNullException(nameof(host));
            _resolveLibrary = resolveLibrary ?? throw new ArgumentNullException(nameof(resolveLibrary));
            _capability = device.Device.Macros;
            _deviceName = device.DisplayName;
            _maxMacroCount = MacroLimits.ResolveMaxMacroCount(device);

            IsSupported = _capability.IsSupported;
            UsesFlatMacroList = _capability.UsesFlatMacroList;

            // The strip offers the slots the dialect WRITES, not the five the model owns: a macro put
            // in slot 4 of a Freestyle Edge is dropped by the very next save (06 §1).
            UsesMacroSlots = _capability.PersistedSlotsPerKey is > 0 && !_capability.UsesFlatMacroList;

            PrimaryActionCaption = UsesFlatMacroList ? NewMacroCaption : PickAnotherKeyCaption;

            LayoutKeystrokeMeter = new MacroMeterViewModel(LayoutBudgetMeterLabel);
            MacroLengthMeter = new MacroMeterViewModel(MacroLengthMeterLabel);
            MacroCountMeter = new MacroMeterViewModel(MacroCountMeterLabel);

            _selectedLayerFilter = _layerFilters[0];

            PrimaryActionCommand = new RelayCommand(RunPrimaryAction);
            SelectSlotCommand = new RelayCommand<MacroSlotViewModel>(SelectSlot);
            MakeActiveCommand = new RelayCommand<MacroSlotViewModel>(MakeActive);
            RecordMacroCommand = new RelayCommand<MacroSlotViewModel>(RecordMacro);
            EditMacroCommand = new RelayCommand<MacroLibraryRowViewModel>(EditMacro);
            BeginRenameCommand = new RelayCommand<MacroLibraryRowViewModel>(BeginRename);
            CommitRenameCommand = new RelayCommand<MacroLibraryRowViewModel>(CommitRename);
            CancelRenameCommand = new RelayCommand<MacroLibraryRowViewModel>(CancelRename);
            DuplicateCommand = new RelayCommand<MacroLibraryRowViewModel>(Duplicate);
            DeleteCommand = new AsyncRelayCommand<MacroLibraryRowViewModel>(DeleteAsync);

            RefreshCaptions();
        }

        /// <summary>
        /// Takes the editor's current state and rebuilds everything from it. Called from the same
        /// funnel that ends in <c>RefreshLegend()</c> — Core announces nothing, so a tab that waited
        /// to be told would wait forever — and from the selection paths, which move what the slot
        /// branch is <em>about</em> without writing anything.
        /// </summary>
        public void Refresh(KeyboardKeyViewModel? key, KeyboardLayerViewModel? layer, KeyboardLayout? layout)
        {
            _key = key;
            _layer = layer;
            _layout = layout;

            RebuildLayerFilters();
            RebuildRows();
            RebuildSlots();
            ApplyFilters();
            RefreshCaptions();
            RefreshMeters();
        }

        /// <summary>
        /// Rebuilds the rows from the library's <b>current</b> snapshot. Every mutation replaces
        /// <see cref="MacroLibrary.Entries"/>, so a row held across one is stale and Core refuses it
        /// — which is why they are rebuilt rather than re-marked.
        /// </summary>
        private void RebuildRows()
        {
            if (_resolveLibrary() is not { } library || _layout is not { } layout)
            {
                _allRows = [];
                TotalCount = 0;

                return;
            }

            var rows = new List<MacroLibraryRowViewModel>(library.Entries.Count);

            foreach (var entry in library.Entries)
            {
                var row = new MacroLibraryRowViewModel(entry, layout, _capability.MaxCharactersPerMacro);

                // The open rename field belongs to the user, not to the snapshot it was opened over.
                if (_renamingName is { } renaming && string.Equals(renaming, row.Name, StringComparison.Ordinal))
                {
                    row.IsRenaming = true;
                }

                rows.Add(row);
            }

            _allRows = rows;
            TotalCount = rows.Count;
        }

        /// <summary>
        /// Rebuilds the selected key's slot cards. Empty on the flat branch, and empty while nothing
        /// that can carry a macro is selected — a card for a position that refuses macros (05 §5.3)
        /// would be an affordance that cannot work.
        /// </summary>
        private void RebuildSlots()
        {
            if (!UsesMacroSlots || _key is not { } key || !key.CanAssignMacro || _capability.PersistedSlotsPerKey is not int slotCount)
            {
                Slots = [];

                return;
            }

            var cards = new List<MacroSlotViewModel>(slotCount);

            for (var slot = Macro.MinMacroIndex; slot <= slotCount; slot++)
            {
                var macro = key.Key.GetMacro(slot);

                cards.Add(new MacroSlotViewModel(slot, FindRow(macro), key.Key.ActiveMacroIndex == slot));
            }

            Slots = cards;

            ApplySlotSelection();
        }

        /// <summary>
        /// The row whose entry holds <paramref name="macro"/> — reference identity, which is how a
        /// key's slot maps back onto a library row without a second grouping pass.
        /// </summary>
        private MacroLibraryRowViewModel? FindRow(Macro? macro)
        {
            if (macro is null)
            {
                return null;
            }

            foreach (var row in _allRows)
            {
                foreach (var site in row.Entry.Sites)
                {
                    if (ReferenceEquals(site.Macro, macro))
                    {
                        return row;
                    }
                }
            }

            return null;
        }

        private void RebuildLayerFilters()
        {
            var chosen = _selectedLayerFilter?.LayerIndex;

            LayerFilters = MacroLayerFilter.BuildAll(_layout);

            MacroLayerFilter? restored = null;

            foreach (var filter in _layerFilters)
            {
                if (filter.LayerIndex == chosen)
                {
                    restored = filter;

                    break;
                }
            }

            SetProperty(ref _selectedLayerFilter, restored ?? _layerFilters[0], nameof(SelectedLayerFilter));
        }

        /// <summary>Narrows the rows by the search text and the layer filter. Writes nothing.</summary>
        private void ApplyFilters()
        {
            var layerIndex = _selectedLayerFilter?.LayerIndex;
            var matches = new List<MacroLibraryRowViewModel>(_allRows.Count);

            foreach (var row in _allRows)
            {
                if (row.Matches(_searchQuery) && row.IsOnLayer(layerIndex))
                {
                    matches.Add(row);
                }
            }

            Rows = matches;
        }

        /// <summary>Re-marks which card the <c>this macro</c> meter is reading.</summary>
        private void ApplySlotSelection()
        {
            MacroSlotViewModel? selected = null;

            foreach (var card in _slots)
            {
                card.IsSelected = card.Slot == _selectedSlot;

                if (card.IsSelected)
                {
                    selected = card;
                }
            }

            // Nothing chosen yet, or the chosen slot went away: fall back to the slot the key
            // actually fires, which is what the header is about.
            if (selected is null)
            {
                foreach (var card in _slots)
                {
                    if (card.IsActive)
                    {
                        card.IsSelected = true;
                        _selectedSlot = card.Slot;
                        selected = card;

                        break;
                    }
                }
            }
        }

        private void RefreshCaptions()
        {
            Header = UsesFlatMacroList ? BuildFlatHeader() : BuildSlotHeader();
            Subtitle = UsesFlatMacroList ? BuildFlatSubtitle() : BuildSlotSubtitle();
        }

        private string BuildFlatHeader()
        {
            return string.Format(CultureInfo.InvariantCulture, FlatHeaderFormat, _deviceName);
        }

        private string BuildFlatSubtitle()
        {
            return string.Format(CultureInfo.InvariantCulture, FlatSubtitleFormat, BuildMacroCountCaption(TotalCount));
        }

        /// <summary>
        /// "Macros on F3 · Freestyle Edge RGB". The key is named by its <b>trigger</b> identity
        /// rather than by its current caption: a macro matches on <c>KeyboardKey.TriggerKey</c>
        /// (05 §1.3), so a remapped position still fires the macro of the key it was.
        /// </summary>
        private string BuildSlotHeader()
        {
            if (_key is not { } key || _layout is not { } layout)
            {
                return string.Format(CultureInfo.InvariantCulture, SlotHeaderWithoutKeyFormat, _deviceName);
            }

            var caption = KeyCaption.For(key.Key.TriggerKey, layout.Dialect, KeyCaption.IsMacOs, EmbeddedFontGlyphCoverage.Instance);

            return string.Format(CultureInfo.InvariantCulture, SlotHeaderFormat, caption, _deviceName);
        }

        private string BuildSlotSubtitle()
        {
            if (_slots.Count == 0)
            {
                return NoKeySubtitle;
            }

            var used = 0;
            var active = 0;

            foreach (var card in _slots)
            {
                if (card.IsOccupied)
                {
                    used++;
                }

                if (card.IsActive)
                {
                    active++;
                }
            }

            return string.Format(CultureInfo.InvariantCulture, SlotSubtitleFormat, used, _slots.Count, active);
        }

        /// <summary>
        /// Re-reads the three meters. <b>A null limit is "no limit", never zero</b> — the Advantage2
        /// states no macro count — and over budget is amber and still saves.
        /// </summary>
        private void RefreshMeters()
        {
            LayoutKeystrokeMeter.Set(_layout?.TotalKeystrokes ?? 0, _capability.MaxTotalKeystrokes);
            MacroCountMeter.Set(_layout?.MacroCount ?? 0, _maxMacroCount);

            var selected = FindSelectedSlot();

            MacroLengthMeter.Set(selected?.Row?.Length ?? 0, _capability.MaxCharactersPerMacro);
        }

        private MacroSlotViewModel? FindSelectedSlot()
        {
            foreach (var card in _slots)
            {
                if (card.IsSelected)
                {
                    return card;
                }
            }

            return null;
        }
    }
}
