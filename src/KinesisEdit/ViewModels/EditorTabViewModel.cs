using KinesisEdit.Core.Devices;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the editor's tab strip: its caption, whether it can be opened yet, and
    /// whether it is the open one. A tab whose device could never have content behind it is
    /// omitted; a tab that this app has not built yet is shown disabled rather than hidden, so
    /// the editor's shape does not change when it arrives.
    /// </summary>
    public sealed class EditorTabViewModel : ViewModelBase
    {
        /// <summary>Caption of the keyboard/remap tab.</summary>
        public const string KeysCaption = "Keys";

        /// <summary>Caption of the macro tab.</summary>
        public const string MacrosCaption = "Macros";

        /// <summary>Caption of the lighting tab.</summary>
        public const string LightingCaption = "Lighting";

        /// <summary>Caption of the settings tab.</summary>
        public const string SettingsCaption = "Settings";

        /// <summary>
        /// Builds the tab strip for <paramref name="device"/> in spec order, filtered by what the
        /// device can actually carry:
        /// <list type="bullet">
        /// <item>the Lighting tab is <b>omitted</b> for a device without lighting hardware
        /// (<see cref="LightingKind.None"/>: Freestyle Pro, Advantage2) — there is no lighting
        /// file to edit, so an empty panel would be a lie;</item>
        /// <item>the Settings tab is <b>omitted</b> for a device with no app-managed settings file
        /// (<c>SettingsCapability.None</c>: Savant Elite2, CROSSFIRE, Advantage 360 Professional);</item>
        /// <item>the Macros tab is always present and always enabled — the panel behind it reads
        /// the device's own macro capability and says so itself on a board that has none
        /// (<c>MacroPanelViewModel.NotSupportedMessage</c>), which is one place fewer for the two
        /// answers to disagree.</item>
        /// </list>
        /// <paramref name="isLightingEnabled"/> is the switch the lighting panel sets from
        /// <see cref="LightingTabViewModel.IsSupported"/>: a lit board whose led file this app
        /// cannot edit yet keeps a visible but disabled tab.
        /// </summary>
        public static IReadOnlyList<EditorTabViewModel> CreateAll(DeviceDefinition device, bool isLightingEnabled)
        {
            ArgumentNullException.ThrowIfNull(device);

            var tabs = new List<EditorTabViewModel>(4)
            {
                new(EditorTab.Keys, KeysCaption, isEnabled: true),
                new(EditorTab.Macros, MacrosCaption, isEnabled: true)
            };

            if (device.Lighting.Kind != LightingKind.None)
            {
                tabs.Add(new EditorTabViewModel(EditorTab.Lighting, LightingCaption, isLightingEnabled));
            }

            if (KeyboardSettingsViewModel.HasSettings(device))
            {
                tabs.Add(new EditorTabViewModel(EditorTab.Settings, SettingsCaption, isEnabled: true));
            }

            return tabs;
        }

        /// <summary>Which section this entry opens.</summary>
        public EditorTab Tab { get; }

        /// <summary>The tab's caption.</summary>
        public string Caption { get; }

        /// <summary>Whether the tab has content behind it yet.</summary>
        public bool IsEnabled { get; }

        /// <summary>Whether this is the open tab.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one tab entry.</summary>
        public EditorTabViewModel(EditorTab tab, string caption, bool isEnabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Tab = tab;
            Caption = caption;
            IsEnabled = isEnabled;
        }
    }
}
