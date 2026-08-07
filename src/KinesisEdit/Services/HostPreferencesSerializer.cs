using System.Text;
using System.Text.Json;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Writes a <see cref="HostPreferences"/> as the JSON text of the host-preferences file.
    /// <para>
    /// Unlike the on-device files, this one has exactly one writer and no legacy app sharing it, so
    /// it is regenerated whole rather than merged — there is no foreign line to preserve. Indented
    /// because a preferences file people occasionally open by hand should be readable; enum values
    /// are written by name, never by number, so the file survives a reordering of the enum.
    /// </para>
    /// <para>
    /// A null <see cref="HostPreferences.Window"/> writes <b>no</b> <c>window</c> object at all
    /// rather than <c>null</c> — absent and "nothing stored" are the same state to the reader, and
    /// one spelling of it is enough.
    /// </para>
    /// </summary>
    public static class HostPreferencesSerializer
    {
        private static readonly JsonWriterOptions _writerOptions = new()
        {
            Indented = true
        };

        /// <summary>The file text for <paramref name="preferences"/>.</summary>
        public static string Serialize(HostPreferences preferences)
        {
            ArgumentNullException.ThrowIfNull(preferences);

            using var buffer = new MemoryStream();

            using (var writer = new Utf8JsonWriter(buffer, _writerOptions))
            {
                writer.WriteStartObject();
                writer.WriteString(HostPreferencesJsonNames.Theme, preferences.Theme.ToString());
                writer.WriteString(HostPreferencesJsonNames.Motion, preferences.Motion.ToString());

                // IsUsable, not just non-null: a geometry built by hand can carry NaN or Infinity,
                // and Utf8JsonWriter throws on those. An unusable geometry is not worth a throw on
                // the save path — it is worth forgetting, which is what the reader would do with
                // it anyway.
                if (preferences.Window is { IsUsable: true } window)
                {
                    WriteWindow(writer, window);
                }

                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static void WriteWindow(Utf8JsonWriter writer, WindowGeometry window)
        {
            writer.WriteStartObject(HostPreferencesJsonNames.Window);
            writer.WriteNumber(HostPreferencesJsonNames.Width, window.Width);
            writer.WriteNumber(HostPreferencesJsonNames.Height, window.Height);

            if (window.X is { } x)
            {
                writer.WriteNumber(HostPreferencesJsonNames.X, x);
            }

            if (window.Y is { } y)
            {
                writer.WriteNumber(HostPreferencesJsonNames.Y, y);
            }

            writer.WriteBoolean(HostPreferencesJsonNames.Maximized, window.IsMaximized);
            writer.WriteEndObject();
        }
    }
}
