using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IKeystrokeCaptureService"/>: counts start/stop/dispose calls and
    /// lets a test push a keystroke in as if the user had pressed a key.
    /// </summary>
    internal sealed class FakeKeystrokeCaptureService : IKeystrokeCaptureService
    {
        public bool IsCapturing { get; private set; }

        public bool IsSuspended { get; private set; }

        /// <summary>How often <see cref="Start"/> was called.</summary>
        public int StartCount { get; private set; }

        /// <summary>How often <see cref="Stop"/> was called.</summary>
        public int StopCount { get; private set; }

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
            IsSuspended = true;
        }

        public void Resume()
        {
            IsSuspended = false;
        }

        /// <summary>Raises a keystroke for <paramref name="key"/>, as the real service would.</summary>
        public void RaiseKeystroke(KeyDefinition key)
        {
            ArgumentNullException.ThrowIfNull(key);

            KeystrokeCaptured?.Invoke(new CapturedKeystroke
            {
                Key = key,
                PhysicalKey = PhysicalKeyCode.None
            });
        }

        public void Dispose()
        {
            DisposeCount++;

            Stop();
        }
    }
}
