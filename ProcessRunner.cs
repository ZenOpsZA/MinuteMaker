using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MinuteMaker
{
    public sealed class ProcessRunResult
    {
        public int ExitCode { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
    }

    public static class ProcessRunner
    {

        public static async Task RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string?>? environmentVariables = null,
            bool suppressOutput = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (environmentVariables is not null)
            {
                foreach (var kvp in environmentVariables)
                {
                    if (kvp.Value is not null)
                        startInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                stdout.AppendLine(e.Data);

                if (!suppressOutput)
                    Console.WriteLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                stderr.AppendLine(e.Data);

                if (!suppressOutput)
                    Console.Error.WriteLine(e.Data);
            };

            if (!process.Start())
                throw new InvalidOperationException($"Failed to start process: {fileName}");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Process failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                    $"Command: {fileName} {arguments}{Environment.NewLine}" +
                    $"STDERR:{Environment.NewLine}{stderr}");
            }
        }

        public static async Task RunWithStageProgressAsync(
    string fileName,
    string arguments,
    string workingDirectory,
    IReadOnlyDictionary<string, string?>? environmentVariables = null,
    string? logFilePath = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (environmentVariables is not null)
            {
                foreach (var kvp in environmentVariables)
                {
                    if (kvp.Value is not null)
                        startInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            StreamWriter? logWriter = null;
            if (!string.IsNullOrWhiteSpace(logFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
                logWriter = new StreamWriter(logFilePath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }

            var stageLock = new object();
            var currentStage = "Launching Python";
            var stageChangedAt = DateTime.UtcNow;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                stdout.AppendLine(e.Data);
                logWriter?.WriteLine(e.Data);

                if (e.Data.StartsWith("STAGE:", StringComparison.OrdinalIgnoreCase))
                {
                    var stage = e.Data.Substring("STAGE:".Length).Trim();

                    lock (stageLock)
                    {
                        currentStage = stage;
                        stageChangedAt = DateTime.UtcNow;
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                    return;

                stderr.AppendLine(e.Data);
                logWriter?.WriteLine("[stderr] " + e.Data);
            };

            ConsoleCancelEventHandler? cancelHandler = null;

            try
            {
                if (!process.Start())
                    throw new InvalidOperationException($"Failed to start process: {fileName}");

                cancelHandler = (_, e) =>
                {
                    e.Cancel = true;
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore cleanup failures
                    }
                };

                Console.CancelKeyPress += cancelHandler;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var spinnerFrames = new[] { '|', '/', '-', '\\' };
                var spinnerIndex = 0;

                string? lastRenderedStage = null;
                string lastElapsedText = "00:00";
                var lastEndTime = DateTime.UtcNow;

                while (!process.HasExited)
                {
                    string stage;
                    DateTime changedAt;

                    lock (stageLock)
                    {
                        stage = currentStage;
                        changedAt = stageChangedAt;
                    }

                    var elapsed = DateTime.UtcNow - changedAt;
                    var elapsedText = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
                    lastElapsedText = elapsedText;

                    if (!string.Equals(stage, lastRenderedStage, StringComparison.Ordinal))
                    {
                        if (lastRenderedStage is not null)
                        {
                            var ended = DateTime.UtcNow - lastEndTime;
                            var endedText = $"{(int)ended.TotalMinutes:00}:{ended.Seconds:00}";
                            Console.Write($"\r  ↳ {lastRenderedStage} done ({endedText})      ");
                            Console.WriteLine();
                            lastEndTime = DateTime.UtcNow;
                        }

                        lastRenderedStage = stage;
                        spinnerIndex = 0;
                    }

                    Console.Write($"\r  ↳ {stage} {spinnerFrames[spinnerIndex % spinnerFrames.Length]} {elapsedText}      ");
                    spinnerIndex++;

                    await Task.Delay(250);
                }

                await process.WaitForExitAsync();

                if (lastRenderedStage is not null)
                {
                    Console.Write($"\r  ↳ {lastRenderedStage} done ({lastElapsedText})      ");
                    Console.WriteLine();
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Process failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                        $"Command: {fileName} {arguments}{Environment.NewLine}" +
                        $"STDERR:{Environment.NewLine}{stderr}");
                }

                Console.WriteLine("Step 2/4 - WhisperX pipeline complete");
            }
            finally
            {
                if (cancelHandler is not null)
                    Console.CancelKeyPress -= cancelHandler;

                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        // ignore cleanup failures
                    }
                }

                logWriter?.Dispose();
            }
        }

        public static async Task<ProcessRunResult> RunAndCaptureAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string?>? environmentVariables = null,
            string? logFilePath = null,
            bool echoOutput = false)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (environmentVariables is not null)
            {
                foreach (var kvp in environmentVariables)
                {
                    if (kvp.Value is not null)
                        startInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            using var process = new Process { StartInfo = startInfo };

            if (!process.Start())
                throw new InvalidOperationException($"Failed to start process: {fileName}");

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (!string.IsNullOrWhiteSpace(logFilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);

                var combined = new StringBuilder();
                combined.AppendLine("=== STDOUT ===");
                combined.AppendLine(stdout);
                combined.AppendLine();
                combined.AppendLine("=== STDERR ===");
                combined.AppendLine(stderr);

                await File.WriteAllTextAsync(logFilePath, combined.ToString(), Encoding.UTF8);
            }

            if (echoOutput)
            {
                if (!string.IsNullOrWhiteSpace(stdout))
                    Console.WriteLine(stdout);

                if (!string.IsNullOrWhiteSpace(stderr))
                    Console.Error.WriteLine(stderr);
            }

            return new ProcessRunResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr
            };
        }
    }
}
