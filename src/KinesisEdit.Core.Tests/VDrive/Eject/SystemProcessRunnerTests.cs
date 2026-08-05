using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.VDrive.Eject
{
    public class SystemProcessRunnerTests
    {
        [Fact]
        public void Run_WithSucceedingCommand_ReturnsZeroExitCodeAndCapturedStdout()
        {
            var runner = new SystemProcessRunner();

            var result = runner.Run("dotnet", new[] { "--version" });

            Assert.Equal(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        }

        [Fact]
        public void Run_WithFailingCommand_ReturnsNonZeroExitCodeAndErrorText()
        {
            var runner = new SystemProcessRunner();

            var result = runner.Run("dotnet", new[] { "definitely-not-a-real-dotnet-command" });

            Assert.NotEqual(0, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.StandardError + result.StandardOutput));
        }
    }
}
