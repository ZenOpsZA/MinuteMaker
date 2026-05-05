using System.Text.Json;

namespace MinuteMaker.Configuration
{
    public sealed class AppConfig
    {
        public string? ConfigFilePath { get; set; }
        public bool ConfigFileExists { get; set; }

        public string FfmpegPath { get; set; } = "ffmpeg";
        public string PythonExePath { get; set; } = "python";
        public string PythonScriptPath { get; set; } =
            Path.Combine(AppContext.BaseDirectory, "Integrations", "Python", "transcribe_diarize.py");

        public string? VlcPath { get; set; } = null;

        public string WhisperModel { get; set; } = "base";
        public string Device { get; set; } = "cpu";
        public string ComputeType { get; set; } = "int8";
        public WhisperXExecutionOptions WhisperX { get; set; } = new();

        public string? HuggingFaceToken { get; set; } =
            Environment.GetEnvironmentVariable("HF_TOKEN");

        public CleaningOptions CleaningOptions { get; set; } = new();

        public string TranscriptionLanguage { get; set; } = "en";

        public static AppConfig CreateDefault()
        {
            var config = new AppConfig
            {
                WhisperX = new WhisperXExecutionOptions(),
                CleaningOptions = new CleaningOptions
                {
                    MinDurationSeconds = 0.45,
                    MinTextLength = 4,
                    MergeGapSeconds = 1.50,
                    DropLikelyNoise = true,
                    NormalizeWhitespace = true,
                    RemoveRepeatedPunctuation = true,
                    IncludeEndTime = false,
                    KeepUnknownSpeakers = true
                }
            };

            config.SyncWhisperXFromLegacyDefaults();
            return config;
        }

        public static AppConfig Load()
        {
            var config = CreateDefault();
            var configPath = ResolveConfigPath();
            config.ConfigFilePath = configPath ?? Path.Combine(Environment.CurrentDirectory, "appsettings.json");
            config.ConfigFileExists = configPath is not null;

            if (configPath is null)
                return config;

            using var stream = File.OpenRead(configPath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            ApplyString(root, nameof(FfmpegPath), value => config.FfmpegPath = value ?? config.FfmpegPath);
            ApplyString(root, nameof(PythonExePath), value => config.PythonExePath = value ?? config.PythonExePath);
            ApplyString(root, nameof(PythonScriptPath), value => config.PythonScriptPath = value ?? config.PythonScriptPath);
            ApplyString(root, nameof(VlcPath), value => config.VlcPath = value);
            ApplyString(root, nameof(HuggingFaceToken), value => config.HuggingFaceToken = value);
            ApplyString(root, nameof(TranscriptionLanguage), value => config.TranscriptionLanguage = value ?? config.TranscriptionLanguage);

            var legacyWhisperModelSet = ApplyString(root, nameof(WhisperModel), value => config.WhisperModel = value ?? config.WhisperModel);
            var legacyDeviceSet = ApplyString(root, nameof(Device), value => config.Device = value ?? config.Device);
            var legacyComputeTypeSet = ApplyString(root, nameof(ComputeType), value => config.ComputeType = value ?? config.ComputeType);

            if (root.TryGetProperty(nameof(WhisperX), out var whisperXElement) &&
                whisperXElement.ValueKind == JsonValueKind.Object)
            {
                ApplyString(whisperXElement, nameof(WhisperXExecutionOptions.ExecutablePath), value => config.WhisperX.ExecutablePath = value ?? config.WhisperX.ExecutablePath);
                ApplyString(whisperXElement, nameof(WhisperXExecutionOptions.Model), value => config.WhisperX.Model = value ?? config.WhisperX.Model);
                ApplyString(whisperXElement, nameof(WhisperXExecutionOptions.Device), value => config.WhisperX.Device = value ?? config.WhisperX.Device);
                ApplyString(whisperXElement, nameof(WhisperXExecutionOptions.ComputeType), value => config.WhisperX.ComputeType = value ?? config.WhisperX.ComputeType);
                ApplyInt(whisperXElement, nameof(WhisperXExecutionOptions.BatchSize), value => config.WhisperX.BatchSize = value);
            }
            else
            {
                if (legacyWhisperModelSet)
                    config.WhisperX.Model = config.WhisperModel;

                if (legacyDeviceSet)
                    config.WhisperX.Device = config.Device;

                if (legacyComputeTypeSet)
                    config.WhisperX.ComputeType = config.ComputeType;
            }

            return config;
        }

        private static string? ResolveConfigPath()
        {
            foreach (var candidatePath in GetConfigCandidatePaths())
            {
                if (File.Exists(candidatePath))
                    return candidatePath;
            }

            return null;
        }

        private static IEnumerable<string> GetConfigCandidatePaths()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var basePath in GetConfigCandidateDirectories())
            {
                var candidatePath = Path.GetFullPath(Path.Combine(basePath, "appsettings.json"));
                if (seen.Add(candidatePath))
                    yield return candidatePath;
            }
        }

        private static IEnumerable<string> GetConfigCandidateDirectories()
        {
            yield return Environment.CurrentDirectory;
            yield return Path.Combine(Environment.CurrentDirectory, "MinuteMaker");

            var projectDirectory = FindAncestorContainingFile(Environment.CurrentDirectory, "MinuteMaker.csproj") ??
                FindAncestorContainingFile(AppContext.BaseDirectory, "MinuteMaker.csproj");

            if (projectDirectory is not null)
                yield return projectDirectory;

            var solutionDirectory = FindAncestorContainingFile(Environment.CurrentDirectory, "MinuteMaker.slnx") ??
                FindAncestorContainingFile(AppContext.BaseDirectory, "MinuteMaker.slnx");

            if (solutionDirectory is not null)
            {
                yield return solutionDirectory;
                yield return Path.Combine(solutionDirectory, "MinuteMaker");
            }

            yield return AppContext.BaseDirectory;
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

        private void SyncWhisperXFromLegacyDefaults()
        {
            WhisperX.Model = WhisperModel;
            WhisperX.Device = Device;
            WhisperX.ComputeType = ComputeType;
        }

        private static bool ApplyString(JsonElement element, string propertyName, Action<string?> apply)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return false;

            if (property.ValueKind == JsonValueKind.Null)
            {
                apply(null);
                return true;
            }

            if (property.ValueKind != JsonValueKind.String)
                return false;

            apply(property.GetString());
            return true;
        }

        private static bool ApplyInt(JsonElement element, string propertyName, Action<int> apply)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Number ||
                !property.TryGetInt32(out var value))
            {
                return false;
            }

            apply(value);
            return true;
        }
    }

    public sealed class WhisperXExecutionOptions
    {
        public string ExecutablePath { get; set; } = "whisperx";
        public string Model { get; set; } = "base";
        public string Device { get; set; } = "cpu";
        public string ComputeType { get; set; } = "int8";
        public int BatchSize { get; set; } = 4;
    }
}
