namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the editor's tab strip: its caption, whether it can be opened yet, and
    /// whether it is the open one. The tabs that later issues fill in are shown disabled rather
    /// than hidden, so the editor's shape does not change when they arrive.
    /// </summary>
    public sealed class EditorTabViewModel : ViewModelBase
    {
        /// <summary>Caption of the keyboard/remap tab.</summary>
        public const string KeysCaption = "Keys";

        /// <summary>Caption of the macro tab.</summary>
        public const string MacrosCaption = "Macros";

        /// <summary>Caption of the lighting tab (issue #16).</summary>
        public const string LightingCaption = "Lighting";

        /// <summary>Caption of the settings tab (issue #16).</summary>
        public const string SettingsCaption = "Settings";

        /// <summary>
        /// Builds the tab strip in spec order. <see cref="EditorTab.Keys"/> and
        /// <see cref="EditorTab.Macros"/> are enabled; <see cref="EditorTab.Lighting"/> and
        /// <see cref="EditorTab.Settings"/> stay disabled because issue #16 has not filled them in
        /// yet — the lighting model is read only to paint the key caps' colour strip, and nothing
        /// edits the settings files. They are shown rather than hidden because a visibly
        /// unavailable tab beats one that silently shows nothing (the same rule the shell's
        /// Settings/Help buttons follow), and because the editor's shape must not change when they
        /// arrive.
        /// </summary>
        public static IReadOnlyList<EditorTabViewModel> CreateAll()
        {
            return
            [
                new EditorTabViewModel(EditorTab.Keys, KeysCaption, isEnabled: true),
                new EditorTabViewModel(EditorTab.Macros, MacrosCaption, isEnabled: true),
                new EditorTabViewModel(EditorTab.Lighting, LightingCaption, isEnabled: false),
                new EditorTabViewModel(EditorTab.Settings, SettingsCaption, isEnabled: false)
            ];
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
