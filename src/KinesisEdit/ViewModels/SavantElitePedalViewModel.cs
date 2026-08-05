using System.Collections.ObjectModel;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The Savant Elite2 editor, currently read-only: it loads <c>active/pedals.txt</c> once and
    /// shows what the pedal has stored (specs/12-savant-elite.md §4, §5). Seven rows are always
    /// built — §1's file is authoritative for none of the hardware, and "your device will only
    /// have some of these inputs" — plus every line that named an input but could not be parsed,
    /// because unparseable lines are surfaced, never dropped.
    /// <para>
    /// Nothing here writes: no capture, no Single/Macro modes, no Special Actions menu, no
    /// Save/Save As/Open (12 §5, §6). Those stay with issue #18.
    /// </para>
    /// </summary>
    public sealed class SavantElitePedalViewModel : DeviceEditorViewModel
    {
        /// <summary>Subtitle stating that this view only reads the device.</summary>
        public const string ReadOnlyNotice = "Read-only view of what the pedal has stored. Editing is not available yet.";

        /// <summary>Note above the rows (12 §1, §5: the file describes all seven possible inputs).</summary>
        public const string AllInputsNote = "All seven possible inputs are listed — your product has only some of these pedals and jacks.";

        /// <summary>Note shown instead of a file's contents while the session is in demo mode.</summary>
        public const string DemoModeMessage = "Demo mode: no pedal v-Drive is connected, so nothing was read from a device. Every input is shown unassigned.";

        /// <summary>Note shown when the drive carries no pedal file yet (12 §5 step 8).</summary>
        public const string MissingFileMessage = "This v-Drive has no active/pedals.txt yet, so every input is shown unassigned.";

        /// <summary>Note shown when the file was read but programs nothing.</summary>
        public const string EmptyFileMessage = "The pedal configuration file has no assignments — every input is unassigned.";

        /// <summary>Prefix of the note shown when the file could not be read at all.</summary>
        public const string LoadFailureMessagePrefix = "Could not read the pedal configuration file: ";

        /// <summary>Heading of the unparseable-lines section.</summary>
        public const string InvalidLinesHeading = "Lines that could not be read";

        /// <summary>Explanation under <see cref="InvalidLinesHeading"/>.</summary>
        public const string InvalidLinesNote = "These lines name a pedal input but could not be understood. They are listed rather than dropped silently; nothing on the device has been changed.";

        /// <summary>The seven inputs in <see cref="PedalInputs.All"/> order, always all of them.</summary>
        public IReadOnlyList<PedalInputRowViewModel> Inputs { get; }

        /// <summary>Every line that named an input but could not be applied (12 §4.2).</summary>
        public IReadOnlyList<PedalInvalidLineViewModel> InvalidLines { get; }

        /// <summary>Whether the unparseable-lines section has anything to show.</summary>
        public bool HasInvalidLines => InvalidLines.Count > 0;

        /// <summary>Where the seven rows came from; the view colors <see cref="StatusMessage"/> from it.</summary>
        public PedalLoadState LoadState { get; }

        /// <summary>The demo / missing-file / empty-file / failure note; empty when there is nothing to say.</summary>
        public string StatusMessage { get; }

        /// <summary>Whether <see cref="StatusMessage"/> has anything to show.</summary>
        public bool HasStatusMessage => StatusMessage.Length > 0;

        /// <summary>The file this view was loaded from; empty in demo mode (12 §5 "File name:").</summary>
        public string FilePath { get; }

        /// <summary>Whether there is a file path to show.</summary>
        public bool HasFilePath => FilePath.Length > 0;

        /// <summary>The read-only subtitle.</summary>
        public string Notice => ReadOnlyNotice;

        /// <summary>The "not every input exists on your product" note.</summary>
        public string InputsNote => AllInputsNote;

        /// <summary>
        /// Loads the device's pedal file and builds the rows. The read happens here, on the
        /// Configure path that already shows the loading caption, because the file is seven
        /// significant lines and the view has nothing to show before it: an editor that
        /// materialized empty and filled in later would flash unassigned rows on every open.
        /// </summary>
        public SavantElitePedalViewModel(DeviceSnapshot device, PedalFileService pedalFiles)
            : base(device)
        {
            ArgumentNullException.ThrowIfNull(pedalFiles);

            var configuration = new PedalConfiguration();
            IReadOnlyList<PedalInvalidLine> invalidLines = [];
            var filePath = string.Empty;
            var failureMessage = string.Empty;

            if (Device.IsDemoMode || Device.Location is null)
            {
                // Demo mode never touches the drive (03 §3.5): even a present but unwritable
                // v-Drive is opened as a demo session, and the seven rows stand in for it.
                LoadState = PedalLoadState.DemoMode;
            }
            else
            {
                try
                {
                    filePath = PedalFileService.GetPedalFilePath(Device.Location);

                    var result = pedalFiles.Load(Device.Location);

                    configuration = result.Configuration;
                    invalidLines = result.InvalidLines;
                    LoadState = PedalLoadState.Loaded;
                }
                catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
                {
                    // A pedal that has never been programmed simply has no file; 12 §5 step 8
                    // turns that into a "Create a new file?" prompt only when saving.
                    LoadState = PedalLoadState.FileMissing;
                }
                catch (Exception exception) when (exception is IOException
                                                      or UnauthorizedAccessException
                                                      or NotSupportedException
                                                      or ArgumentException
                                                      or InvalidOperationException)
                {
                    LoadState = PedalLoadState.LoadFailed;
                    failureMessage = exception.Message;
                }
            }

            Inputs = BuildRows(configuration);
            InvalidLines = BuildInvalidLines(invalidLines);
            StatusMessage = BuildStatusMessage(LoadState, configuration.IsEmpty, failureMessage);
            FilePath = filePath;
        }

        private static IReadOnlyList<PedalInputRowViewModel> BuildRows(PedalConfiguration configuration)
        {
            var rows = new PedalInputRowViewModel[configuration.Inputs.Count];

            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = new PedalInputRowViewModel(configuration.Inputs[index]);
            }

            return new ReadOnlyCollection<PedalInputRowViewModel>(rows);
        }

        private static IReadOnlyList<PedalInvalidLineViewModel> BuildInvalidLines(IReadOnlyList<PedalInvalidLine> lines)
        {
            var rows = new PedalInvalidLineViewModel[lines.Count];

            for (var index = 0; index < rows.Length; index++)
            {
                rows[index] = new PedalInvalidLineViewModel(lines[index]);
            }

            return new ReadOnlyCollection<PedalInvalidLineViewModel>(rows);
        }

        private static string BuildStatusMessage(PedalLoadState state, bool isEmpty, string failureMessage)
        {
            return state switch
            {
                PedalLoadState.DemoMode => DemoModeMessage,
                PedalLoadState.FileMissing => MissingFileMessage,
                PedalLoadState.LoadFailed => LoadFailureMessagePrefix + failureMessage,
                PedalLoadState.Loaded when isEmpty => EmptyFileMessage,
                _ => string.Empty
            };
        }
    }
}
