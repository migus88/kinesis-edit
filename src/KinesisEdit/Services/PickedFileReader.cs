using System.Text;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The toolkit-free half of <see cref="AvaloniaFilePickerService"/>: turns a picked file's
    /// bytes into a <see cref="PickedFile"/>. It lives outside the picker so the decoding rules
    /// are unit-testable without an Avalonia application, leaving the picker itself with nothing
    /// but the platform dialog call.
    /// <para>
    /// The decoding mirrors <c>VDriveFileService.ReadAllLines</c> (docs/app/vdrive.md, "Io"):
    /// raw bytes decoded as <see cref="Encoding.Latin1"/> — never UTF-8, and with no BOM
    /// stripping — then split on CRLF, LF, or a lone CR, a trailing newline yielding no empty
    /// last line. That is what makes an imported file parse identically to one read off the
    /// drive. Core's splitter is private, so it is duplicated here rather than shared;
    /// <c>PickedFileReaderTests</c> pins the two against each other over the same bytes so they
    /// cannot drift apart.
    /// </para>
    /// </summary>
    public static class PickedFileReader
    {
        /// <summary>
        /// Defensive read cap — twenty times the 50 KB import maximum of specs/07-lighting.md
        /// §1.4. Anything over the maximum is refused by the import view model on
        /// <see cref="PickedFile.ByteLength"/> long before its content matters, so buffering all
        /// of a multi-gigabyte pick would be pure waste. The true length is reported regardless.
        /// </summary>
        public const long MaxReadBytes = 20 * 50 * 1024;

        private const int ReadBufferSize = 64 * 1024;

        private const char CarriageReturn = '\r';

        private const char LineFeed = '\n';

        /// <summary>
        /// Reads <paramref name="stream"/> into a <see cref="PickedFile"/> named
        /// <paramref name="name"/>, keeping at most <see cref="MaxReadBytes"/> of content while
        /// still reporting the file's true byte length: a seekable stream is asked for its
        /// length and abandoned at the cap, a non-seekable one is drained to count its bytes but
        /// only the first <see cref="MaxReadBytes"/> are buffered.
        /// </summary>
        public static async Task<PickedFile> ReadAsync(
            string name,
            string? path,
            Stream stream,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(stream);

            var declaredLength = stream.CanSeek ? Math.Max(0, stream.Length - stream.Position) : (long?)null;
            var content = new MemoryStream((int)Math.Min(declaredLength ?? ReadBufferSize, MaxReadBytes));
            var buffer = new byte[ReadBufferSize];
            var readLength = 0L;

            while (declaredLength is null || readLength < MaxReadBytes)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    break;
                }

                readLength += read;

                var keep = (int)Math.Min(read, Math.Max(0, MaxReadBytes - content.Length));

                if (keep > 0)
                {
                    content.Write(buffer, 0, keep);
                }
            }

            var byteLength = declaredLength ?? readLength;

            // Decoding the raw bytes rather than reading text keeps a BOM as ordinary
            // characters, the same "no BOM handling" rule the v-Drive reader follows.
            var text = Encoding.Latin1.GetString(content.GetBuffer(), 0, (int)content.Length);

            return new PickedFile(name, path, byteLength, SplitLines(text), byteLength > MaxReadBytes);
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            var lines = new List<string>();
            var start = 0;
            var index = 0;

            while (index < text.Length)
            {
                var current = text[index];

                if (current != CarriageReturn && current != LineFeed)
                {
                    index++;
                    continue;
                }

                lines.Add(text[start..index]);

                if (current == CarriageReturn && index + 1 < text.Length && text[index + 1] == LineFeed)
                {
                    index++;
                }

                index++;
                start = index;
            }

            if (start < text.Length)
            {
                lines.Add(text[start..]);
            }

            return lines;
        }
    }
}
