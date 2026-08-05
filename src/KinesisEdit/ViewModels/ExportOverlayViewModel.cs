using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Export files overlay of specs/11-feature-dialogs.md §11.5: three mutually exclusive
    /// choices, a native directory picker, and the serialized layout and/or led file written into
    /// the chosen directory under their current base names.
    /// <para>
    /// What is exported is planned by Core (<see cref="ProfileExportPlanner"/>, reached through
    /// <see cref="IProfileSession.PlanExport"/>); the bytes are written by
    /// <see cref="IVDriveFileService"/>, the same Latin1, whole-file writer the v-Drive uses
    /// (specs/03-vdrive-and-files.md §5.2), so an exported file is byte-identical to what the
    /// drive would hold. This view model owns the exclusivity rule, the picker call, and §11.5's
    /// three messages.
    /// </para>
    /// </summary>
    public sealed class ExportOverlayViewModel : EditorOverlayViewModel
    {
        /// <summary>Dialog title (§11.5), and the title of the directory picker it opens.</summary>
        public const string OverlayTitle = "Export files";

        /// <summary>Caption of the default checkbox (§11.5).</summary>
        public const string LayoutAndLightingCaption = "Layout and Lighting";

        /// <summary>Caption of the layout-only checkbox (§11.5).</summary>
        public const string LayoutOnlyCaption = "Layout only";

        /// <summary>Caption of the lighting-only checkbox (§11.5).</summary>
        public const string LightingOnlyCaption = "Lighting only";

        /// <summary>Prefix of the layout-failure message; the error follows it (§11.5).</summary>
        public const string LayoutFailurePrefix = "Error exporting layout file: ";

        /// <summary>Prefix of the lighting-failure message; the error follows it (§11.5).</summary>
        public const string LightingFailurePrefix = "Error exporting lighting file: ";

        /// <summary>Shown when every planned file was written (§11.5).</summary>
        public const string SuccessMessage = "Files exported successfully!";

        /// <summary>The current choice; assigning it is what the three checkboxes do.</summary>
        public ProfileExportSelection Selection
        {
            get => _selection;
            set
            {
                if (SetProperty(ref _selection, value))
                {
                    OnPropertyChanged(nameof(IsLayoutAndLightingSelected));
                    OnPropertyChanged(nameof(IsLayoutOnlySelected));
                    OnPropertyChanged(nameof(IsLightingOnlySelected));
                }
            }
        }

        /// <summary>
        /// The <c>'Layout and Lighting'</c> checkbox, checked by default (§11.5). The three are
        /// mutually exclusive: checking one un-checks the others, and un-checking the checked one
        /// is ignored, so exactly one choice is always live.
        /// </summary>
        public bool IsLayoutAndLightingSelected
        {
            get => Selection == ProfileExportSelection.LayoutAndLighting;
            set => Select(value, ProfileExportSelection.LayoutAndLighting);
        }

        /// <summary>The <c>'Layout only'</c> checkbox (§11.5).</summary>
        public bool IsLayoutOnlySelected
        {
            get => Selection == ProfileExportSelection.LayoutOnly;
            set => Select(value, ProfileExportSelection.LayoutOnly);
        }

        /// <summary>The <c>'Lighting only'</c> checkbox (§11.5).</summary>
        public bool IsLightingOnlySelected
        {
            get => Selection == ProfileExportSelection.LightingOnly;
            set => Select(value, ProfileExportSelection.LightingOnly);
        }

        /// <summary>
        /// Whether there is anything to export. False in demo mode: no profile was ever read from
        /// a drive there, so no session exists to serialize (docs/app/app-shell.md, spec 03 §3.5).
        /// </summary>
        public bool CanExport => _session is not null;

        /// <summary>
        /// The overlay's Ok button. It replaces the base <see cref="EditorOverlayViewModel.AcceptCommand"/>
        /// because §11.5's accept is asynchronous — it opens the directory picker — while
        /// <see cref="TryAccept"/> is not; the base command stays inert until this one has
        /// actually written the files.
        /// </summary>
        public IAsyncRelayCommand ExportCommand { get; }

        private readonly IProfileSession? _session;
        private readonly IFolderPickerService _folderPicker;
        private readonly IVDriveFileService _files;
        private readonly INotificationService _notifications;

        private ProfileExportSelection _selection = ProfileExportSelection.LayoutAndLighting;
        private bool _exported;

        /// <summary>
        /// Creates the overlay over <paramref name="session"/>; pass null for a demo-mode editor,
        /// which disables the export.
        /// </summary>
        public ExportOverlayViewModel(
            IProfileSession? session,
            IFolderPickerService folderPicker,
            IVDriveFileService files,
            INotificationService notifications)
            : base(OverlayTitle)
        {
            _session = session;
            _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));

            ExportCommand = new AsyncRelayCommand(ExportAsync, () => CanExport && !IsClosed);
            Closed += OnClosed;
        }

        /// <summary>
        /// Only ever true once <see cref="ExportAsync"/> has written every planned file: the
        /// overlay closes on a completed export and stays open for anything else, including a
        /// cancelled picker, which §11.5 treats as no outcome at all.
        /// </summary>
        protected override bool TryAccept()
        {
            return _exported;
        }

        private async Task ExportAsync()
        {
            if (IsClosed || _session is null)
            {
                return;
            }

            ErrorMessage = null;

            var folder = await PickFolderAsync();

            if (folder is null)
            {
                return;
            }

            var plan = _session.PlanExport(Selection);

            if (!TryWrite(plan, folder, out var failureMessage))
            {
                await ShowAsync(failureMessage, MessageBoxIcon.Error);

                return;
            }

            _exported = true;

            Accept();

            await ShowAsync(SuccessMessage, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Opens the picker, treating a platform failure exactly as
        /// <see cref="IFolderPickerService"/> documents a missing window: no directory, and no
        /// message — §11.5 gives wording for the two file writes only.
        /// </summary>
        private async Task<string?> PickFolderAsync()
        {
            try
            {
                return await _folderPicker.PickFolderAsync(OverlayTitle);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private bool TryWrite(IReadOnlyList<ExportFile> plan, string folder, out string failureMessage)
        {
            for (var index = 0; index < plan.Count; index++)
            {
                try
                {
                    _files.WriteAllLines(Path.Combine(folder, plan[index].FileName), plan[index].Lines, allowCreate: true);
                }
                catch (Exception exception)
                {
                    failureMessage = DescribeFailure(index) + exception.Message;

                    return false;
                }
            }

            failureMessage = string.Empty;

            return true;
        }

        /// <summary>
        /// Which of §11.5's two failure messages a planned file gets.
        /// <see cref="ProfileExportPlanner"/> plans the layout first and the led file second, so
        /// the position alone identifies the file — the base names themselves are Core-internal.
        /// </summary>
        private string DescribeFailure(int index)
        {
            var isLayout = Selection != ProfileExportSelection.LightingOnly && index == 0;

            return isLayout ? LayoutFailurePrefix : LightingFailurePrefix;
        }

        private Task ShowAsync(string message, MessageBoxIcon icon)
        {
            return _notifications.ShowMessageBoxAsync(new MessageBoxRequest
            {
                Title = OverlayTitle,
                Message = message,
                Icon = icon
            });
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            ExportCommand.NotifyCanExecuteChanged();
        }

        private void Select(bool isChecked, ProfileExportSelection selection)
        {
            if (!isChecked)
            {
                // Un-checking the live choice would leave none; the view re-reads the getter.
                OnPropertyChanged(NameOf(selection));

                return;
            }

            Selection = selection;
        }

        private static string NameOf(ProfileExportSelection selection)
        {
            return selection switch
            {
                ProfileExportSelection.LayoutOnly => nameof(IsLayoutOnlySelected),
                ProfileExportSelection.LightingOnly => nameof(IsLightingOnlySelected),
                _ => nameof(IsLayoutAndLightingSelected)
            };
        }
    }
}
