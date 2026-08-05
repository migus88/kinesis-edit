using System.Collections.ObjectModel;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The Fn-layer save-token exceptions of specs/07-lighting.md §2.4 item 7
    /// (specs/05-key-model.md §5.5): on the Fn/embedded layer only, eight media/navigation
    /// keys are written under their top-layer position tokens (mute as <c>[F1]</c>, insert as
    /// <c>[pause]</c>, scroll lock as <c>[del]</c>, ...) and translated back on load. The
    /// mapping is between key codes; unmapped codes pass through unchanged. Top-layer lines
    /// are never translated, and §5.5 scopes the table to the FS-family bottom layer — the
    /// engine applies it in the RGB key-backlight context only, never on the TKO.
    /// </summary>
    public static class FnLayerTokenTranslator
    {
        /// <summary>The eight (in-memory, file) key-code pairs in spec table order (05 §5.5).</summary>
        public static IReadOnlyList<FnLayerTokenPair> Pairs { get; }

        private static readonly Dictionary<int, int> _fileCodeByMemoryCode;
        private static readonly Dictionary<int, int> _memoryCodeByFileCode;

        static FnLayerTokenTranslator()
        {
            Pairs = new ReadOnlyCollection<FnLayerTokenPair>(new[]
            {
                new FnLayerTokenPair(VirtualKey.VolumeMute, VirtualKey.F1),
                new FnLayerTokenPair(VirtualKey.VolumeDown, VirtualKey.F2),
                new FnLayerTokenPair(VirtualKey.VolumeUp, VirtualKey.F3),
                new FnLayerTokenPair(VirtualKey.MediaPlayPause, VirtualKey.F4),
                new FnLayerTokenPair(VirtualKey.MediaPrevTrack, VirtualKey.F5),
                new FnLayerTokenPair(VirtualKey.MediaNextTrack, VirtualKey.F6),
                new FnLayerTokenPair(VirtualKey.Insert, VirtualKey.Pause),
                new FnLayerTokenPair(VirtualKey.ScrollLock, VirtualKey.Delete)
            });

            _fileCodeByMemoryCode = new Dictionary<int, int>(Pairs.Count);
            _memoryCodeByFileCode = new Dictionary<int, int>(Pairs.Count);

            foreach (var pair in Pairs)
            {
                _fileCodeByMemoryCode.Add(pair.MemoryKeyCode, pair.FileKeyCode);
                _memoryCodeByFileCode.Add(pair.FileKeyCode, pair.MemoryKeyCode);
            }
        }

        /// <summary>
        /// Maps an in-memory key code to the key code whose token is written on the Fn layer;
        /// unmapped codes are returned unchanged.
        /// </summary>
        public static int TranslateForSave(int memoryKeyCode)
        {
            return _fileCodeByMemoryCode.GetValueOrDefault(memoryKeyCode, memoryKeyCode);
        }

        /// <summary>
        /// Maps a key code parsed from an Fn-layer file line to the in-memory key it colors;
        /// unmapped codes are returned unchanged.
        /// </summary>
        public static int TranslateForLoad(int fileKeyCode)
        {
            return _memoryCodeByFileCode.GetValueOrDefault(fileKeyCode, fileKeyCode);
        }
    }
}
