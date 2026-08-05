using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Thin binding of the pure pedal parse/serialize layer to a discovered v-Drive: loads and
    /// saves <c>active/pedals.txt</c> through <see cref="IVDriveFileService"/> with the save
    /// mechanics of specs/12-savant-elite.md §4.6. The path comes from the device catalog's
    /// <see cref="LayoutFileScheme"/> (<c>active</c> + <c>pedals.txt</c>), never from a literal
    /// here.
    /// <para>
    /// There is no eject: the SE2 has a physical mode switch, so §5 step 7 ends a save with
    /// "NOW CHANGE YOUR SE2 TO 'PLAY MODE' TO IMPLEMENT CHANGES", not a drive ejection.
    /// </para>
    /// </summary>
    public sealed class PedalFileService
    {
        private readonly IVDriveFileService _fileService;

        public PedalFileService(IVDriveFileService fileService)
        {
            ArgumentNullException.ThrowIfNull(fileService);

            _fileService = fileService;
        }

        /// <summary>
        /// The path of <c>pedals.txt</c> on <paramref name="location"/> — the layout folder and
        /// fixed file name of the device's <see cref="LayoutSchemeKind.PedalFile"/> scheme (12 §1,
        /// specs/03-vdrive-and-files.md §4.4). Throws <see cref="InvalidOperationException"/> for a
        /// device that has no pedal file.
        /// </summary>
        public static string GetPedalFilePath(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            var scheme = location.Device.LayoutScheme;

            if (scheme.Kind != LayoutSchemeKind.PedalFile
                || scheme.LayoutFolder is null
                || scheme.FixedLayoutFileName is null)
            {
                throw new InvalidOperationException($"{location.Device.DisplayName} has no pedal configuration file.");
            }

            return Path.Combine(location.RootPath, scheme.LayoutFolder, scheme.FixedLayoutFileName);
        }

        /// <summary>
        /// Loads and parses <c>pedals.txt</c> (12 §4.2). Throws
        /// <see cref="FileNotFoundException"/> when the file is missing — §5 step 8 turns that into
        /// the "Cannot open pedals.txt configuration file. / Create a new file?" prompt, which is
        /// the UI's decision, not this layer's.
        /// </summary>
        public PedalParseResult Load(VDriveLocation location)
        {
            return PedalFileParser.Parse(_fileService.ReadAllLines(GetPedalFilePath(location)));
        }

        /// <summary>
        /// Saves <paramref name="configuration"/> per 12 §4.6: the file on disk is re-read and only
        /// its pedal lines are rewritten in place, so the factory instruction block survives with
        /// its content verbatim — only the line terminators are normalized to the platform default
        /// by <see cref="IVDriveFileService.WriteAllLines"/> (specs/03-vdrive-and-files.md §5.2);
        /// a missing file is created outright with exactly seven lines in
        /// <see cref="PedalInputs.All"/> order.
        /// </summary>
        public void Save(VDriveLocation location, PedalConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var path = GetPedalFilePath(location);
            IReadOnlyList<PedalSourceLine>? sourceLines;

            // Only the re-read may fall back to creating the file. Scoping the catch this tightly
            // matters: WriteAllLines throws FileNotFoundException in its own right when the file
            // vanishes mid-save (the drive is unmounted, or the pedal is flipped to play mode), and
            // a wider try would answer that by rewriting the drive with seven bare lines — throwing
            // away the merge result that still held the factory instruction block.
            try
            {
                sourceLines = PedalFileParser.Parse(_fileService.ReadAllLines(path)).SourceLines;
            }
            catch (FileNotFoundException)
            {
                sourceLines = null;
            }

            if (sourceLines is null)
            {
                _fileService.WriteAllLines(path, PedalFileSerializer.Serialize(configuration), allowCreate: true);

                return;
            }

            _fileService.WriteAllLines(path, PedalFileSerializer.Merge(configuration, sourceLines));
        }
    }
}
