namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One mode tab of the key inspector rail — <c>Remap</c>, <c>Tap &amp; hold</c>, <c>Macro</c>
    /// (mockups <c>1e</c>/<c>2a</c>, laid out as a 2×2 grid per docs/design/handoff.md:137).
    /// <para>
    /// <b>It carries an <see cref="IsEnabled"/>, and <see cref="EditorTabViewModel"/> deliberately
    /// does not.</b> The section strip obeys the capability law outright — a section this app
    /// cannot open is not rendered — but the inspector has the design's own stated exception to it:
    /// a <b>locked position</b>, whose tabs "are individually disabled with reasons" (mockup
    /// <c>2h</c>, and docs/design/README.md's own carve-out). A dead tab with no reason beside it
    /// would be exactly the greyed-out promise the law forbids, so <see cref="DisabledReason"/> is
    /// never empty while <see cref="IsEnabled"/> is false, and <see cref="DisplayCaption"/> is what
    /// the tab actually reads.
    /// </para>
    /// <para>
    /// The absent tab is still absent: <see cref="KeyInspectorMode.MultiModifier"/> is not built at
    /// all on a board whose firmware lacks it (<c>KeyboardKey.SupportsMultiModifiers</c>). Disabled
    /// is for a position that <em>this device</em> can normally do and <em>this key</em> cannot.
    /// </para>
    /// </summary>
    public sealed class KeyInspectorTabViewModel : ViewModelBase
    {
        /// <summary>Caption of the remap tab.</summary>
        public const string RemapCaption = "Remap";

        /// <summary>Caption of the tap-and-hold tab. The ampersand is the mockups' own.</summary>
        public const string TapAndHoldCaption = "Tap & hold";

        /// <summary>Caption of the macro tab — a bridge to the Macros tab, not a panel (issue #93).</summary>
        public const string MacroCaption = "Macro";

        /// <summary>Caption of the multi-modifier tab, drawn only where the firmware has it.</summary>
        public const string MultiModifierCaption = "Multi-mod";

        /// <summary>
        /// Why every tab of a locked position is dead, verbatim from mockup <c>2h</c>
        /// ("Remap — not writable"). The em dash and the spacing are the mockup's.
        /// </summary>
        public const string NotWritableReason = "not writable";

        /// <summary>What joins a caption to its reason: <c>Remap — not writable</c>.</summary>
        public const string ReasonSeparator = " — ";

        /// <summary>The mockups' caption for one mode.</summary>
        public static string CaptionFor(KeyInspectorMode mode)
        {
            return mode switch
            {
                KeyInspectorMode.Remap => RemapCaption,
                KeyInspectorMode.TapAndHold => TapAndHoldCaption,
                KeyInspectorMode.Macro => MacroCaption,
                KeyInspectorMode.MultiModifier => MultiModifierCaption,
                _ => string.Empty
            };
        }

        /// <summary>A live tab.</summary>
        public static KeyInspectorTabViewModel Enabled(KeyInspectorMode mode)
        {
            return new KeyInspectorTabViewModel(mode, isEnabled: true, string.Empty);
        }

        /// <summary>
        /// A tab that is drawn dead <b>with its reason</b>. <paramref name="reason"/> is required:
        /// see the type's remarks.
        /// </summary>
        public static KeyInspectorTabViewModel Disabled(KeyInspectorMode mode, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);

            return new KeyInspectorTabViewModel(mode, isEnabled: false, reason);
        }

        /// <summary>The slot this tab selects.</summary>
        public KeyInspectorMode Mode { get; }

        /// <summary>The mode's own caption, without any reason attached.</summary>
        public string Caption { get; }

        /// <summary>Whether the tab can be chosen at all.</summary>
        public bool IsEnabled { get; }

        /// <summary>Why it cannot be, or an empty string while it can.</summary>
        public string DisabledReason { get; }

        /// <summary>
        /// What the tab reads: the caption alone while it is live, the caption and its reason while
        /// it is not — <c>Remap — not writable</c>.
        /// </summary>
        public string DisplayCaption { get; }

        /// <summary>Whether this is the mode the rail is showing.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        private KeyInspectorTabViewModel(KeyInspectorMode mode, bool isEnabled, string reason)
        {
            Mode = mode;
            Caption = CaptionFor(mode);
            IsEnabled = isEnabled;
            DisabledReason = reason;
            DisplayCaption = isEnabled ? Caption : Caption + ReasonSeparator + reason;
        }
    }
}
