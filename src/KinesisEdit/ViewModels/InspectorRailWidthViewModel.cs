using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// How wide the editor's right-hand rail is drawn, and everything that follows from that: the
    /// clamp into the band, the macro floor, and the per-user persistence.
    /// <para>
    /// <b>It is one object because there is one width.</b> Issue #124 gave the Lighting tab's mode
    /// rail the Keys tab's drag seam, and the user's answer to "how wide should the rail be" is one
    /// answer for the editor rather than one per tab — a width dragged on either tab is the width
    /// the other opens at, and it is persisted under the one existing <c>inspectorRailWidth</c> key
    /// (docs/app/host-preferences.md). This state used to sit on
    /// <see cref="KeyboardEditorViewModel"/>, which is where the second tab could not reach it
    /// without either a second stored value or a back-reference to the editor.
    /// </para>
    /// <para>
    /// <b>The two tabs read different properties of it, deliberately.</b> The Keys tab binds
    /// <see cref="EffectiveWidth"/>, because its macro panel is entitled to
    /// <see cref="MacroRailWidth"/>; the Lighting tab binds <see cref="Width"/>, because it has no
    /// macro panel and a floor that leaked onto it would widen a rail for a reason that does not
    /// exist there.
    /// </para>
    /// </summary>
    public sealed class InspectorRailWidthViewModel : ViewModelBase
    {
        /// <summary>
        /// The rail width macro editing is entitled to (<c>WidthInspectorRailWide</c>). A
        /// <b>floor</b> on <see cref="EffectiveWidth"/>, never a replacement — see there.
        /// <para>
        /// <b>440, not the handoff's 300</b> (docs/design/handoff.md § Geometry says 300 "on the
        /// macro-editing variant"), and the deviation is issue #146's: the redesigned Macro panel
        /// draws a step row and the composer's first row as single lines, and neither fits in 300.
        /// Recorded in docs/app/design-system.md's deviation list.
        /// </para>
        /// <para>
        /// Written as a plain number rather than read out of <c>Themes/Geometry.axaml</c>: a view
        /// model may not depend on Avalonia resources (app-shell.md invariant 8), and the token and
        /// this constant are pinned to each other by a test.
        /// </para>
        /// </summary>
        public const double MacroRailWidth = 440;

        /// <summary>
        /// How wide the user has dragged the rail, in DIPs — clamped into
        /// <see cref="HostPreferences.MinimumInspectorRailWidth"/>…<see cref="HostPreferences.MaximumInspectorRailWidth"/>
        /// on the way in and <b>persisted per user</b> (docs/app/host-preferences.md), so the width
        /// survives a restart and follows the person rather than the board.
        /// <para>
        /// <b>Setting the same width again costs nothing</b> — no notification, no write. The store
        /// persists synchronously on every real change by design, and a splitter drag reports
        /// continuously, so the no-op guard is what turns a drag into one write at its commit rather
        /// than one per pointer move. Nothing here is debounced.
        /// </para>
        /// <para>
        /// This is the user's number; <see cref="EffectiveWidth"/> is the number the Keys tab's rail
        /// is actually drawn at. The Lighting tab's rail is drawn at <i>this</i> one.
        /// </para>
        /// </summary>
        public double Width
        {
            get => _width;
            set => SetWidth(value);
        }

        /// <summary>
        /// Whether the showing panel wants the wide rail — <c>KeyInspectorViewModel.IsWide</c>,
        /// pushed in by the editor. It is an input to <see cref="EffectiveWidth"/> and nothing else;
        /// this object never asks the rail anything, so a tab with no inspector simply leaves it
        /// false.
        /// </summary>
        public bool IsWide
        {
            get => _isWide;
            set
            {
                if (SetProperty(ref _isWide, value))
                {
                    OnPropertyChanged(nameof(EffectiveWidth));
                }
            }
        }

        /// <summary>
        /// The width the Keys tab's rail is drawn at: the user's, or <b>at least</b>
        /// <see cref="MacroRailWidth"/> while the Macro panel is showing.
        /// <para>
        /// A <b>floor, not an override</b> — the deliberate deviation of issue #119. The macro width
        /// was a style setter that replaced the rail's width outright, which would have yanked a user
        /// who dragged to 500 back to the macro width the moment they opened a macro. Taking the
        /// maximum honours the entitlement without undoing a drag.
        /// </para>
        /// </summary>
        public double EffectiveWidth => _isWide ? Math.Max(_width, MacroRailWidth) : _width;

        private readonly IHostPreferencesStore? _hostPreferences;
        private double _width = HostPreferences.DefaultInspectorRailWidth;
        private bool _isWide;

        /// <summary>
        /// Seeds the width from <paramref name="hostPreferences"/> and keeps the store to write
        /// through.
        /// <para>
        /// <b>Read once, here, rather than followed:</b> this object is the only thing that writes
        /// the rail's width, so a <c>Changed</c> subscription could only ever tell it what it just
        /// did. The clamp is applied on the way in as well as on the way out, because a store handed
        /// a hand-built record has never been through the tolerant read at all.
        /// </para>
        /// <para>
        /// The store is optional for the same reason the editor's session is — a rail built for a
        /// unit test or a design scene has no app around it — and with none, the rail sits at its
        /// authored width and a drag is forgotten when the editor closes.
        /// </para>
        /// </summary>
        public InspectorRailWidthViewModel(IHostPreferencesStore? hostPreferences = null)
        {
            _hostPreferences = hostPreferences;

            if (hostPreferences?.Current.InspectorRailWidth is { } storedWidth)
            {
                _width = HostPreferences.ClampInspectorRailWidth(storedWidth);
            }
        }

        /// <summary>
        /// Takes a width from the splitter, clamps it into the band and persists it. The write goes
        /// through <see cref="IHostPreferencesStore.Update"/>'s mutation function rather than a
        /// whole record, so a theme or a window geometry written by another consumer a moment
        /// earlier cannot be clobbered by this one (docs/app/host-preferences.md).
        /// <para>
        /// <b>An unchanged width returns before it writes.</b> That is the whole of the "do not
        /// debounce" rule: the store persists synchronously on every real change, so what turns a
        /// continuous drag into a single write at its commit is that every report of the width it
        /// already has costs nothing. A width the clamp <em>moved</em> is still announced even when
        /// the field did not change, or the column would keep drawing a width the model refused.
        /// </para>
        /// </summary>
        private void SetWidth(double width)
        {
            var clamped = HostPreferences.ClampInspectorRailWidth(width);

            if (!SetProperty(ref _width, clamped, nameof(Width)))
            {
                if (clamped != width)
                {
                    OnPropertyChanged(nameof(Width));
                }

                return;
            }

            OnPropertyChanged(nameof(EffectiveWidth));

            _hostPreferences?.Update(preferences => preferences with { InspectorRailWidth = clamped });
        }
    }
}
