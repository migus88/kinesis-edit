using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// An <see cref="IGlyphCoverage"/> that answers the same way every time, so a caption rule can
    /// be driven down both branches of the glyph gate on one machine and with no UI toolkit
    /// running. It also records what it was asked, which is how a test proves the rule consulted
    /// the probe only where it should have.
    /// </summary>
    internal sealed class FakeGlyphCoverage : IGlyphCoverage
    {
        /// <summary>A probe for which every character prints — the "font has everything" branch.</summary>
        public static FakeGlyphCoverage CoveringEverything => new(true);

        /// <summary>
        /// A probe for which nothing prints — what the embedded families actually answer for every
        /// one of the key table's 17 glyphs today.
        /// </summary>
        public static FakeGlyphCoverage CoveringNothing => new(false);

        /// <summary>Every string the rule asked about, in order.</summary>
        public IReadOnlyList<string?> Queries => _queries;

        private readonly List<string?> _queries = [];

        private readonly bool _canPrint;

        private FakeGlyphCoverage(bool canPrint)
        {
            _canPrint = canPrint;
        }

        /// <inheritdoc />
        public bool CanPrint(string? text)
        {
            _queries.Add(text);

            return _canPrint;
        }
    }
}
