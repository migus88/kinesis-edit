using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="DeviceEditorViewModel"/> whose two shell-facing members can be made to
    /// fail: <see cref="ConfirmCloseAsync"/> — the unsaved-work gate the shell awaits — and
    /// <see cref="Dispose"/>, which for a real editor stops the app-wide keystroke capture service.
    /// Both run inside navigations that must not throw, so what the shell does with a failure in
    /// them is part of its contract.
    /// </summary>
    internal sealed class FakeDeviceEditorViewModel : DeviceEditorViewModel, IDisposable
    {
        /// <summary>What <see cref="ConfirmCloseAsync"/> answers while it does not throw.</summary>
        public bool ConfirmCloseResult { get; set; } = true;

        /// <summary>When set, <see cref="ConfirmCloseAsync"/> throws it instead of answering.</summary>
        public Exception? ConfirmCloseExceptionToThrow { get; set; }

        /// <summary>When set, <see cref="Dispose"/> throws it.</summary>
        public Exception? DisposeExceptionToThrow { get; set; }

        /// <summary>How many times the shell disposed this editor.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>How many times the shell asked whether it may be closed.</summary>
        public int ConfirmCloseCount { get; private set; }

        public FakeDeviceEditorViewModel(DeviceSnapshot device) : base(device)
        {
        }

        public override Task<bool> ConfirmCloseAsync()
        {
            ConfirmCloseCount++;

            if (ConfirmCloseExceptionToThrow is not null)
            {
                throw ConfirmCloseExceptionToThrow;
            }

            return Task.FromResult(ConfirmCloseResult);
        }

        public void Dispose()
        {
            DisposeCount++;

            if (DisposeExceptionToThrow is not null)
            {
                throw DisposeExceptionToThrow;
            }
        }
    }
}
