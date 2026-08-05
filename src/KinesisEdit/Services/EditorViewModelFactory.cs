using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Input;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Services
{
    /// <summary>
    /// Resolves a device to the real editor when it can be drawn, and to the placeholder when it
    /// cannot. "Can be drawn" is both catalogs answering: <see cref="GeometryCatalog"/> for the key
    /// positions and <see cref="VisualCatalog"/> for where they sit. Only the Freestyle Edge RGB
    /// has an authored picture today — issues #39-#42 add the rest, and each one becomes editable
    /// by adding data, never by changing this class.
    /// </summary>
    public sealed class EditorViewModelFactory : IEditorViewModelFactory
    {
        private readonly IProfileSessionFactory _profileSessions;
        private readonly Func<IKeystrokeCaptureService> _captureServiceResolver;
        private readonly INotificationService _notifications;

        /// <summary>
        /// Creates the factory. The capture service is resolved through a delegate rather than
        /// held directly, for the same reason the message-box presenter resolves its owner window
        /// through one: it is built over the shell window, which does not exist while the
        /// composition root wires the view models (docs/app/app-shell.md).
        /// </summary>
        public EditorViewModelFactory(
            IProfileSessionFactory profileSessions,
            Func<IKeystrokeCaptureService> captureServiceResolver,
            INotificationService notifications)
        {
            _profileSessions = profileSessions ?? throw new ArgumentNullException(nameof(profileSessions));
            _captureServiceResolver = captureServiceResolver ?? throw new ArgumentNullException(nameof(captureServiceResolver));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        /// <inheritdoc />
        public EditorViewModelBase Create(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (!CanRender(device))
            {
                return new EditorPlaceholderViewModel(device);
            }

            return new DeviceEditorViewModel(device, _profileSessions, _captureServiceResolver(), _notifications);
        }

        private static bool CanRender(DeviceSnapshot device)
        {
            return VisualCatalog.TryGet(device.DeviceId, out _) && GeometryCatalog.TryGet(device.DeviceId, out _);
        }
    }
}
