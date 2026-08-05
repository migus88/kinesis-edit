namespace KinesisEdit.Core.Layouts
{
    /// <summary>Kind of one scanned span of a layout line (specs/04-layout-file-format.md §1.3).</summary>
    internal enum LayoutLinePartKind
    {
        /// <summary>Text between groups — whitespace is tolerated, anything else is junk.</summary>
        Text = 0,

        /// <summary>A <c>[...]</c> group — single-key rule syntax (04 §1.3).</summary>
        SingleKeyGroup = 1,

        /// <summary>A <c>{...}</c> group — macro rule syntax (04 §1.3).</summary>
        MacroGroup = 2,

        /// <summary>An opening bracket whose closing bracket never arrives (04 §5.1).</summary>
        UnterminatedGroup = 3
    }

    /// <summary>One scanned span: absolute offsets into the line plus the bracket-free content.</summary>
    internal readonly record struct LayoutLinePart(int Start, int Length, LayoutLinePartKind Kind, string Inner)
    {
        /// <summary>End offset (exclusive) of the span.</summary>
        public int End => Start + Length;

        /// <summary>Whether this is a closed <c>[...]</c> or <c>{...}</c> group.</summary>
        public bool IsGroup => Kind is LayoutLinePartKind.SingleKeyGroup or LayoutLinePartKind.MacroGroup;

        /// <summary>Whether this is text between groups consisting only of whitespace.</summary>
        public bool IsWhitespaceText => Kind == LayoutLinePartKind.Text && string.IsNullOrWhiteSpace(Inner);
    }

    /// <summary>
    /// Splits a span of a layout line into bracket groups and the text between them
    /// (specs/04-layout-file-format.md §1.3: <c>[</c>/<c>]</c> single-key syntax,
    /// <c>{</c>/<c>}</c> macro syntax, no nesting). Offsets are absolute so callers can slice
    /// the verbatim original line for segment tracking (04 §5.1).
    /// </summary>
    internal static class LayoutLineScanner
    {
        /// <summary>Scans <paramref name="line"/> from <paramref name="start"/> (inclusive) to <paramref name="end"/> (exclusive).</summary>
        public static List<LayoutLinePart> Scan(string line, int start, int end)
        {
            var parts = new List<LayoutLinePart>();
            var index = start;

            while (index < end)
            {
                var open = line[index];

                if (open is '[' or '{')
                {
                    var close = open == '[' ? ']' : '}';
                    var closeIndex = line.IndexOf(close, index + 1, end - index - 1);

                    if (closeIndex < 0)
                    {
                        parts.Add(new LayoutLinePart(
                            index,
                            end - index,
                            LayoutLinePartKind.UnterminatedGroup,
                            line[(index + 1)..end]));
                        break;
                    }

                    var kind = open == '[' ? LayoutLinePartKind.SingleKeyGroup : LayoutLinePartKind.MacroGroup;

                    parts.Add(new LayoutLinePart(index, closeIndex - index + 1, kind, line[(index + 1)..closeIndex]));
                    index = closeIndex + 1;
                }
                else
                {
                    var next = NextGroupStart(line, index, end);

                    parts.Add(new LayoutLinePart(index, next - index, LayoutLinePartKind.Text, line[index..next]));
                    index = next;
                }
            }

            return parts;
        }

        private static int NextGroupStart(string line, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                if (line[i] is '[' or '{')
                {
                    return i;
                }
            }

            return end;
        }
    }
}
