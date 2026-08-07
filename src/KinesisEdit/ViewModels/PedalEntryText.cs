namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one display rule the pedal's entry box adds on top of
    /// <see cref="KinesisEdit.Core.SavantElite.PedalActionDescriber"/>: specs/12-savant-elite.md §5
    /// makes a space visible while it is being recorded.
    /// <para>
    /// It applies to the <b>entry box only</b>. The row memos keep the raw token text the describer
    /// produces, because that is the text of the file — substituting there would make the read-only
    /// view disagree with <c>pedals.txt</c>.
    /// </para>
    /// </summary>
    public static class PedalEntryText
    {
        /// <summary>
        /// U+00B7 MIDDLE DOT, the visible stand-in for a space.
        /// <para>
        /// Spec 12 §5 names <c>␣</c> (U+2423 OPEN BOX), and this is a deliberate deviation from it:
        /// U+2423 is in <b>neither</b> embedded IBM Plex family, at any weight, so it would draw as
        /// tofu. It cannot be a drawn mark either — the substitution happens <i>inside</i> a mono
        /// token (<c>{lshift+ }</c> becomes <c>{lshift+·}</c>), so only a character will do. The
        /// middle dot is carried by both families at every weight, is the conventional
        /// visible-space mark, and is already the app's separator glyph.
        /// </para>
        /// </summary>
        public const char SpaceSymbol = '·';

        /// <summary>The space character it replaces.</summary>
        public const char Space = ' ';

        /// <summary>One display item with its spaces made visible; null becomes empty.</summary>
        public static string Format(string? item)
        {
            return item is null ? string.Empty : item.Replace(Space, SpaceSymbol);
        }

        /// <summary>Every display item of an entry, in order; null becomes an empty list.</summary>
        public static IReadOnlyList<string> Format(IReadOnlyList<string>? items)
        {
            if (items is null || items.Count == 0)
            {
                return [];
            }

            var formatted = new string[items.Count];

            for (var index = 0; index < items.Count; index++)
            {
                formatted[index] = Format(items[index]);
            }

            return formatted;
        }
    }
}
