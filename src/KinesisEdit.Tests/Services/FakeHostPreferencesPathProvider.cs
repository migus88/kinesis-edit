using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// An <see cref="IHostPreferencesPathProvider"/> that answers with the path a test named, or
    /// throws when the test is checking that a store cannot be taken down by a path lookup that
    /// fails.
    /// </summary>
    internal sealed class FakeHostPreferencesPathProvider : IHostPreferencesPathProvider
    {
        /// <summary>How many times <see cref="GetFilePath"/> has been called.</summary>
        public int GetFilePathCallCount { get; private set; }

        private readonly string? _path;

        private readonly Exception? _failure;

        public FakeHostPreferencesPathProvider(string path)
        {
            _path = path;
        }

        private FakeHostPreferencesPathProvider(Exception failure)
        {
            _failure = failure;
        }

        /// <summary>A provider whose every call throws <paramref name="failure"/>.</summary>
        public static FakeHostPreferencesPathProvider Failing(Exception failure)
        {
            return new FakeHostPreferencesPathProvider(failure);
        }

        /// <inheritdoc />
        public string GetFilePath()
        {
            GetFilePathCallCount++;

            if (_failure is not null)
            {
                throw _failure;
            }

            return _path!;
        }
    }
}
