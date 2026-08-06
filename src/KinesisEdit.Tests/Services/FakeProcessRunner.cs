using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// An <see cref="IProcessRunner"/> that runs nothing: it records the command it was asked for
    /// and returns a canned result, or throws when the test is exercising a failing launch.
    /// </summary>
    internal sealed class FakeProcessRunner : IProcessRunner
    {
        /// <summary>The file name of the last requested command, or null if never called.</summary>
        public string? LastFileName { get; private set; }

        /// <summary>The arguments of the last requested command, or null if never called.</summary>
        public IReadOnlyList<string>? LastArguments { get; private set; }

        private readonly ProcessRunResult? _result;

        private readonly Exception? _failure;

        public FakeProcessRunner(int exitCode, string standardOutput = "", string standardError = "")
        {
            _result = new ProcessRunResult
            {
                ExitCode = exitCode,
                StandardOutput = standardOutput,
                StandardError = standardError,
            };
        }

        private FakeProcessRunner(Exception failure)
        {
            _failure = failure;
        }

        /// <summary>A runner whose every call throws <paramref name="failure"/>.</summary>
        public static FakeProcessRunner Failing(Exception failure)
        {
            return new FakeProcessRunner(failure);
        }

        /// <inheritdoc />
        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            LastFileName = fileName;
            LastArguments = arguments;

            if (_failure is not null)
            {
                throw _failure;
            }

            return _result!;
        }
    }
}
