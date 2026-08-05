using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.VDrive.Eject
{
    /// <summary>
    /// Hand-rolled <see cref="IProcessRunner"/> fake that records the last invocation and
    /// returns a preconfigured result.
    /// </summary>
    internal sealed class FakeProcessRunner : IProcessRunner
    {
        public ProcessRunResult ResultToReturn { get; set; } = new ProcessRunResult
        {
            ExitCode = 0,
            StandardOutput = string.Empty,
            StandardError = string.Empty,
        };

        public string? LastFileName { get; private set; }

        public IReadOnlyList<string>? LastArguments { get; private set; }

        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            LastFileName = fileName;
            LastArguments = arguments;

            return ResultToReturn;
        }
    }
}
