namespace KinesisEdit.Services
{
    /// <summary>
    /// <b>Everything the app remembers about this machine's user</b> — theme, motion, the shell
    /// window's last geometry and how wide they dragged the editor's key inspector rail. Held by
    /// <see cref="IHostPreferencesStore"/> and persisted to a JSON file in the per-user
    /// configuration directory.
    /// <para>
    /// <b>This is not <c>app_settings.txt</c>.</b> That file lives on a keyboard's v-Drive, is
    /// per-device, travels with the board, and is reached through <see cref="IAppPreferencesStore"/>
    /// — its own screen section even says "stored per device". Nothing here is about any keyboard,
    /// which is exactly why it cannot live there: a preference stored on a drive is unreadable with
    /// no drive attached, and the window has to open before any drive is scanned. See
    /// docs/app/host-preferences.md.
    /// </para>
    /// <para>
    /// A record, and deliberately toolkit-free: only <see cref="ThemeApplier"/> and
    /// <see cref="MotionPreferenceApplier"/> — the two bridging services — turn these values into
    /// Avalonia state (app-shell.md invariant 8).
    /// </para>
    /// </summary>
    public sealed record HostPreferences
    {
        /// <summary>
        /// The key inspector rail's width when nothing is stored — the <c>WidthInspectorRail</c> of
        /// docs/design/handoff.md § Geometry, which is what the rail was before it could be dragged.
        /// </summary>
        public const double DefaultInspectorRailWidth = 268;

        /// <summary>The narrowest the rail may be dragged (<c>WidthInspectorRailMin</c>).</summary>
        public const double MinimumInspectorRailWidth = 240;

        /// <summary>The widest the rail may be dragged (<c>WidthInspectorRailMax</c>).</summary>
        public const double MaximumInspectorRailWidth = 520;

        /// <summary>
        /// A fresh install: follow the OS for both theme and motion, remember no window, and leave
        /// the inspector rail at its authored width. Every tolerant-read failure resolves to this —
        /// an absent file, an unparseable one, a file full of keys or types this app does not model.
        /// </summary>
        public static HostPreferences Default { get; } = new();

        /// <summary>
        /// <paramref name="width"/> brought inside the rail's band. A width that is not a number at
        /// all — a splitter reporting NaN, a hand-built record — is not a preference and yields
        /// <see cref="DefaultInspectorRailWidth"/>; <c>Math.Clamp</c> alone would propagate the NaN.
        /// <para>
        /// <b>The band lives here and nowhere else.</b> The same three numbers are geometry tokens
        /// (<c>WidthInspectorRailMin</c> / <c>WidthInspectorRail</c> / <c>WidthInspectorRailMax</c>)
        /// so the splitter can bound the drag, but a view model may not read Avalonia resources
        /// (app-shell.md invariant 8) — so they are written twice on purpose, this copy is the one
        /// the model is clamped by, and a test pins the two copies to each other.
        /// </para>
        /// </summary>
        public static double ClampInspectorRailWidth(double width)
        {
            if (!double.IsFinite(width))
            {
                return DefaultInspectorRailWidth;
            }

            return Math.Clamp(width, MinimumInspectorRailWidth, MaximumInspectorRailWidth);
        }

        /// <summary>Which light/dark face the app wears. Defaults to <see cref="AppThemePreference.FollowSystem"/>.</summary>
        public AppThemePreference Theme { get; init; } = AppThemePreference.FollowSystem;

        /// <summary>Which motion budget the app animates with. Defaults to <see cref="MotionPreference.FollowSystem"/>.</summary>
        public MotionPreference Motion { get; init; } = MotionPreference.FollowSystem;

        /// <summary>
        /// The shell window's last size and position, or <c>null</c> when nothing usable is
        /// stored — a fresh install, or a stored geometry that did not survive
        /// <see cref="WindowGeometry.TryCreate"/>. Null means "let the window pick its own size".
        /// </summary>
        public WindowGeometry? Window { get; init; }

        /// <summary>
        /// How wide the user dragged the keyboard editor's key inspector rail, or <c>null</c> when
        /// nothing is stored — a fresh install, or a stored value the tolerant read refused. Null
        /// means <see cref="DefaultInspectorRailWidth"/>, which is what the rail was before it could
        /// be dragged at all.
        /// <para>
        /// A host preference and not a device one, deliberately: it is a fact about this person's
        /// screen and how they like their editor, not about any keyboard, and it must be right on
        /// the first frame of an editor opened over a board that has never been plugged in here.
        /// </para>
        /// </summary>
        public double? InspectorRailWidth { get; init; }
    }
}
