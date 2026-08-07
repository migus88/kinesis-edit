using KinesisEdit.Core.Devices;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the editor's tab strip: its caption and whether it is the open one.
    /// <para>
    /// <b>There is no <c>IsEnabled</c>.</b> The design's capability law is "absent features are not
    /// shown, not disabled" (docs/design/README.md), so a section this app cannot open is not
    /// rendered at all — a greyed-out tab promises a screen and refuses it in the same gesture.
    /// The strip therefore carries only tabs that work, and <see cref="CreateAll"/> is the single
    /// place that decides which those are.
    /// </para>
    /// </summary>
    public sealed class EditorTabViewModel : ViewModelBase
    {
        /// <summary>
        /// Caption of the keyboard/remap tab. The <see cref="EditorTab.Keys"/> enum member keeps
        /// its name deliberately: it is carried inside <c>EnumMatch</c> converter-parameter
        /// <em>strings</em> in XAML (<c>ConverterParameter='Keys,Macros'</c>), so renaming it is a
        /// string-coupled change with no user-visible payoff. Only the caption is the mockups'.
        /// </summary>
        public const string LayoutCaption = "Layout";

        /// <summary>Caption of the macro tab.</summary>
        public const string MacrosCaption = "Macros";

        /// <summary>Caption of the lighting tab.</summary>
        public const string LightingCaption = "Lighting";

        /// <summary>Caption of the settings tab.</summary>
        public const string SettingsCaption = "Settings";

        /// <summary>
        /// Builds the tab strip for <paramref name="device"/> in spec order, filtered by what the
        /// device can actually carry — every entry it returns opens a working section:
        /// <list type="bullet">
        /// <item>the Lighting tab is <b>omitted</b> unless the board has lighting hardware
        /// (<see cref="LightingKind.None"/>: Freestyle Pro, Advantage2) <b>and</b>
        /// <paramref name="isLightingSupported"/> — the switch the lighting panel sets from
        /// <see cref="LightingTabViewModel.IsSupported"/>. A TKO or Advantage 360 therefore renders
        /// no Lighting tab at all until its own editor lands (#40/#41), rather than a dark one;</item>
        /// <item>the Settings tab is <b>omitted</b> for a device with no app-managed settings file
        /// (<c>SettingsCapability.None</c>: Savant Elite2, CROSSFIRE, Advantage 360 Professional);</item>
        /// <item>the Macros tab is always present — the panel behind it reads the device's own
        /// macro capability and says so itself on a board that has none
        /// (<c>MacroLibraryViewModel.NotSupportedMessage</c>), which is one place fewer for the two
        /// answers to disagree.</item>
        /// </list>
        /// The lighting question is device-level on purpose: this runs before any profile has been
        /// read, and demo mode never reads one at all.
        /// </summary>
        public static IReadOnlyList<EditorTabViewModel> CreateAll(DeviceDefinition device, bool isLightingSupported)
        {
            ArgumentNullException.ThrowIfNull(device);

            var tabs = new List<EditorTabViewModel>(4)
            {
                new(EditorTab.Keys, LayoutCaption),
                new(EditorTab.Macros, MacrosCaption)
            };

            if (device.Lighting.Kind != LightingKind.None && isLightingSupported)
            {
                tabs.Add(new EditorTabViewModel(EditorTab.Lighting, LightingCaption));
            }

            if (KeyboardSettingsViewModel.HasSettings(device))
            {
                tabs.Add(new EditorTabViewModel(EditorTab.Settings, SettingsCaption));
            }

            return tabs;
        }

        /// <summary>Which section this entry opens.</summary>
        public EditorTab Tab { get; }

        /// <summary>The tab's caption.</summary>
        public string Caption { get; }

        /// <summary>Whether this is the open tab.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one tab entry.</summary>
        public EditorTabViewModel(EditorTab tab, string caption)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Tab = tab;
            Caption = caption;
        }
    }
}
