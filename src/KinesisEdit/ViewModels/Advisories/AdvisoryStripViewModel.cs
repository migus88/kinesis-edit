using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels.Advisories
{
    /// <summary>
    /// The editor's advisory summary strip (mockup <c>1e</c>): the open section's count, its one
    /// calm sentence, and the <c>Review N</c> that walks what the section has a note about.
    /// <para>
    /// <b>It projects; it never builds.</b> The set is the editor's — <see cref="EditorAdvisories"/>
    /// is rebuilt whole after every layout mutation — and this type is handed the current one
    /// together with the open tab and layer (<see cref="Project"/>). Narrowing is all it does, which
    /// is why one control in one row serves all four tabs.
    /// </para>
    /// <para>
    /// <b>It selects through callbacks.</b> Reviewing means moving the editor's selection to a key
    /// cap or a macro row, and neither the board nor the macro panel is this type's to reach. The
    /// two handlers are passed in, so the strip is constructible — and testable — with no loaded
    /// editor anywhere near it.
    /// </para>
    /// <para>
    /// <b>Nothing here blocks.</b> <see cref="ReviewAdvisoriesCommand"/> carries no
    /// <c>CanExecute</c> at all: an advisory never disables anything, so with nothing anchored the
    /// press is a no-op (docs/design/README.md, "advisories never block").
    /// </para>
    /// <para>
    /// <b>It reads one preference.</b> <c>advisory_detail</c> (docs/app/settings.md) decides whether
    /// the sentence is trimmed to one line or shown whole, and it is re-read on every
    /// <see cref="IAppPreferencesStore.Changed"/> — the settings screen and this strip are on the
    /// same screen's tabs, so a toggle there has to move the strip without reopening the editor.
    /// The subscription is why this type is <see cref="IDisposable"/>: the store outlives the
    /// editor, and a strip that stayed hooked would keep a closed editor's projection alive.
    /// </para>
    /// </summary>
    public sealed class AdvisoryStripViewModel : ViewModelBase, IDisposable
    {
        /// <summary>
        /// The post-save toast: the device's own refresh wording, plus what the profile carries
        /// notes about. The count is a <b>report on a write that happened</b> — an advisory never
        /// stopped the save, and the toast is reached only on success — so it is stated in the same
        /// breath rather than warned about separately.
        /// <para>
        /// It takes <see cref="EditorAdvisories.Total"/>, not <see cref="AdvisoryCount"/>: the
        /// toast is about the whole profile that was just written, while the strip is about the
        /// section the user happens to be looking at.
        /// </para>
        /// </summary>
        public static string? BuildPostSaveMessage(string? postSaveMessage, int advisoryCount)
        {
            if (advisoryCount == 0)
            {
                return postSaveMessage;
            }

            var advisories = AdvisoryText.SavedWithAdvisories(advisoryCount);

            return postSaveMessage is null ? advisories : postSaveMessage + " " + advisories;
        }

        /// <summary>
        /// How many advisories the <b>open section</b> carries — the Layout tab narrowed to the
        /// shown layer, every other tab across the whole profile. This is the strip's count, not
        /// <see cref="EditorAdvisories.Total"/>, which is the save toast's.
        /// </summary>
        public int AdvisoryCount
        {
            get => _advisoryCount;
            private set
            {
                if (SetProperty(ref _advisoryCount, value))
                {
                    OnPropertyChanged(nameof(HasAdvisories));
                    OnPropertyChanged(nameof(ReviewCaption));
                }
            }
        }

        /// <summary>The open section's one calm sentence, or empty when it has nothing to say.</summary>
        public string AdvisorySummary
        {
            get => _advisorySummary;
            private set => SetProperty(ref _advisorySummary, value);
        }

        /// <summary>Whether the summary strip is on screen at all; it collapses when it is not.</summary>
        public bool HasAdvisories => AdvisoryCount > 0;

        /// <summary>The strip's action caption — "Review 3" (mockup 1e).</summary>
        public string ReviewCaption => AdvisoryText.Review(AdvisoryCount);

        /// <summary>
        /// Whether the sentence is shown whole instead of trimmed to one line — the user-facing
        /// value of <c>advisory_detail</c> ("Explain advisory warnings in full instead of one
        /// line", mockup 1j), off by default.
        /// <para>
        /// It is read through <see cref="AppPreferenceCatalog.AdvisoryDetail"/>'s
        /// <see cref="AppPreferenceDescriptor.GetValue"/> and never off the stored flag:
        /// <c>advisory_detail</c> is a <i>display</i> preference where <c>on</c> means "expand",
        /// the opposite polarity to the twelve <c>*_msg</c> hide flags beside it in the same file
        /// (docs/app/settings.md, "the two polarities").
        /// </para>
        /// <para>
        /// The view turns this into wrapping instead of an ellipsis and lets the strip's height
        /// grow past <c>HeightAdvisoryStrip</c>. It is still a layout change and nothing else: the
        /// strip stays amber, stays non-blocking, and grows without animating — the motion budget
        /// is fixed and small, and a bar that resized under a moving pointer would be noise.
        /// </para>
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            private set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// <c>Review N</c>: moves the selection to the next thing the open section has a note
        /// about — a key cap on the Layout tab, a macro row on the Macros tab — and cycles round on
        /// each further press.
        /// <para>
        /// <b>It selects; it never starts a remap.</b> Reviewing is reading, and putting the cap
        /// into listening state would eat the user's next keystroke. It carries no
        /// <c>CanExecute</c> either: an advisory never disables anything, so with nothing anchored
        /// the press is simply a no-op.
        /// </para>
        /// </summary>
        public IRelayCommand ReviewAdvisoriesCommand { get; }

        private readonly Action<AdvisoryAnchor> _selectKey;
        private readonly Action<AdvisoryAnchor> _selectMacro;
        private readonly IAppPreferencesStore _preferences;
        private readonly Action _preferencesChangedHandler;
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private EditorTab _tab = EditorTab.Keys;
        private int? _layerIndex;
        private string _advisorySummary = string.Empty;
        private int _advisoryCount;
        private bool _isExpanded;
        private bool _isDisposed;

        /// <summary>Where <c>Review</c> has walked to; -1 until it is pressed, and again whenever the set moves.</summary>
        private int _reviewIndex = -1;

        /// <summary>
        /// Creates the strip. <paramref name="selectKey"/> puts the board's selection on the
        /// anchored cap and <paramref name="selectMacro"/> opens the anchored macro row; both are
        /// the editor's, because the board and the macro panel are.
        /// <paramref name="preferences"/> is the open session's <c>app_settings.txt</c>; a null one
        /// means "no device", and every preference then sits at its default.
        /// </summary>
        public AdvisoryStripViewModel(
            Action<AdvisoryAnchor> selectKey,
            Action<AdvisoryAnchor> selectMacro,
            IAppPreferencesStore? preferences = null)
        {
            _selectKey = selectKey ?? throw new ArgumentNullException(nameof(selectKey));
            _selectMacro = selectMacro ?? throw new ArgumentNullException(nameof(selectMacro));
            _preferences = preferences ?? NullAppPreferencesStore.Instance;

            ReviewAdvisoriesCommand = new RelayCommand(ReviewAdvisories);

            _preferencesChangedHandler = ReadPreferences;
            _preferences.Changed += _preferencesChangedHandler;

            ReadPreferences();
        }

        /// <summary>
        /// Re-projects a set onto the open section. Called both when the set is rebuilt and when
        /// only the tab or the layer moved — neither of which changes the model, which is why this
        /// is the cheap half of the refresh. The review walk starts over, because it walked a
        /// different list.
        /// </summary>
        public void Project(EditorAdvisories advisories, EditorTab tab, int? layerIndex)
        {
            ArgumentNullException.ThrowIfNull(advisories);

            _advisories = advisories;
            _tab = tab;
            _layerIndex = layerIndex;
            _reviewIndex = -1;

            AdvisoryCount = advisories.ForTab(tab, layerIndex).Count;
            AdvisorySummary = advisories.SummaryFor(tab, layerIndex);
        }

        /// <summary>
        /// Walks the open section's anchored advisories, one per press. The Layout tab's strip is
        /// narrowed to the shown layer, so a key anchor is always on the board in front of the user
        /// and nothing here has to switch layers.
        /// </summary>
        private void ReviewAdvisories()
        {
            var targets = ReviewTargets();

            if (targets.Count == 0)
            {
                return;
            }

            _reviewIndex = (_reviewIndex + 1) % targets.Count;

            var anchor = targets[_reviewIndex].Anchor;

            if (_tab == EditorTab.Macros)
            {
                _selectMacro(anchor);

                return;
            }

            _selectKey(anchor);
        }

        /// <summary>The open section's advisories that name something selectable.</summary>
        private IReadOnlyList<AdvisoryViewModel> ReviewTargets()
        {
            var targets = new List<AdvisoryViewModel>();

            foreach (var advisory in _advisories.ForTab(_tab, _layerIndex))
            {
                if (advisory.KeyIndex is not null)
                {
                    targets.Add(advisory);
                }
            }

            return targets;
        }

        /// <summary>
        /// Re-reads <c>advisory_detail</c> off the store. Run once at construction and again on
        /// every <see cref="IAppPreferencesStore.Changed"/>, because the preference is edited on
        /// the Settings tab of the very editor this strip is drawn in.
        /// </summary>
        private void ReadPreferences()
        {
            IsExpanded = AppPreferenceCatalog.AdvisoryDetail.GetValue(_preferences.Current);
        }

        /// <summary>
        /// Comes off the store. The store belongs to the device session and outlives the editor,
        /// so this is what keeps a closed editor's strip from being re-read on the next preference
        /// write. Safe to call more than once.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _preferences.Changed -= _preferencesChangedHandler;
        }
    }
}
