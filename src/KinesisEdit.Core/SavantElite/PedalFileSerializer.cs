namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Writes <c>active/pedals.txt</c> per specs/12-savant-elite.md §4.3 and §4.6. Two entry
    /// points, matching the two cases §4.6 distinguishes: <see cref="Serialize"/> emits the seven
    /// lines of a newly created file in <see cref="PedalInputs.All"/> order, while
    /// <see cref="Merge"/> rewrites only the lines whose prefix names an input, in place, copying
    /// every other line — the factory instruction block and its ASCII diagram — verbatim. "Verbatim"
    /// is the line *content*: the line terminator is the platform default, because
    /// <see cref="VDrive.Io.IVDriveFileService.WriteAllLines"/> re-terminates every line
    /// (specs/03-vdrive-and-files.md §5.2: "native platform line endings are produced"), so a CRLF
    /// device file saved on macOS comes back LF-terminated throughout.
    /// <para>
    /// Tokens are written in canonical registry casing (<c>F1</c>-<c>F24</c> capitalised,
    /// everything else lowercase) through <see cref="PedalTokenMap"/>, so a field file written by
    /// firmware 1.0.44 upgrades its <c>{125}</c>/<c>{500}</c> delays to <c>{d125}</c>/<c>{d500}</c>
    /// on the first save (§4.4).
    /// </para>
    /// </summary>
    public static class PedalFileSerializer
    {
        /// <summary>Emits the seven input lines in fixed order — the file a fresh drive gets (§4.1, §4.6).</summary>
        public static IReadOnlyList<string> Serialize(PedalConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var lines = new List<string>(PedalInputs.Count);

            foreach (var input in PedalInputs.All)
            {
                lines.Add(SerializeInput(configuration[input]));
            }

            return lines;
        }

        /// <summary>
        /// §4.6: rewrites every line of <paramref name="sourceLines"/> whose prefix names an input
        /// from the current model, at the same line index and in whichever bracket form the model
        /// now needs (an input that flipped single↔macro keeps its slot), and copies every other
        /// line verbatim.
        /// <para>
        /// §4.6 sanctions no appending — "it rewrites *only* the lines whose prefix matches a pedal
        /// token" — and §1 states a device "will only have some of these inputs", so a file that
        /// lists five inputs stays a five-line file. A line is therefore added only for an input
        /// that the file never mentioned *and* that the model now assigns
        /// (<see cref="PedalInput.IsAssigned"/>), and it is inserted right after the last
        /// pedal-owned line — keeping the pedal block contiguous and the factory instruction block
        /// at the bottom — in <see cref="PedalInputs.All"/> order.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> Merge(
            PedalConfiguration configuration,
            IReadOnlyList<PedalSourceLine> sourceLines)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(sourceLines);

            var lines = new List<string>(sourceLines.Count + PedalInputs.Count);
            var written = new HashSet<PedalInputId>();
            var insertIndex = -1;

            foreach (var sourceLine in sourceLines)
            {
                if (sourceLine.Input == PedalInputId.None)
                {
                    lines.Add(sourceLine.Text);

                    continue;
                }

                lines.Add(SerializeInput(configuration[sourceLine.Input]));
                written.Add(sourceLine.Input);
                insertIndex = lines.Count;
            }

            // A file with no pedal line at all has no block to stay contiguous with, so the new
            // lines go to the end of it.
            if (insertIndex < 0)
            {
                insertIndex = lines.Count;
            }

            foreach (var input in PedalInputs.All)
            {
                if (written.Contains(input) || !configuration[input].IsAssigned)
                {
                    continue;
                }

                lines.Insert(insertIndex, SerializeInput(configuration[input]));
                insertIndex++;
            }

            return lines;
        }

        /// <summary>
        /// One input's line (§4.3): <c>{input}&gt;</c> plus the macro groups in macro mode,
        /// otherwise <c>[input]&gt;[token]</c> — with nothing after the <c>&gt;</c> when the input
        /// is unassigned.
        /// </summary>
        public static string SerializeInput(PedalInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var token = PedalInputs.GetToken(input.Id);

            if (input.Mode == PedalInputMode.Macro && input.Macro is not null)
            {
                return "{" + token + "}>" + PedalMacroWriter.Write(input.Macro);
            }

            var action = input.Mode == PedalInputMode.Single && input.Action is not null
                ? "[" + PedalTokenMap.GetSingleToken(input.Action) + "]"
                : string.Empty;

            return "[" + token + "]>" + action;
        }
    }
}
