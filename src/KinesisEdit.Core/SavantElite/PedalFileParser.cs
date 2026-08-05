using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.SavantElite
{
    /// <summary>
    /// Parses the lines of <c>active/pedals.txt</c> into a fresh <see cref="PedalConfiguration"/>
    /// per specs/12-savant-elite.md §4.1-§4.2. Pure lines-in/model-out — file I/O, encoding, and
    /// newline handling live in the v-Drive services.
    /// <para>
    /// Tolerant and case-insensitive (§4.1: "read case-insensitively, lines are lowercased before
    /// matching"): only lines that *start with* a pedal input name in either bracket form are
    /// parsed; every other line — the factory <c>*</c> instruction block and its ASCII module
    /// diagram — is preserved verbatim in <see cref="PedalParseResult.SourceLines"/>. A line that
    /// names an input but cannot be applied is tracked with per-segment validity instead of being
    /// dropped silently; duplicate lines for the same input apply in file order, so the last one
    /// wins.
    /// </para>
    /// </summary>
    public static class PedalFileParser
    {
        /// <summary>The separator between the input name and the action text (§4.2).</summary>
        private const char Separator = '>';

        /// <summary>Parses <paramref name="lines"/> into a fresh configuration (§4.2).</summary>
        public static PedalParseResult Parse(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var configuration = new PedalConfiguration();
            var invalidLines = new List<PedalInvalidLine>();
            var sourceLines = new List<PedalSourceLine>(lines.Count);

            for (var i = 0; i < lines.Count; i++)
            {
                var raw = lines[i] ?? string.Empty;

                sourceLines.Add(ParseLine(configuration, invalidLines, i + 1, raw, Preprocess(raw)));
            }

            return new PedalParseResult(configuration, invalidLines, sourceLines);
        }

        private static PedalSourceLine ParseLine(
            PedalConfiguration configuration,
            List<PedalInvalidLine> invalidLines,
            int lineNumber,
            string raw,
            string processed)
        {
            if (!TryReadInputName(processed, out var input, out var mode, out var closeIndex))
            {
                // §4.1: "The parser only cares about lines that start with a pedal-name token;
                // every other line is ignored on load and preserved verbatim on save."
                return new PedalSourceLine(raw, PedalInputId.None);
            }

            var sourceLine = new PedalSourceLine(raw, input);
            var recorder = new PedalLineSegmentRecorder(raw);

            if (closeIndex + 1 >= processed.Length || processed[closeIndex + 1] != Separator)
            {
                recorder.MarkWholeLineInvalid();
                invalidLines.Add(new PedalInvalidLine(lineNumber, input, raw, recorder.Build()));

                return sourceLine;
            }

            var parts = LayoutLineScanner.Scan(processed, closeIndex + 2, processed.Length);

            if (mode == PedalInputMode.Macro)
            {
                var macro = ReadMacro(parts, recorder);

                if (!recorder.HasInvalidSegments)
                {
                    configuration[input].SetMacro(macro);

                    return sourceLine;
                }
            }
            else
            {
                var action = ReadSingleAction(parts, recorder);

                if (!recorder.HasInvalidSegments)
                {
                    configuration[input].SetSingleAction(action);

                    return sourceLine;
                }
            }

            invalidLines.Add(new PedalInvalidLine(lineNumber, input, raw, recorder.Build()));

            return sourceLine;
        }

        /// <summary>
        /// §4.2: <c>"[" &lt;input&gt; "]"</c> selects single mode, <c>"{" &lt;input&gt; "}"</c>
        /// macro mode, for the whole line. False when the line does not start with an input name —
        /// a line that merely contains one further along is not a pedal line.
        /// </summary>
        private static bool TryReadInputName(
            string processed,
            out PedalInputId input,
            out PedalInputMode mode,
            out int closeIndex)
        {
            input = PedalInputId.None;
            mode = PedalInputMode.None;
            closeIndex = -1;

            if (processed.Length == 0 || processed[0] is not ('[' or '{'))
            {
                return false;
            }

            var index = processed.IndexOf(processed[0] == '[' ? ']' : '}', 1);

            if (index < 0 || !PedalInputs.TryParseToken(processed[1..index], out input))
            {
                return false;
            }

            mode = processed[0] == '[' ? PedalInputMode.Single : PedalInputMode.Macro;
            closeIndex = index;

            return true;
        }

        /// <summary>
        /// §4.2: "For single mode exactly one <c>[token]</c> is read." No group at all leaves the
        /// input unassigned — the <c>[input]&gt;</c> line of a freshly created file (§4.1).
        /// </summary>
        private static KeyDefinition? ReadSingleAction(List<LayoutLinePart> parts, PedalLineSegmentRecorder recorder)
        {
            KeyDefinition? action = null;
            var hasGroup = false;

            foreach (var part in parts)
            {
                if (part.Kind == LayoutLinePartKind.Text)
                {
                    recorder.Add(part, part.IsWhitespaceText);

                    continue;
                }

                if (part.Kind != LayoutLinePartKind.SingleKeyGroup || hasGroup)
                {
                    recorder.Add(part, false);

                    continue;
                }

                hasGroup = true;
                action = PedalTokenMap.Resolve(part.Inner);

                recorder.Add(part, action is not null);
            }

            return action;
        }

        /// <summary>
        /// §4.2: "the <c>{...}</c> groups are iterated, treating a leading <c>-</c> as key-down and
        /// a leading <c>+</c> as key-up". An empty value side is an empty macro — the input stays
        /// in macro mode, so <c>{input}&gt;</c> round-trips.
        /// </summary>
        private static Macro ReadMacro(List<LayoutLinePart> parts, PedalLineSegmentRecorder recorder)
        {
            var reader = new PedalMacroReader();

            foreach (var part in parts)
            {
                if (part.Kind == LayoutLinePartKind.Text)
                {
                    recorder.Add(part, part.IsWhitespaceText);

                    continue;
                }

                recorder.Add(part, part.Kind == LayoutLinePartKind.MacroGroup && reader.TryRead(part.Inner));
            }

            var macro = new Macro();

            macro.AddKeystrokes(reader.Complete());

            return macro;
        }

        /// <summary>
        /// §4.1: every line is lowercased and trailing whitespace trimmed before matching.
        /// Lowercasing is per character, so offsets into the processed line always map onto the
        /// verbatim original for segment tracking. Only the *line* is trimmed — the inner space of
        /// a trailing <c>{ }</c> group is content and survives, because the line ends with <c>}</c>.
        /// </summary>
        private static string Preprocess(string raw)
        {
            var end = raw.Length;

            while (end > 0 && char.IsWhiteSpace(raw[end - 1]))
            {
                end--;
            }

            if (end == 0)
            {
                return string.Empty;
            }

            return string.Create(end, raw, static (buffer, source) =>
            {
                for (var i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = char.ToLowerInvariant(source[i]);
                }
            });
        }
    }
}
