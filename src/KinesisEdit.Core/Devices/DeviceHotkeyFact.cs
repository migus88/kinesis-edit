namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// One thing a board's configuration key does when it is held — a line of the locked-key
    /// panel's "What it does on the board" section (mockup <c>2h</c>). See
    /// <see cref="DeviceHotkeyCatalog"/> for where the facts come from and why they are data.
    /// <para>
    /// <see cref="Source"/> is carried on the fact itself rather than written once at the top of
    /// the catalog, because the three kinds of source are genuinely different — two are spec
    /// citations and one is the board's own printed silkscreen, which specs/ does not document.
    /// A fact that could not name where it came from would be a fact this app made up.
    /// </para>
    /// </summary>
    /// <param name="Keys">What is held with the configuration key — <c>F8</c>, <c>1…9</c>.</param>
    /// <param name="Effect">What that does, in the app's own voice — <c>mount the v-Drive</c>.</param>
    /// <param name="Source">Where the fact comes from; never empty.</param>
    public sealed record DeviceHotkeyFact(string Keys, string Effect, string Source)
    {
        /// <summary>Where a fact that is printed on the board rather than written in specs/ comes from.</summary>
        public const string SilkscreenSource = "authored board silkscreen (Geometry/Visual/*Visual.cs), not spec text";

        /// <summary>A fact quoted from specs/.</summary>
        public static DeviceHotkeyFact Spec(string keys, string effect, string source)
        {
            return new DeviceHotkeyFact(keys, effect, source);
        }

        /// <summary>A fact read off the board's printed legends — see <see cref="SilkscreenSource"/>.</summary>
        public static DeviceHotkeyFact Silkscreen(string keys, string effect)
        {
            return new DeviceHotkeyFact(keys, effect, SilkscreenSource);
        }

        /// <summary>
        /// The whole line as the panel reads it: <c>hold + F8 mount the v-Drive</c>. Sans, not
        /// mono — a key on the board is not a value in a config file.
        /// </summary>
        public string Text => DeviceHotkeyCatalog.HoldPrefix + Keys + " " + Effect;
    }
}
