using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// What the key inspector shows instead of its mode panels when the selected position can never
    /// be remapped (specs/05-key-model.md §5.3 — the Advantage2 <c>Keypad</c> and <c>Program</c>
    /// buttons, the <c>ss</c> SmartSet key on the TKO and the Advantage 360). Mockup <c>2h</c>'s
    /// "Locked key panel".
    ///
    /// <para><b>This is the design's own exception to the capability law.</b> Everywhere else in
    /// the app "absent features are not shown, not disabled" (docs/design/README.md) — and here the
    /// three mode tabs are drawn <b>dead, each with its reason</b>. The difference is that the user
    /// pointed at this key: a panel that answered by rendering nothing would look broken, where a
    /// dead tab that says <i>not writable</i> answers the question that was asked. The reason is
    /// what makes it legal, which is why <see cref="KeyInspectorTabViewModel.Disabled"/> refuses to
    /// build a dead tab without one.</para>
    ///
    /// <para><b>The copy asymmetry is the panel's other job.</b> A locked position can be copied
    /// <em>from</em> — its assignment is perfectly readable — and can never be copied
    /// <em>onto</em>, because <c>KeyboardKey.CopyFrom</c> would drop the key-data half and still
    /// carry the macros. So one direction is disabled and the action as a whole is not.</para>
    ///
    /// <para><b>Unreachable on the only board with a picture, deliberately.</b> <c>Locked()</c>
    /// appears in the Advantage2, TKO and Advantage 360 geometries; the Freestyle Edge RGB's index
    /// 0 is <c>hk0</c>, an ordinary remappable hotkey. The panel is built and tested against
    /// <c>TestLayouts.CreateLockedKeyLayout()</c> and becomes reachable with issues #40/#41 — the
    /// same precedent as the read-only Settings screen.</para>
    /// </summary>
    public sealed class LockedKeyPanelViewModel : ViewModelBase
    {
        /// <summary>The panel's state line, verbatim from mockup <c>2h</c>.</summary>
        public const string StateLine = "Locked position";

        /// <summary>
        /// Why nothing here can be written, verbatim from mockup <c>2h</c>. The last clause is the
        /// load-bearing one: this is not a device-wide refusal, it is one position.
        /// </summary>
        public const string Explanation =
            "This key is the board's own configuration key. Its behaviour lives in firmware, not in "
            + "the layout files, so nothing here can be written — including on a device where every "
            + "other position is free.";

        /// <summary>
        /// The informational section's title. Mockup <c>2h</c> writes it as "What it does on the
        /// board" under a CSS <c>text-transform: uppercase</c>; Avalonia has none, so a section
        /// label is authored uppercase at the call site (docs/app/design-system.md), exactly as
        /// <see cref="BoardLegendViewModel.LegendLabel"/> is.
        /// </summary>
        public const string HotkeySectionTitle = "WHAT IT DOES ON THE BOARD";

        /// <summary>
        /// Copying somebody else's assignment <b>onto</b> this position — the direction that is
        /// refused. Mockup <c>2h</c>'s caption.
        /// </summary>
        public const string CopyFromCaption = "Copy from…";

        /// <summary>
        /// Copying <b>this</b> position's assignment onto another — the direction that works, and
        /// the same armed pick the legend row's <c>Copy key…</c> starts.
        /// </summary>
        public const string CopyToCaption = "Copy to…";

        /// <summary>
        /// Why <see cref="CopyFromCaption"/> is dead, in the app's own voice. Mockup <c>2h</c>
        /// states the rule as "A locked key can still be a copy source, never a target — so the
        /// inspector disables one direction, not the whole action"; the second half of that
        /// sentence describes what this panel <em>does</em> rather than what it <em>says</em>, so
        /// only the rule itself is shown.
        /// <para>
        /// <b>Which of the two captions is the live one is a deliberate departure from the mock's
        /// drawing.</b> <c>2h</c> draws <c>Copy from…</c> live and <c>Copy to…</c> dead, which
        /// contradicts both its own rule sentence and mockup <c>1e</c>, where the inspector
        /// footer's <c>Copy to…</c> is the action that copies <em>this</em> key onto another. Read
        /// consistently with <c>1e</c> and with the rule, the live direction is the one where the
        /// locked key is the <b>source</b> — <c>Copy to…</c> — and the refused one is where it
        /// would be the target. Issue #92's mechanical instruction says the same thing: relax
        /// <c>CanCopyKey()</c>, whose source is the selected key, and leave the existing
        /// <c>!target.CanEdit</c> refusal alone.
        /// </para>
        /// </summary>
        public const string CopyDirectionReason = "A locked key can still be a copy source, never a target.";

        /// <summary>
        /// The three dead tabs, in the mockups' order. Built once: which modes a locked position
        /// refuses does not depend on which locked position it is.
        /// </summary>
        public IReadOnlyList<KeyInspectorTabViewModel> Tabs { get; }

        /// <summary>
        /// What the board's configuration key does when it is held
        /// (<see cref="DeviceHotkeyCatalog"/>), or an empty list for a device this app has no facts
        /// about — in which case the section is not drawn rather than drawn empty.
        /// </summary>
        public IReadOnlyList<DeviceHotkeyFact> Hotkeys
        {
            get => _hotkeys;
            private set
            {
                if (SetProperty(ref _hotkeys, value))
                {
                    OnPropertyChanged(nameof(HasHotkeys));
                }
            }
        }

        /// <summary>Whether there is anything to say under <see cref="HotkeySectionTitle"/>.</summary>
        public bool HasHotkeys => _hotkeys.Count > 0;

        /// <summary>
        /// Arms a copy <b>from</b> this position (the editor's own <c>CopyKeyCommand</c>, the same
        /// one the legend row runs). Nothing is re-implemented here — #91 already built the
        /// arm-then-click-a-target interaction, and a second copy path would be a second set of
        /// refusals to keep in step.
        /// </summary>
        public IRelayCommand CopyToCommand { get; }

        private IReadOnlyList<DeviceHotkeyFact> _hotkeys = [];

        /// <summary>Builds the panel over the editor's copy command.</summary>
        public LockedKeyPanelViewModel(IRelayCommand copyKeyCommand)
        {
            CopyToCommand = copyKeyCommand ?? throw new ArgumentNullException(nameof(copyKeyCommand));

            Tabs =
            [
                KeyInspectorTabViewModel.Disabled(KeyInspectorMode.Remap, KeyInspectorTabViewModel.NotWritableReason),
                KeyInspectorTabViewModel.Disabled(KeyInspectorMode.TapAndHold, KeyInspectorTabViewModel.NotWritableReason),
                KeyInspectorTabViewModel.Disabled(KeyInspectorMode.Macro, KeyInspectorTabViewModel.NotWritableReason)
            ];
        }

        /// <summary>
        /// Re-reads the facts for whichever board is open. A null layout — nothing loaded, or demo
        /// mode before a profile exists — leaves the section empty rather than guessing a device.
        /// </summary>
        public void Refresh(KeyboardLayout? layout)
        {
            Hotkeys = layout is null ? [] : DeviceHotkeyCatalog.ForDevice(layout.Device.Id);
        }
    }
}
