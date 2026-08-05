using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IProfileSession"/> over a layout the test builds itself: every
    /// answer is settable, and <see cref="Save"/> records its calls and returns whatever the test
    /// staged.
    /// </summary>
    internal sealed class FakeProfileSession : IProfileSession
    {
        /// <summary>A save that went through, with no post-save wording.</summary>
        public static ProfileSaveResult SuccessfulSave { get; } = new()
        {
            Success = true,
            Violations = [],
            Ejected = true
        };

        public KeyboardLayout Layout { get; }

        public object? Lighting { get; set; }

        public IReadOnlyList<LayoutInvalidLine> InvalidLines { get; set; } = [];

        public int ProfileNumber { get; set; } = 1;

        public bool CanSave { get; set; } = true;

        public bool IsDirty { get; set; }

        /// <summary>How often <see cref="Save"/> was called.</summary>
        public int SaveCallCount { get; private set; }

        /// <summary>What <see cref="Save"/> reports.</summary>
        public ProfileSaveResult ResultToReturn { get; set; } = SuccessfulSave;

        /// <summary>When set, <see cref="Save"/> throws it instead of returning.</summary>
        public Exception? SaveExceptionToThrow { get; set; }

        /// <summary>
        /// When set, runs inside <see cref="Save"/> — the window in which the editor reports
        /// <c>IsBusy</c>, so a test can observe command state while the save is in flight.
        /// </summary>
        public Action? DuringSave { get; set; }

        public FakeProfileSession(KeyboardLayout layout)
        {
            Layout = layout ?? throw new ArgumentNullException(nameof(layout));
        }

        public ProfileSaveResult Save()
        {
            SaveCallCount++;

            DuringSave?.Invoke();

            if (SaveExceptionToThrow is not null)
            {
                throw SaveExceptionToThrow;
            }

            return ResultToReturn;
        }
    }
}
