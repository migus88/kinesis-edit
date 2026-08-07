using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="DeviceEditorViewModel"/> whose two shell-facing members can be made to
    /// fail: <see cref="ConfirmCloseAsync"/> — the unsaved-work gate the shell awaits on Home, on
    /// Configure and now on window close — and <see cref="Dispose"/>, which for a real editor stops
    /// the app-wide keystroke capture service. Both run inside operations that must not throw, so
    /// what the shell does with a failure in them is part of its contract.
    /// <para>
    /// <see cref="ConfirmCloseAsync"/> can also be made slow (<see cref="ConfirmCloseGate"/>) — a
    /// question is on screen until it is answered, which is when the shell has to hold everything
    /// else off — or made to go through a real message box
    /// (<see cref="ConfirmCloseNotifications"/>), which is how the presenter's "no host, answer as
    /// if dismissed" branch can be driven end to end.
    /// </para>
    /// </summary>
    internal sealed class FakeDeviceEditorViewModel : DeviceEditorViewModel, IDisposable
    {
        /// <summary>Title of the box <see cref="ConfirmCloseNotifications"/> raises.</summary>
        public const string ConfirmCloseTitle = "Unsaved Changes";

        /// <summary>Message of that box.</summary>
        public const string ConfirmCloseMessage = "This session has unsaved changes.";

        /// <summary>What <see cref="ConfirmCloseAsync"/> answers while it does not throw.</summary>
        public bool ConfirmCloseResult { get; set; } = true;

        /// <summary>When set, <see cref="ConfirmCloseAsync"/> throws it instead of answering.</summary>
        public Exception? ConfirmCloseExceptionToThrow { get; set; }

        /// <summary>
        /// When set, <see cref="ConfirmCloseAsync"/> does not answer until the test completes this
        /// source — a question that is still on screen.
        /// </summary>
        public TaskCompletionSource? ConfirmCloseGate { get; set; }

        /// <summary>
        /// When set, <see cref="ConfirmCloseAsync"/> asks this service a real message box and lets
        /// the close through only on <see cref="MessageBoxResult.Yes"/> — the shape every real
        /// editor's unsaved-changes prompt has, and the only way to exercise what a box that never
        /// reached the screen means for the shell.
        /// </summary>
        public INotificationService? ConfirmCloseNotifications { get; set; }

        /// <summary>When set, <see cref="Dispose"/> throws it.</summary>
        public Exception? DisposeExceptionToThrow { get; set; }

        /// <summary>How many times the shell disposed this editor.</summary>
        public int DisposeCount { get; private set; }

        /// <summary>How many times the shell asked whether it may be closed.</summary>
        public int ConfirmCloseCount { get; private set; }

        public FakeDeviceEditorViewModel(DeviceSnapshot device) : base(device)
        {
        }

        public override async Task<bool> ConfirmCloseAsync()
        {
            ConfirmCloseCount++;

            if (ConfirmCloseExceptionToThrow is not null)
            {
                throw ConfirmCloseExceptionToThrow;
            }

            if (ConfirmCloseGate is not null)
            {
                await ConfirmCloseGate.Task.ConfigureAwait(true);
            }

            if (ConfirmCloseNotifications is not null)
            {
                var outcome = await ConfirmCloseNotifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = ConfirmCloseTitle,
                    Message = ConfirmCloseMessage,
                    Buttons = MessageBoxButtons.YesNoCancel,
                    Icon = MessageBoxIcon.Warning
                }).ConfigureAwait(true);

                return outcome.Result == MessageBoxResult.Yes;
            }

            return ConfirmCloseResult;
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
