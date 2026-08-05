using KinesisEdit.Core.Keys;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Shared helpers and file fixtures for the Savant Elite2 tests: one-call parse and
    /// parse→save round trips, key lookups by file token, and the two real device files quoted in
    /// specs/12-savant-elite.md §4.1 and §4.5. No on-disk fixtures — every file is inline.
    /// </summary>
    internal static class PedalFixtures
    {
        /// <summary>
        /// The factory <c>pedals.txt</c> of 12 §4.1: the seven assignment lines (including the
        /// <c>{125}</c> delay of the firmware-1.0.44 double click) followed by the <c>*</c>
        /// instruction block quoted in §1 and its ASCII module diagram.
        /// </summary>
        public static string[] FactoryFile { get; } =
        [
            "[lpedal]>[lmouse]",
            "{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}",
            "[rpedal]>[rmouse]",
            "[jack1]>[lmouse]",
            "[jack2]>[rmouse]",
            "[jack3]>[bspace]",
            "{jack4}>{-shift}{-t}{+t}{+shift}{-h}{+h}{-a}{+a}{-n}{+n}{-k}{+k}{-space}{+space}{-y}{+y}{-o}{+o}{-u}{+u}{-.}{+.}",
            "*",
            "* Above are the assigned actions for 7 possible inputs(left, middle, and right pedals",
            "* and Jacks 1-4). Your device will only have some of these inputs",
            "*",
            "*                    ---------------------",
            "*       (jack3)-----|                     |-----(jack1)",
            "*       (jack4)-----|   SE2 control module|-----(jack2)",
            "*                   |                     |",
            "*                    ----O-----------O----",
            "*",
            "*         (lpedal)        (mpedal)        (rpedal)",
            "*"
        ];

        /// <summary>The hand-edited device file of 12 §4.5, written in the factory pair style.</summary>
        public static string[] HandEditedFile { get; } =
        [
            "[mpedal]>[space]",
            "{rpedal}>{-ctrl}{-alt}{-delete}{+delete}{+ctrl}{+alt}",
            "[jack1]>[F1]",
            "{jack4}>{-F1}{+F1}{-F2}{+F2}{-F3}{+F3}{-F4}{+F4}"
        ];

        public static PedalParseResult Parse(params string[] lines)
        {
            return PedalFileParser.Parse(lines);
        }

        /// <summary>Parses and saves back through the 12 §4.6 merge — the load/save cycle of the app.</summary>
        public static IReadOnlyList<string> RoundTrip(params string[] lines)
        {
            var result = PedalFileParser.Parse(lines);

            return PedalFileSerializer.Merge(result.Configuration, result.SourceLines);
        }

        /// <summary>The canonical line one input would be saved as after parsing <paramref name="lines"/>.</summary>
        public static string SerializeInput(PedalInputId input, params string[] lines)
        {
            return PedalFileSerializer.SerializeInput(Parse(lines).Configuration[input]);
        }

        public static KeyDefinition Key(string token)
        {
            return PedalTokenMap.Resolve(token)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve for the pedal.");
        }
    }
}
