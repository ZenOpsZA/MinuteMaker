using MinuteMaker.MinuteMaker;
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
                var config = AppConfig.CreateDefault();

                Console.WriteLine("=== MinuteMaker ===");
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
                    var pythonEnv = new Dictionary<string, string?>
                    {
                        ["HF_TOKEN"] = config.HuggingFaceToken
                    };

                    await ProcessRunner.RunWithStageProgressAsync(
                        fileName: config.PythonExePath,
                        arguments:
                            $"\"{config.PythonScriptPath}\" " +
                            $"--input \"{paths.WavPath}\" " +
                            $"--output \"{paths.RawJsonPath}\" " +
                            $"--model \"{config.WhisperModel}\" " +
                            $"--device \"{config.Device}\" " +
                            $"--compute-type \"{config.ComputeType}\"",
                        workingDirectory: paths.JobFolder,
                        environmentVariables: pythonEnv,
                        logFilePath: paths.PythonLogPath);

                    if (!File.Exists(paths.RawJsonPath))
                    {
                        throw new FileNotFoundException(
                            $"Python completed but did not create the JSON output file:{Environment.NewLine}" +
                            $"{paths.RawJsonPath}{Environment.NewLine}{Environment.NewLine}" +
                            $"Check the Python log here:{Environment.NewLine}" +
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

                Console.WriteLine("Step 4/4 - Prompting for speaker names...");
                var rawSpeakers = transcript.Segments
                    .Select(s => string.IsNullOrWhiteSpace(s.Speaker) ? "UNKNOWN" : s.Speaker.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var speakerSamples = SpeakerSampleService.BuildSamples(transcript.Segments);

                var speakerMap = SpeakerMapService.BuildInteractiveMap(
                    rawSpeakers,
                    speakerSamples,
                    paths.SourceMediaPath,
                    config.VlcPath);

                JsonFileService.WriteJson(paths.SpeakerMapPath, speakerMap);

                var options = config.CleaningOptions;

                var cleaned = TranscriptCleaner.CleanSegments(transcript.Segments, options);

                var cleanTranscript = TranscriptFormatter.ToReadableTranscript(cleaned, speakerMap, options);
                File.WriteAllText(paths.CleanTranscriptPath, cleanTranscript, Encoding.UTF8);

                var reviewOptions = config.CleaningOptions.Clone();
                reviewOptions.IncludeEndTime = true;
                reviewOptions.DropLikelyNoise = false;
                reviewOptions.MinDurationSeconds = 0.0;
                reviewOptions.MinTextLength = 1;

                var reviewTranscript = TranscriptFormatter.ToReadableTranscript(
                    transcript.Segments.OrderBy(s => s.Start).ToList(),
                    speakerMap,
                    reviewOptions);

                File.WriteAllText(paths.ReviewTranscriptPath, reviewTranscript, Encoding.UTF8);

                Console.WriteLine();
                Console.WriteLine("Done.");
                Console.WriteLine($"Clean transcript : {paths.CleanTranscriptPath}");
                Console.WriteLine($"Review transcript: {paths.ReviewTranscriptPath}");
                Console.WriteLine($"Speaker map      : {paths.SpeakerMapPath}");
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
                ".mp4", ".wav", ".mp3", ".m4a", ".mov"
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
    }
}