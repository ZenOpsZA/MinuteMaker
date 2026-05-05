using MinuteMaker.Configuration;
using MinuteMaker.Persistence.Corrections;
using MinuteMaker.Models.Pipeline;
using MinuteMaker.Models.Transcription;
using MinuteMaker.Services.Corrections;
using MinuteMaker.Services.Output;
using MinuteMaker.Services.Transcription;
using MinuteMaker.Utilities;
using System.Text;

namespace MinuteMaker
{
    internal static class Program
    {
        private sealed record InputSelection(string FolderPath, string FilePath);

        private enum PipelineStartMode
        {
            FullRun,
            ResumeFromExistingJson,
            Cancel
        }

        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            try
            {
                var config = AppConfig.Load();

                Console.WriteLine("=== MinuteMaker ===");
                Console.WriteLine($"Config file checked : {config.ConfigFilePath}");
                Console.WriteLine($"Config file exists  : {config.ConfigFileExists}");
                Console.WriteLine();

                var selection = SelectInputFromFolder();
                var inputMediaPath = selection.FilePath;

                if (!File.Exists(inputMediaPath))
                {
                    Console.WriteLine($"Input file not found: {inputMediaPath}");
                    return 1;
                }

                var paths = PipelinePaths.Create(selection.FolderPath, inputMediaPath);

                Directory.CreateDirectory(paths.JobFolder);

                Console.WriteLine($"Selected: {Path.GetFileName(inputMediaPath)}");
                Console.WriteLine($"Output folder: {paths.JobFolder}");

                var startMode = GetPipelineStartMode(paths);
                if (startMode == PipelineStartMode.Cancel)
                {
                    Console.WriteLine("Cancelled.");
                    return 0;
                }

                if (startMode == PipelineStartMode.FullRun)
                {
                    Console.Write("Step 1/4 - Extracting WAV with FFmpeg");
                    var ffmpegTask = ProcessRunner.RunAsync(
                        fileName: config.FfmpegPath,
                        arguments: $"-y -loglevel error -i \"{paths.SourceMediaPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{paths.WavPath}\"",
                        workingDirectory: paths.JobFolder,
                        suppressOutput: true);

                    while (!ffmpegTask.IsCompleted)
                    {
                        Console.Write(".");
                        await Task.Delay(1000);
                    }

                    await ffmpegTask;
                    Console.WriteLine(" done");

                    Console.WriteLine("Step 2/4 - Running WhisperX / diarization...");
                    var whisperX = config.WhisperX;
                    var whisperXExecutablePath = ResolveExecutablePath(whisperX.ExecutablePath, config.ConfigFilePath);
                    var whisperXArguments = BuildWhisperXArguments(paths, config);
                    var commandLine = $"{Quote(whisperXExecutablePath)} {whisperXArguments}";
                    var validationOutput = await ValidateWhisperXConfigurationAsync(whisperXExecutablePath, whisperX, paths.JobFolder);

                    var launchSummary = BuildWhisperXLaunchSummary(
                        config,
                        whisperXExecutablePath,
                        commandLine,
                        validationOutput);

                    foreach (var line in launchSummary)
                        Console.WriteLine(line);

                    var whisperXEnv = new Dictionary<string, string?>
                    {
                        ["HF_TOKEN"] = config.HuggingFaceToken
                    };

                    await ProcessRunner.RunWithStageProgressAsync(
                        fileName: whisperXExecutablePath,
                        arguments: whisperXArguments,
                        workingDirectory: paths.JobFolder,
                        environmentVariables: whisperXEnv,
                        logFilePath: paths.PythonLogPath,
                        logPreambleLines: launchSummary);

                    NormalizeWhisperXOutputPath(paths);

                    if (!File.Exists(paths.RawJsonPath))
                    {
                        throw new FileNotFoundException(
                            $"WhisperX completed but did not create the JSON output file:{Environment.NewLine}" +
                            $"{paths.RawJsonPath}{Environment.NewLine}{Environment.NewLine}" +
                            $"Check the WhisperX log here:{Environment.NewLine}" +
                            $"{paths.PythonLogPath}");
                    }
                }
                else
                {
                    Console.WriteLine("Step 1/4 - Skipped (using existing output)");
                    Console.WriteLine("Step 2/4 - Skipped (using existing diarized JSON)");
                }

                Console.WriteLine("Step 3/4 - Reading diarized JSON...");
                var transcript = JsonFileService.ReadJson<WhisperXResult>(paths.RawJsonPath);

                if (transcript?.Segments is null || transcript.Segments.Count == 0)
                {
                    Console.WriteLine("No segments found in the JSON.");
                    return 1;
                }

                Console.WriteLine($"Raw segments: {transcript.Segments.Count}");

                var correctionState = CorrectionStateStore.Load(paths.CorrectionStatePath);
                var correctionWorkspace = CorrectionConsoleWorkflowService.RunInteractiveReview(
                    transcript.Segments,
                    correctionState,
                    paths.CorrectionStatePath,
                    paths.SourceMediaPath,
                    config.VlcPath);
                CorrectionStateStore.Save(paths.CorrectionStatePath, correctionWorkspace.CorrectionState);

                var speakerMap = CorrectionTranscriptProjectionService.BuildBucketSpeakerMap(correctionWorkspace);
                JsonFileService.WriteJson(paths.SpeakerMapPath, speakerMap);

                var correctedSegments = CorrectionTranscriptProjectionService.BuildCorrectedSegments(
                    transcript.Segments,
                    correctionWorkspace);

                var transcriptSpeakerMap = CorrectionTranscriptProjectionService.BuildIdentitySpeakerMap(correctedSegments);

                var options = config.CleaningOptions;

                var cleaned = TranscriptCleaner.CleanSegments(correctedSegments, options);

                var cleanTranscript = TranscriptFormatter.ToReadableTranscript(cleaned, transcriptSpeakerMap, options);
                File.WriteAllText(paths.CleanTranscriptPath, cleanTranscript, Encoding.UTF8);

                var reviewOptions = config.CleaningOptions.Clone();
                reviewOptions.IncludeEndTime = true;
                reviewOptions.DropLikelyNoise = false;
                reviewOptions.MinDurationSeconds = 0.0;
                reviewOptions.MinTextLength = 1;

                var reviewTranscript = TranscriptFormatter.ToReadableTranscript(
                    correctedSegments.OrderBy(s => s.Start).ToList(),
                    transcriptSpeakerMap,
                    reviewOptions);

                File.WriteAllText(paths.ReviewTranscriptPath, reviewTranscript, Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine("Done.");
                Console.WriteLine($"Clean transcript : {paths.CleanTranscriptPath}");
                Console.WriteLine($"Review transcript: {paths.ReviewTranscriptPath}");
                Console.WriteLine($"Speaker map      : {paths.SpeakerMapPath}");
                Console.WriteLine($"Corrections      : {paths.CorrectionStatePath}");
                Console.WriteLine($"Raw JSON         : {paths.RawJsonPath}");
                Console.WriteLine($"WAV              : {paths.WavPath}");

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("An error occurred:");
                Console.WriteLine(ex);
                return 1;
            }
        }

