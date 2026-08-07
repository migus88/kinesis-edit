using KinesisEdit.Core.Keys;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One filter chip of the token picker — <c>All · Letters · Nav · Media · Mouse · Hotkeys</c>
    /// plus <c>Recent</c> (mockups <c>1e</c>/<c>2a</c>), drawn on the <c>FilterChip</c> control
    /// theme.
    ///
    /// <para><b><c>Recent</c> is a chip, not a category.</b> The other six select a
    /// <see cref="KeySearchCategory"/>, which every catalog row carries; <c>Recent</c> filters by
    /// what this session has assigned (<see cref="RecentTokenStore"/>) and so cannot be one — which
    /// is why <see cref="IsRecent"/> exists rather than a seventh enum member that Core would have to
    /// know about.</para>
    ///
    /// <para><b>The narrow set is a responsive rule, not a second chip set.</b> Mockup <c>2a</c>
    /// draws the row "reduced at this width to All, Letters, Nav, Media, Recent" — the same chips,
    /// two of them dropped. <see cref="SurvivesNarrow"/> is which ones stay; the picker decides when,
    /// because only it knows how wide the rail is.</para>
    /// </summary>
    public sealed class TokenPickerCategoryViewModel : ViewModelBase
    {
        /// <summary>The neutral filter: every row of the dialect's catalog.</summary>
        public const string AllCaption = "All";

        /// <summary>§3.1 — letters and digits.</summary>
        public const string LettersCaption = "Letters";

        /// <summary>§3.3 — whitespace, editing and navigation.</summary>
        public const string NavCaption = "Nav";

        /// <summary>§3.7 — media and volume.</summary>
        public const string MediaCaption = "Media";

        /// <summary>§3.8 — mouse actions.</summary>
        public const string MouseCaption = "Mouse";

        /// <summary>§3.11 — profile selectors and the board's own hotkeys.</summary>
        public const string HotkeysCaption = "Hotkeys";

        /// <summary>What this session has already assigned.</summary>
        public const string RecentCaption = "Recent";

        /// <summary>What the chip row calls a <see cref="KeySearchCategory"/>.</summary>
        public static string CaptionFor(KeySearchCategory category)
        {
            return category switch
            {
                KeySearchCategory.All => AllCaption,
                KeySearchCategory.Letters => LettersCaption,
                KeySearchCategory.Nav => NavCaption,
                KeySearchCategory.Media => MediaCaption,
                KeySearchCategory.Mouse => MouseCaption,
                KeySearchCategory.Hotkeys => HotkeysCaption,
                _ => AllCaption
            };
        }

        /// <summary>Builds the chip for one category.</summary>
        public static TokenPickerCategoryViewModel For(KeySearchCategory category, bool survivesNarrow)
        {
            return new TokenPickerCategoryViewModel(category, isRecent: false, CaptionFor(category), survivesNarrow);
        }

        /// <summary>Builds the <c>Recent</c> chip, which survives the narrow reduction (mockup <c>2a</c>).</summary>
        public static TokenPickerCategoryViewModel Recent()
        {
            return new TokenPickerCategoryViewModel(
                KeySearchCategory.All,
                isRecent: true,
                RecentCaption,
                survivesNarrow: true);
        }

        /// <summary>
        /// The category this chip selects. Meaningless while <see cref="IsRecent"/> is true, where it
        /// is <see cref="KeySearchCategory.All"/> so a caller that reads it anyway gets "no category
        /// filter" rather than a wrong one.
        /// </summary>
        public KeySearchCategory Category { get; }

        /// <summary>Whether this is the <c>Recent</c> chip rather than a category one.</summary>
        public bool IsRecent { get; }

        /// <summary>The chip's label.</summary>
        public string Caption { get; }

        /// <summary>Whether the chip stays on screen when the rail is too narrow for all seven.</summary>
        public bool SurvivesNarrow { get; }

        /// <summary>Whether this chip is the live filter. Exactly one chip is, always.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// Whether the chip is drawn at the current width. Written by
        /// <see cref="TokenPickerViewModel"/> — a chip does not know how wide its row is.
        /// </summary>
        public bool IsShown
        {
            get => _isShown;
            set => SetProperty(ref _isShown, value);
        }

        private bool _isSelected;
        private bool _isShown = true;

        private TokenPickerCategoryViewModel(
            KeySearchCategory category,
            bool isRecent,
            string caption,
            bool survivesNarrow)
        {
            Category = category;
            IsRecent = isRecent;
            Caption = caption;
            SurvivesNarrow = survivesNarrow;
        }
    }
}
