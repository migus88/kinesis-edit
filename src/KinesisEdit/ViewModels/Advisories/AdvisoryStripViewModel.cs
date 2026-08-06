using CommunityToolkit.Mvvm.Input;

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
    /// </summary>
    public sealed class AdvisoryStripViewModel : ViewModelBase
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
        private EditorAdvisories _advisories = EditorAdvisories.Empty;
        private EditorTab _tab = EditorTab.Keys;
        private int? _layerIndex;
        private string _advisorySummary = string.Empty;
        private int _advisoryCount;

        /// <summary>Where <c>Review</c> has walked to; -1 until it is pressed, and again whenever the set moves.</summary>
        private int _reviewIndex = -1;

        /// <summary>
        /// Creates the strip. <paramref name="selectKey"/> puts the board's selection on the
        /// anchored cap and <paramref name="selectMacro"/> opens the anchored macro row; both are
        /// the editor's, because the board and the macro panel are.
        /// </summary>
        public AdvisoryStripViewModel(Action<AdvisoryAnchor> selectKey, Action<AdvisoryAnchor> selectMacro)
        {
            _selectKey = selectKey ?? throw new ArgumentNullException(nameof(selectKey));
            _selectMacro = selectMacro ?? throw new ArgumentNullException(nameof(selectMacro));

            ReviewAdvisoriesCommand = new RelayCommand(ReviewAdvisories);
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
    }
}
