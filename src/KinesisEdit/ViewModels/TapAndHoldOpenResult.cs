namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What <see cref="TapAndHoldOverlayViewModel.TryCreate"/> answers: either the overlay to
    /// show, or the verbatim refusal of the four pre-dialog checks of
    /// specs/11-feature-dialogs.md §11.1. Exactly one of the two is set, so the caller shows the
    /// message box when <see cref="Overlay"/> is null and never has to run the checks itself.
    /// </summary>
    public sealed record TapAndHoldOpenResult
    {
        /// <summary>The result of a check that passed, carrying the overlay to show.</summary>
        public static TapAndHoldOpenResult Allowed(TapAndHoldOverlayViewModel overlay)
        {
            ArgumentNullException.ThrowIfNull(overlay);

            return new TapAndHoldOpenResult { Overlay = overlay };
        }

        /// <summary>The result of a check that refused, carrying §11.1's wording for it.</summary>
        public static TapAndHoldOpenResult Refused(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            return new TapAndHoldOpenResult { RefusalMessage = message };
        }

        /// <summary>The overlay to show, or null when a pre-dialog check refused.</summary>
        public TapAndHoldOverlayViewModel? Overlay { get; private init; }

        /// <summary>The refusal message to show, or null when the overlay may open.</summary>
        public string? RefusalMessage { get; private init; }

        /// <summary>Whether the overlay may open.</summary>
        public bool IsAllowed => Overlay is not null;

        private TapAndHoldOpenResult()
        {
        }
    }
}
