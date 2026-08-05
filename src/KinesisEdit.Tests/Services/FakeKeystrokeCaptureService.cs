using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IKeystrokeCaptureService"/>: counts start/stop/suspend/resume/dispose
    /// calls and lets a test push a keystroke in as if the user had pressed a key.
    /// </summary>
    internal sealed class FakeKeystrokeCaptureService : IKeystrokeCaptureService
    {
        public bool IsCapturing { get; private set; }

        public bool IsSuspended { get; private set; }

        /// <summary>How often <see cref="Start"/> was called.</summary>
        public int StartCount { get; private set; }

        /// <summary>How often <see cref="Stop"/> was called.</summary>
        public int StopCount { get; private set; }

        /// <summary>How often <see cref="Suspend"/> was called.</summary>
        public int SuspendCount { get; private set; }

        /// <summary>How often <see cref="Resume"/> was called.</summary>
        public int ResumeCount { get; private set; }

        /// <summary>How often <see cref="Dispose"/> was called.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>Whether anything is still listening to <see cref="KeystrokeCaptured"/>.</summary>
        public bool HasSubscribers => KeystrokeCaptured is not null;

        public event Action<CapturedKeystroke>? KeystrokeCaptured;

        public void Start()
        {
            StartCount++;
            IsCapturing = true;
        }

        public void Stop()
        {
            StopCount++;
            IsCapturing = false;
            IsSuspended = false;
        }

        public void Suspend()
        {
            SuspendCount++;
            IsSuspended = true;
        }

        public void Resume()
        {
            ResumeCount++;
            IsSuspended = false;
        }

        /// <summary>
        /// Raises a keystroke for <paramref name="key"/> with the modifiers held while it was
        /// pressed, as the real service would. <see cref="CapturedKeystroke"/> drops the held set
        /// itself when the key is a modifier (specs/05-key-model.md §5.1).
        /// </summary>
        public void RaiseKeystroke(KeyDefinition key, params KeyDefinition[] heldModifiers)
        {
            RaiseKeystroke(key, PhysicalKeyCode.None, heldModifiers);
        }

        /// <summary>Raises a keystroke that also names the physical position it came from.</summary>
        public void RaiseKeystroke(KeyDefinition key, PhysicalKeyCode physicalKey, params KeyDefinition[] heldModifiers)
        {
            ArgumentNullException.ThrowIfNull(key);
            ArgumentNullException.ThrowIfNull(heldModifiers);

            KeystrokeCaptured?.Invoke(new CapturedKeystroke
            {
                Key = key,
                PhysicalKey = physicalKey,
                HeldModifiers = heldModifiers
            });
        }

        public void Dispose()
        {
            DisposeCount++;

            Stop();
        }
    }
}
