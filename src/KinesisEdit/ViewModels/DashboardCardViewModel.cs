namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One card of the device dashboard, whatever kind it is (docs/design/mockups.md §1b). The
    /// dashboard's collection is typed to this base because two anatomies share the grid: the
    /// drive-backed <see cref="DeviceCardViewModel"/> and the
    /// <see cref="WebToolCardViewModel"/> for a board this app does not edit at all.
    /// <para>
    /// Only what every card has is declared here: the identity the dashboard reconciles by, the
    /// device name, and the one-line hardware summary under it. Everything else — status,
    /// actions, mount path — belongs to a specific card kind, because a card that cannot be
    /// scanned has no status to show and a card with no drive has nothing to eject.
    /// </para>
    /// </summary>
    public abstract class DashboardCardViewModel : ViewModelBase
    {
        /// <summary>
        /// Stable identity of this card within one session, unique across every card kind. Device
        /// cards derive it from their <c>ScannedDeviceId</c> — never the resolved device id, which
        /// Freestyle resolution makes many-to-one (docs/app/app-shell.md, invariant 4).
        /// </summary>
        public abstract string Key { get; }

        /// <summary>Device name shown as the card's title.</summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// The hardware summary under the title — "Split flat · 2 layers · per-key RGB". Always
        /// composed by <see cref="KinesisEdit.Core.Devices.DeviceMetaLine"/> from catalog data, so
        /// no card and no view ever carries per-device code.
        /// </summary>
        public abstract string MetaLine { get; }

        /// <summary>
        /// Whether this card has already been on screen once. The design animates a card in when
        /// its device is <em>newly detected</em>; a card view is also rebuilt from scratch every
        /// time the shell swaps the dashboard back in, and an existing card being re-hosted is not
        /// an insertion — without this the whole grid would grow in from zero on every trip Home.
        /// <para>
        /// A bool rather than anything from the toolkit: the card knows whether it has been shown,
        /// the view decides what to do about it (docs/app/app-shell.md, invariant 8).
        /// </para>
        /// </summary>
        public bool HasBeenPresented { get; private set; }

        /// <summary>Records that a view for this card has joined the visual tree.</summary>
        public void MarkPresented()
        {
            HasBeenPresented = true;
        }
    }
}