        private static PipelineStartMode GetPipelineStartMode(PipelinePaths paths)
        {
            var hasOutputFolder = Directory.Exists(paths.JobFolder);
            var hasJson = File.Exists(paths.RawJsonPath);

            if (!hasOutputFolder || !hasJson)
                return PipelineStartMode.FullRun;

            Console.WriteLine();
            Console.WriteLine("Existing output detected for this recording.");
            Console.WriteLine("1. Re-run full pipeline");
            Console.WriteLine("2. Resume from existing diarized JSON");
            Console.WriteLine("3. Cancel");

            while (true)
            {
                Console.Write("Select option (1-3): ");
                var input = Console.ReadLine()?.Trim();

                if (input == "1")
                    return PipelineStartMode.FullRun;

                if (input == "2")
                    return PipelineStartMode.ResumeFromExistingJson;

                if (input == "3")
                    return PipelineStartMode.Cancel;

                Console.WriteLine("Invalid selection. Try again.");
            }
        }

        private static InputSelection SelectInputFromFolder()
        {
            Console.Write("Enter folder path containing recordings: ");
            var folder = Console.ReadLine()?.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                throw new InvalidOperationException("Invalid folder path.");

            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".mp4", ".wav", ".mp3", ".m4a", ".mov", ".wmv"
            };

            var files = Directory
                .GetFiles(folder)
                .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => f)
                .ToList();

            if (files.Count == 0)
                throw new InvalidOperationException("No supported media files found in folder.");

