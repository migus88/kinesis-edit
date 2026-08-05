using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Parses the lines of a layout file (<c>layouts/layoutN.txt</c>, Advantage2
    /// <c>qwerty.txt</c>/<c>dvorak.txt</c>) into a fresh <see cref="KeyboardLayout"/> per
    /// specs/04-layout-file-format.md §4.2 and specs/06-macros.md §2. Pure lines-in/model-out —
    /// file I/O, encoding, and newline handling live in the v-Drive services.
    /// <para>
    /// The dialect is selected from the device identity alone (04 §4.1); parsing is
    /// case-insensitive (every line is lowercased and right-trimmed first, 04 §1.2) and
    /// tolerant: rules are applied through the model's tolerant load paths, device limits are
    /// left to <see cref="KeyboardLayout.Validate"/>, and every line that cannot be applied is
    /// tracked with per-segment validity instead of being dropped silently (04 §5).
    /// </para>
    /// </summary>
    public sealed class LayoutFileParser
    {
        /// <summary>The Gen1-family bottom-layer line prefix — three characters including the space (04 §3.1).</summary>
        private const string FnLinePrefix = "fn ";

        /// <summary>Index of the bottom (Fn/keypad) layer on two-layer devices (04 §3.1, §3.2).</summary>
        private const int BottomLayerIndex = 1;

        /// <summary>The dialect files are parsed as (04 §4.1).</summary>
        public LayoutDialect Dialect => _rules.Dialect;

        private readonly DeviceId _deviceId;
        private readonly LayoutVariant _variant;
        private readonly LayoutDialectRules _rules;

        /// <summary>
        /// Creates a parser for <paramref name="deviceId"/>'s layout dialect (04 §4.1).
        /// <paramref name="variant"/> selects the Advantage2 QWERTY/Dvorak geometry (QWERTY by
        /// default). Throws for devices without an in-scope layout dialect.
        /// </summary>
        public LayoutFileParser(DeviceId deviceId, LayoutVariant variant = LayoutVariant.None)
        {
            _deviceId = deviceId;
            _variant = variant;
            _rules = LayoutDialectRules.Create(deviceId);
        }

        /// <summary>
        /// Parses <paramref name="lines"/> into a fresh factory-state layout — the file fully
        /// replaces the model (04 §4.2 step 1). Blank lines are ignored; there is no comment
        /// syntax (04 §1.2).
        /// </summary>
        public LayoutParseResult Parse(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var layout = KeyboardLayout.Create(_deviceId, _variant);
            var invalidLines = new List<LayoutInvalidLine>();
            var currentLayer = 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var raw = lines[i] ?? string.Empty;
                var processed = Preprocess(raw);

                if (processed.Length == 0)
                {
                    continue;
                }

                ParseLine(layout, invalidLines, i + 1, raw, processed, ref currentLayer);
            }

            return new LayoutParseResult(_rules.Dialect, layout, invalidLines);
        }

        private void ParseLine(
            KeyboardLayout layout,
            List<LayoutInvalidLine> invalidLines,
            int lineNumber,
            string raw,
            string processed,
            ref int currentLayer)
        {
            // 04 §3.3: Gen2 header lines switch the current layer and are detected before the
            // split on the first '>' — "<base>" must never be split there.
            if (_rules.UsesLayerHeaders && processed[0] == '<')
            {
                if (processed[^1] == '>' && Gen2LayerHeaders.TryGetLayerIndex(processed, out var headerLayer))
                {
                    currentLayer = headerLayer;

                    return;
                }

                invalidLines.Add(CreateWholeLineInvalid(lineNumber, currentLayer, raw));

                return;
            }

            var offset = 0;
            var lineLayer = _rules.UsesLayerHeaders ? currentLayer : 0;

            // 04 §3.1: a Gen1-family line carrying the "fn " prefix targets the bottom layer;
            // the 3-character prefix is stripped before parsing.
            if (_rules.UsesFnLinePrefix && processed.StartsWith(FnLinePrefix, StringComparison.Ordinal))
            {
                lineLayer = BottomLayerIndex;
                offset = FnLinePrefix.Length;
            }

            // 04 §1.3: the older FS and Adv2 parsers require the line to start with '[' or '{'.
            if (_rules.RequiresBracketAtLineStart
                && (offset >= processed.Length || processed[offset] is not ('[' or '{')))
            {
                invalidLines.Add(CreateWholeLineInvalid(lineNumber, lineLayer, raw));

                return;
            }

            // 04 §1.3: the separator is the first '>' in the line; no '>' means no rule.
            var separatorIndex = processed.IndexOf('>', offset);

            if (separatorIndex < 0)
            {
                invalidLines.Add(CreateWholeLineInvalid(lineNumber, lineLayer, raw));

                return;
            }

            var context = new LayoutLineContext
            {
                Layout = layout,
                Rules = _rules,
                Processed = processed,
                Offset = offset,
                SeparatorIndex = separatorIndex,
                ConfigParts = LayoutLineScanner.Scan(processed, offset, separatorIndex),
                ValueParts = LayoutLineScanner.Scan(processed, separatorIndex + 1, processed.Length),
                Segments = new LayoutLineSegmentRecorder(raw),
                LayerIndex = lineLayer
            };

            // 04 §1.3 rule-type detection: '[' or ']' on the config side is a single-key rule,
            // '{' or '}' a macro rule; neither makes the whole line invalid.
            bool applied;

            if (ContainsAny(processed, offset, separatorIndex, '[', ']'))
            {
                applied = SingleKeyLineParser.TryParse(context);
            }
            else if (ContainsAny(processed, offset, separatorIndex, '{', '}'))
            {
                applied = MacroLineParser.TryParse(context);
            }
            else
            {
                invalidLines.Add(CreateWholeLineInvalid(lineNumber, lineLayer, raw));

                return;
            }

            if (!applied)
            {
                invalidLines.Add(new LayoutInvalidLine(lineNumber, context.LayerIndex, raw, context.Segments.Build()));
            }
        }

        /// <summary>
        /// 04 §1.2: every line is lowercased and trailing whitespace trimmed before parsing.
        /// Lowercasing is per character, so offsets into the processed line always map onto the
        /// verbatim original for segment tracking.
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

        private static bool ContainsAny(string text, int start, int end, char first, char second)
        {
            for (var i = start; i < end; i++)
            {
                if (text[i] == first || text[i] == second)
                {
                    return true;
                }
            }

            return false;
        }

        private static LayoutInvalidLine CreateWholeLineInvalid(int lineNumber, int layerIndex, string raw)
        {
            var recorder = new LayoutLineSegmentRecorder(raw);

            recorder.MarkWholeLineInvalid();

            return new LayoutInvalidLine(lineNumber, layerIndex, raw, recorder.Build());
        }
    }
}
