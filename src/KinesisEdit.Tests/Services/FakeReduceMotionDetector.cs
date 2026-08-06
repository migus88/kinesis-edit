using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// An <see cref="IReduceMotionDetector"/> that answers what the test told it to, or throws
    /// when the test is checking that a failing detector cannot take the app down with it.
    /// </summary>
    internal sealed class FakeReduceMotionDetector : IReduceMotionDetector
    {
        /// <summary>How many times <see cref="Detect"/> has been called.</summary>
        public int DetectCallCount { get; private set; }

        private readonly bool? _result;

        private readonly Exception? _failure;

        public FakeReduceMotionDetector(bool? result)
        {
            _result = result;
        }

        private FakeReduceMotionDetector(Exception failure)
        {
            _failure = failure;
        }

        /// <summary>A detector whose every call throws <paramref name="failure"/>.</summary>
        public static FakeReduceMotionDetector Failing(Exception failure)
        {
            return new FakeReduceMotionDetector(failure);
        }

        /// <inheritdoc />
        public bool? Detect()
        {
            DetectCallCount++;

            if (_failure is not null)
            {
                throw _failure;
            }

            return _result;
        }
    }
}