            Console.WriteLine();
            Console.WriteLine("Available recordings:");

            for (int i = 0; i < files.Count; i++)
            {
                var fileInfo = new FileInfo(files[i]);
                Console.WriteLine($"{i + 1}. {fileInfo.Name} ({FormatFileSize(fileInfo.Length)})");
            }

            Console.WriteLine();

            while (true)
            {
                Console.Write($"Select recording (1-{files.Count}): ");
                var input = Console.ReadLine();

                if (int.TryParse(input, out var index) &&
                    index >= 1 &&
                    index <= files.Count)
                {
                    return new InputSelection(folder, files[index - 1]);
                }

                Console.WriteLine("Invalid selection. Try again.");
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        private static string BuildWhisperXArguments(PipelinePaths paths, AppConfig config)
        {
            var whisperX = config.WhisperX;
            var arguments = new List<string>
            {
                Quote(paths.WavPath),
                "--output_dir",
                Quote(paths.JobFolder),
                "--output_format",
                "json",
                "--diarize",
                "--model",
                Quote(whisperX.Model),
                "--device",
                Quote(whisperX.Device),
                "--compute_type",
                Quote(whisperX.ComputeType),
                "--batch_size",
                whisperX.BatchSize.ToString()
            };

            if (!string.IsNullOrWhiteSpace(config.TranscriptionLanguage))
            {
                arguments.Add("--language");
                arguments.Add(Quote(config.TranscriptionLanguage));
            }

            return string.Join(" ", arguments);
        }

        private static string ResolveExecutablePath(string configuredPath, string? configFilePath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                throw new InvalidOperationException("WhisperX executable path is not configured.");

            if (Path.IsPathRooted(configuredPath))
                return Path.GetFullPath(configuredPath);

            var solutionDirectory = FindAncestorContainingFile(Environment.CurrentDirectory, "MinuteMaker.slnx") ??
                FindAncestorContainingFile(AppContext.BaseDirectory, "MinuteMaker.slnx");

            var projectDirectory = FindAncestorContainingFile(Environment.CurrentDirectory, "MinuteMaker.csproj") ??
                FindAncestorContainingFile(AppContext.BaseDirectory, "MinuteMaker.csproj");

            if (!ContainsDirectorySeparator(configuredPath))
            {
                var pathExecutable = ResolveExecutableFromPath(configuredPath);
                return pathExecutable ?? configuredPath;
            }

            foreach (var baseDirectory in GetExecutableBaseDirectories(configFilePath, solutionDirectory, projectDirectory))
            {
                var candidatePath = Path.GetFullPath(configuredPath, baseDirectory);
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            var preferredBaseDirectory = solutionDirectory ??
                projectDirectory ??
                Path.GetDirectoryName(configFilePath ?? string.Empty) ??
                Environment.CurrentDirectory;

            return Path.GetFullPath(configuredPath, preferredBaseDirectory);
        }

        private static async Task<string> ValidateWhisperXConfigurationAsync(
            string executablePath,
            WhisperXExecutionOptions whisperX,
            string workingDirectory)
        {
            if (!string.Equals(whisperX.Device, "cuda", StringComparison.OrdinalIgnoreCase))
                return "CUDA validation: skipped because Device is not cuda.";

            if (string.Equals(whisperX.ExecutablePath, "whisperx", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("WARNING: Device is cuda but WhisperX ExecutablePath is just 'whisperx'. This may resolve to a global CPU install.");
            }

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    $"CUDA mode is configured but the WhisperX executable was not found: {executablePath}");
            }

            var pythonPath = Path.Combine(Path.GetDirectoryName(executablePath)!, "python.exe");
            if (!File.Exists(pythonPath))
            {
                throw new FileNotFoundException(
                    $"CUDA mode is configured but python.exe was not found next to WhisperX: {pythonPath}");
            }

            var result = await ProcessRunner.RunAndCaptureAsync(
                fileName: pythonPath,
                arguments: "-c \"import sys, torch; print(sys.executable); print(torch.__version__); print(torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'No CUDA')\"",
                workingDirectory: workingDirectory);

            var validationOutput = new StringBuilder();
            validationOutput.AppendLine("CUDA validation:");
            validationOutput.AppendLine($"  python.exe : {pythonPath}");
            validationOutput.AppendLine("  command    : " + Quote(pythonPath) + " -c \"import sys, torch; print(sys.executable); print(torch.__version__); print(torch.cuda.is_available()); print(torch.cuda.get_device_name(0) if torch.cuda.is_available() else 'No CUDA')\"");

            foreach (var line in ReadNonEmptyLines(result.StandardOutput))
                validationOutput.AppendLine($"  stdout     : {line}");

            foreach (var line in ReadNonEmptyLines(result.StandardError))
                validationOutput.AppendLine($"  stderr     : {line}");

            var stdoutLines = ReadNonEmptyLines(result.StandardOutput).ToList();
            var cudaAvailable = stdoutLines.Count >= 3 ? stdoutLines[2] : string.Empty;
            if (result.ExitCode != 0 || !string.Equals(cudaAvailable, "True", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "CUDA mode is configured, but PyTorch did not report CUDA availability. " +
                    $"Expected 'True' from torch.cuda.is_available(), got '{cudaAvailable}'.{Environment.NewLine}" +
                    validationOutput);
            }

            return validationOutput.ToString().TrimEnd();
        }

        private static IReadOnlyList<string> BuildWhisperXLaunchSummary(
            AppConfig config,
            string whisperXExecutablePath,
            string commandLine,
            string validationOutput)
        {
            var whisperX = config.WhisperX;
            var lines = new List<string>
            {
                "WhisperX configuration:",
                $"  config path      : {config.ConfigFilePath}",
                $"  config exists    : {config.ConfigFileExists}",
                $"  executable input : {whisperX.ExecutablePath}",
                $"  executable final : {whisperXExecutablePath}",
                $"  model            : {whisperX.Model}",
                $"  device           : {whisperX.Device}",
                $"  compute type     : {whisperX.ComputeType}",
                $"  batch size       : {whisperX.BatchSize}",
                $"  command          : {commandLine}"
            };

            if (string.Equals(whisperX.Device, "cuda", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(whisperX.ExecutablePath, "whisperx", StringComparison.OrdinalIgnoreCase))
            {
                lines.Add("  warning          : Device is cuda but ExecutablePath is just 'whisperx'; this may resolve to a global CPU install.");
            }

            lines.AddRange(validationOutput.Split(
                new[] { "\r\n", "\n" },
                StringSplitOptions.RemoveEmptyEntries));

            return lines;
        }

        private static IEnumerable<string> GetExecutableBaseDirectories(
            string? configFilePath,
            string? solutionDirectory,
            string? projectDirectory)
        {
            yield return Environment.CurrentDirectory;

            if (solutionDirectory is not null)
                yield return solutionDirectory;

            if (!string.IsNullOrWhiteSpace(configFilePath))
            {
                var configDirectory = Path.GetDirectoryName(configFilePath);
                if (!string.IsNullOrWhiteSpace(configDirectory))
                    yield return configDirectory;
            }

            if (projectDirectory is not null)
                yield return projectDirectory;

            yield return AppContext.BaseDirectory;
        }

        private static string? ResolveExecutableFromPath(string executablePath)
        {
            var pathValue = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrWhiteSpace(pathValue))
                return null;

            var candidateNames = Path.HasExtension(executablePath)
                ? new[] { executablePath }
                : new[] { executablePath, executablePath + ".exe" };

            foreach (var pathEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var candidateName in candidateNames)
                {
                    var candidatePath = Path.Combine(pathEntry, candidateName);
                    if (File.Exists(candidatePath))
                        return candidatePath;
                }
            }

            return null;
        }

        private static string? FindAncestorContainingFile(string startDirectory, string fileName)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, fileName)))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }

        private static IEnumerable<string> ReadNonEmptyLines(string value) =>
            value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line));

        private static void NormalizeWhisperXOutputPath(PipelinePaths paths)
        {
            if (File.Exists(paths.RawJsonPath))
                return;

            var whisperXJsonPath = Path.Combine(
                paths.JobFolder,
                Path.ChangeExtension(Path.GetFileName(paths.WavPath), ".json"));

            if (File.Exists(whisperXJsonPath))
                File.Copy(whisperXJsonPath, paths.RawJsonPath, overwrite: true);
        }

        private static bool ContainsDirectorySeparator(string path) =>
            path.Contains(Path.DirectorySeparatorChar) ||
            path.Contains(Path.AltDirectorySeparatorChar);

        private static string Quote(string value) =>
            "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}
