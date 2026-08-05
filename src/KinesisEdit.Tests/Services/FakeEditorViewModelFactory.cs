using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IEditorViewModelFactory"/> that builds placeholders — or, when
    /// <see cref="ExceptionToThrow"/> is set, fails the way a real editor's constructor can. The
    /// shell forgets the task <c>OpenDevice</c> returns, so what happens to that failure is part of
    /// the navigation's contract.
    /// </summary>
    internal sealed class FakeEditorViewModelFactory : IEditorViewModelFactory
    {
        /// <summary>When set, every <see cref="Create"/> throws it instead of building an editor.</summary>
        public Exception? ExceptionToThrow { get; set; }

        /// <summary>
        /// When set, <see cref="Create"/> hands this editor back instead of a placeholder — the seam
        /// for an editor that misbehaves on the way out (<see cref="FakeDeviceEditorViewModel"/>).
        /// </summary>
        public DeviceEditorViewModel? EditorToReturn { get; set; }

        /// <summary>The devices the shell asked for an editor for, in order.</summary>
        public List<DeviceSnapshot> Requests { get; } = [];

        public DeviceEditorViewModel Create(DeviceSnapshot device)
        {
            Requests.Add(device);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return EditorToReturn ?? new EditorPlaceholderViewModel(device);
        }
    }
}
