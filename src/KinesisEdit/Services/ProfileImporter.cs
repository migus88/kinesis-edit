using System.Globalization;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Transfer;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The Import flow of specs/10-apps-and-ui.md ("Import loads an external <c>.txt</c>
    /// (maximum 50 KB) and auto-detects whether it is layout or LED content") and its
    /// classification rule (specs/07-lighting.md §1.4): pick a file, refuse one over
    /// <see cref="ImportClassifier.MaxImportBytes"/>, classify it, and hand it to the session.
    /// <para>
    /// It lives beside the editor rather than inside it because none of those five steps needs
    /// the editor's state, and because the flow is the whole feature: <see cref="ViewModels.KeyboardEditorViewModel"/>
    /// is left with "run it, refresh the picture, show the message". Everything domain-shaped is
    /// borrowed — the size limit and the heuristic from <see cref="ImportClassifier"/>, the parse
    /// and the model swap from <see cref="Core.Profiles.ProfileSession.Import"/> — so this class
    /// owns only the sequence and its wording.
    /// </para>
    /// </summary>
    public sealed class ProfileImporter
    {
        /// <summary>
        /// Title of the picker and of every box the flow raises. The specs quote no wording for
        /// the import failures at all, so every string on this class is app-chosen, in the shape
        /// the rest of the app already uses ("Save Profile", "Load Profile").
        /// </summary>
        public const string DialogTitle = "Import File";

        /// <summary>Refusal for a file past the 50 KB maximum of 07 §1.4. App-chosen wording.</summary>
        public const string TooLargeMessageFormat =
            "'{0}' is {1} KB. An imported file may be at most {2} KB.";

        /// <summary>Prefix of the failure message; the error follows it. App-chosen wording.</summary>
        public const string FailureMessagePrefix = "The file could not be imported: ";

        /// <summary>Confirmation of an imported layout file. App-chosen wording.</summary>
        public const string LayoutImportedMessageFormat = "Imported '{0}' as this profile's layout.";

        /// <summary>Confirmation of an imported led file. App-chosen wording.</summary>
        public const string LightingImportedMessageFormat = "Imported '{0}' as this profile's lighting.";

        private const int BytesPerKilobyte = 1024;

        /// <summary>Builds the over-size refusal for a pick of <paramref name="byteLength"/> bytes.</summary>
        public static string BuildTooLargeMessage(string fileName, long byteLength)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                TooLargeMessageFormat,
                fileName,
                ToKilobytes(byteLength),
                ToKilobytes(ImportClassifier.MaxImportBytes));
        }

        /// <summary>Builds the confirmation for a file read as <paramref name="kind"/>.</summary>
        public static string BuildImportedMessage(string fileName, ImportedFileKind kind)
        {
            var format = kind == ImportedFileKind.Lighting
                ? LightingImportedMessageFormat
                : LayoutImportedMessageFormat;

            return string.Format(CultureInfo.InvariantCulture, format, fileName);
        }

        private readonly IFilePickerService _filePicker;

        /// <summary>Creates the importer over <paramref name="filePicker"/>.</summary>
        public ProfileImporter(IFilePickerService filePicker)
        {
            _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        }

        /// <summary>
        /// Runs the whole flow against <paramref name="session"/>. Total: a cancelled picker is a
        /// silent no-op, a file past the maximum is refused before its content is looked at, and
        /// anything the picker, the classifier or the parse throws comes back as a failure
        /// message rather than as an exception — the caller only decides where to show it.
        /// </summary>
        public async Task<ProfileImportOutcome> ImportAsync(IProfileSession session, DeviceId deviceId)
        {
            ArgumentNullException.ThrowIfNull(session);

            PickedFile? file;

            try
            {
                file = await _filePicker.PickTextFileAsync(DialogTitle).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                return ProfileImportOutcome.Failed(FailureMessagePrefix + exception.Message);
            }

            if (file is null)
            {
                return ProfileImportOutcome.Cancelled;
            }

            // On the true length, never on what was read: the reader caps huge picks and reports
            // the size faithfully so exactly this check can refuse them (07 §1.4).
            if (file.ByteLength > ImportClassifier.MaxImportBytes)
            {
                return ProfileImportOutcome.Failed(BuildTooLargeMessage(file.Name, file.ByteLength));
            }

            try
            {
                var kind = ImportClassifier.Classify(deviceId, file.Lines);
                var result = session.Import(kind, file.Lines);

                return ProfileImportOutcome.Applied(result.Kind, BuildImportedMessage(file.Name, result.Kind));
            }
            catch (Exception exception)
            {
                return ProfileImportOutcome.Failed(FailureMessagePrefix + exception.Message);
            }
        }

        /// <summary>Whole kilobytes, rounded up, so a file over the limit never reads as exactly the limit.</summary>
        private static long ToKilobytes(long byteLength)
        {
            return (byteLength + BytesPerKilobyte - 1) / BytesPerKilobyte;
        }
    }
}
