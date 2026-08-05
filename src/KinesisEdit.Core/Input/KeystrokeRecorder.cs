using System.Collections.ObjectModel;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Input
{
    /// <summary>
    /// The UI-free state machine behind keystroke capture — it turns a stream of physical
    /// key-down/key-up events into <see cref="CapturedKeystroke"/>s and tells the host which
    /// events to swallow. It reproduces the legacy capture contract of
    /// specs/10-apps-and-ui.md ("Keyboard-input capture") and
    /// specs/12-savant-elite.md §"Programming a pedal" step 3:
    /// <list type="bullet">
    /// <item>modifiers accumulate on key-down and release on key-up; auto-repeat never
    /// duplicates a held key, modifier or not;</item>
    /// <item>a non-modifier key-down is recorded together with the currently held modifiers and swallowed;</item>
    /// <item>Print Screen is recognized only on key-up, carrying the modifiers held at its key-down;</item>
    /// <item>a modifier tapped alone registers as a keystroke of that modifier on key-up, while a
    /// modifier that took part in a combination — or that was pressed while another key was
    /// already held — does not;</item>
    /// <item>a position that resolves to no key-table entry is passed through untouched, and so
    /// is a key-up for a key that was never recorded — an unknown key is let through rather
    /// than eaten.</item>
    /// </list>
    /// The recorder holds no OS or toolkit state; a host adapter owns the event plumbing.
    /// </summary>
    public sealed class KeystrokeRecorder
    {
        /// <summary>
        /// The modifiers currently held, in press order and deduplicated by position
        /// (specs/05-key-model.md §5.1). A fresh snapshot on every read.
        /// </summary>
        public IReadOnlyList<KeyDefinition> HeldModifiers
        {
            get => SnapshotHeldModifiers();
        }

        private readonly HeldKeySet _heldKeys = new();

        private IReadOnlyList<KeyDefinition>? _printScreenModifiers;

        /// <summary>
        /// Feeds one physical key event to the recorder and returns what the host must do with
        /// it: whether to swallow it, and the keystroke it completed (if any).
        /// <see cref="KeyTransition.None"/> is ignored and always passes through.
        /// </summary>
        public KeystrokeCaptureResult Handle(PhysicalKeyCode key, KeyTransition transition)
        {
            return transition switch
            {
                KeyTransition.Down => HandleKeyDown(key),
                KeyTransition.Up => HandleKeyUp(key),
                _ => KeystrokeCaptureResult.PassThrough
            };
        }

        /// <summary>
        /// Forgets every held key. Called when capture stops or is suspended so a key released
        /// outside the capture window cannot leak into the next recording
        /// (specs/10-apps-and-ui.md: capture is suspended while a dialog needs real typing).
        /// </summary>
        public void Reset()
        {
            _heldKeys.Clear();
            _printScreenModifiers = null;
        }

        private KeystrokeCaptureResult HandleKeyDown(PhysicalKeyCode key)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            if (definition is null)
            {
                // An unresolvable position still is a real physical key press, so it ends the
                // "tapped alone" claim of every modifier held around it. The event itself passes
                // through untouched — an unknown key is never eaten.
                _heldKeys.MarkAllConsumed();

                return KeystrokeCaptureResult.PassThrough;
            }

            if (PhysicalKeyMap.IsModifier(key))
            {
                return HandleModifierDown(key);
            }

            return HandleRegularKeyDown(definition, key);
        }

        private KeystrokeCaptureResult HandleModifierDown(PhysicalKeyCode key)
        {
            // A modifier pressed while something else is already held is part of a chord, never a
            // tap: neither it nor the keys it joins may still register as "tapped alone"
            // (specs/12-savant-elite.md §"Programming a pedal" step 3). Auto-repeat of a lone
            // modifier is not a chord, hence "other than this key".
            var joinsACombination = _heldKeys.ContainsAnyOtherThan(key);

            _heldKeys.Hold(key);

            if (joinsACombination)
            {
                _heldKeys.MarkAllConsumed();
            }

            return KeystrokeCaptureResult.Swallowed;
        }

        private KeystrokeCaptureResult HandleRegularKeyDown(KeyDefinition definition, PhysicalKeyCode key)
        {
            if (_heldKeys.Contains(key))
            {
                // Auto-repeat: the host gives no repeat flag, so a key-down for a position that is
                // already held is swallowed without emitting a duplicate keystroke.
                return KeystrokeCaptureResult.Swallowed;
            }

            // Any non-modifier key-down makes every held modifier part of a combination, so none
            // of them can still count as "tapped alone" when it is released.
            _heldKeys.MarkAllConsumed();
            _heldKeys.Hold(key);

            if (key == PhysicalKeyCode.PrintScreen)
            {
                // Print Screen is recognized only on key-up (specs/10-apps-and-ui.md), but the
                // modifiers that belong to it are the ones held when it went down.
                _printScreenModifiers = SnapshotHeldModifiers();

                return KeystrokeCaptureResult.Swallowed;
            }

            return Capture(definition, key, SnapshotHeldModifiers());
        }

        private KeystrokeCaptureResult HandleKeyUp(PhysicalKeyCode key)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            if (definition is null)
            {
                return KeystrokeCaptureResult.PassThrough;
            }

            if (!_heldKeys.Release(key, out var wasConsumed))
            {
                // The recorder never saw this key go down — for example a modifier held before
                // capture started. It must not be swallowed: never eat a key that was not recorded.
                return KeystrokeCaptureResult.PassThrough;
            }

            if (PhysicalKeyMap.IsModifier(key))
            {
                if (wasConsumed)
                {
                    return KeystrokeCaptureResult.Swallowed;
                }

                // A modifier tapped alone registers as a keystroke of that modifier on key-up
                // (specs/12-savant-elite.md §"Programming a pedal" step 3), never carrying
                // modifiers of its own (specs/05-key-model.md §5.1).
                return Capture(definition, key, Array.Empty<KeyDefinition>());
            }

            if (key == PhysicalKeyCode.PrintScreen)
            {
                var modifiers = _printScreenModifiers ?? Array.Empty<KeyDefinition>();
                _printScreenModifiers = null;

                return Capture(definition, key, modifiers);
            }

            return KeystrokeCaptureResult.Swallowed;
        }

        private IReadOnlyList<KeyDefinition> SnapshotHeldModifiers()
        {
            var modifiers = new List<KeyDefinition>();

            foreach (var position in _heldKeys.Positions)
            {
                if (!PhysicalKeyMap.IsModifier(position))
                {
                    continue;
                }

                var definition = PhysicalKeyMap.Resolve(position);

                if (definition is not null)
                {
                    modifiers.Add(definition);
                }
            }

            return new ReadOnlyCollection<KeyDefinition>(modifiers);
        }

        private static KeystrokeCaptureResult Capture(
            KeyDefinition definition,
            PhysicalKeyCode key,
            IReadOnlyList<KeyDefinition> heldModifiers)
        {
            return KeystrokeCaptureResult.Captured(new CapturedKeystroke
            {
                Key = definition,
                PhysicalKey = key,
                HeldModifiers = heldModifiers
            });
        }
    }
}
