using System.Globalization;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// The <c>layout&lt;n&gt;.txt</c> / <c>led&lt;n&gt;.txt</c> naming of the numbered-profile
    /// scheme (specs/03-vdrive-and-files.md §4.1, specs/07-lighting.md §1.2). One place, because
    /// three callers need the same names: the v-Drive paths <see cref="ProfileSession"/> reads
    /// and writes, the <c>led_mode=ledN.txt</c> settings value it saves, and the base names an
    /// export keeps (specs/11-feature-dialogs.md §11.5).
    /// </summary>
    internal static class ProfileFileNames
    {
        private const string LayoutPrefix = "layout";
        private const string LightingPrefix = "led";
        private const string FileSuffix = ".txt";

        /// <summary>The layout file name of <paramref name="profileNumber"/>, e.g. <c>layout1.txt</c>.</summary>
        public static string Layout(int profileNumber)
        {
            return LayoutPrefix + profileNumber.ToString(CultureInfo.InvariantCulture) + FileSuffix;
        }

        /// <summary>The led file name of <paramref name="profileNumber"/>, e.g. <c>led1.txt</c>.</summary>
        public static string Lighting(int profileNumber)
        {
            return LightingPrefix + profileNumber.ToString(CultureInfo.InvariantCulture) + FileSuffix;
        }
    }
}
