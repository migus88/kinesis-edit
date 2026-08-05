using System.Diagnostics;
using System.Text;

namespace KinesisEdit.Core.VDrive.Eject
{
    /// <summary>
    /// <see cref="IProcessRunner"/> backed by <see cref="Process"/> with redirected output
    /// streams and no shell execute, supporting the eject sequence of
    /// specs/03-vdrive-and-files.md §5.3. If the process has not exited after 30 seconds it
    /// is killed (with its process tree) and a result with exit code -1 and a timeout note
    /// appended to <see cref="ProcessRunResult.StandardError"/> is returned; no exception is
    /// thrown on timeout.
    /// </summary>
    public sealed class SystemProcessRunner : IProcessRunner
    {
        private const int TimeoutMilliseconds = 30_000;

        private const int TimeoutExitCode = -1;

        /// <summary>
        /// Runs the process to completion (or the 30-second timeout) and captures both output
        /// streams. See the class summary for the timeout behavior.
        /// </summary>
        public ProcessRunResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);
            ArgumentNullException.ThrowIfNull(arguments);

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            using var process = new Process();
            process.StartInfo = startInfo;
            process.OutputDataReceived += (_, eventArgs) => AppendLine(outputBuilder, eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => AppendLine(errorBuilder, eventArgs.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();

                errorBuilder.AppendLine($"Process '{fileName}' timed out after {TimeoutMilliseconds / 1000} seconds and was killed.");

                return new ProcessRunResult
                {
                    ExitCode = TimeoutExitCode,
                    StandardOutput = outputBuilder.ToString(),
                    StandardError = errorBuilder.ToString(),
                };
            }

            process.WaitForExit();

            return new ProcessRunResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = outputBuilder.ToString(),
                StandardError = errorBuilder.ToString(),
            };
        }

        private static void AppendLine(StringBuilder builder, string? data)
        {
            if (data is null)
            {
                return;
            }

            builder.AppendLine(data);
        }
    }
}
