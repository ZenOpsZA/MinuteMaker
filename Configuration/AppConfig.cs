namespace MinuteMaker.Configuration
{
    public sealed class AppConfig
    {
        public string FfmpegPath { get; set; } = "ffmpeg";
        public string PythonExePath { get; set; } = "python";
        public string PythonScriptPath { get; set; } =
            Path.Combine(AppContext.BaseDirectory, "Integrations", "Python", "transcribe_diarize.py");

        public string? VlcPath { get; set; } = null;

        public string WhisperModel { get; set; } = "base";
        public string Device { get; set; } = "cpu";
        public string ComputeType { get; set; } = "int8";

        public string? HuggingFaceToken { get; set; } =
            Environment.GetEnvironmentVariable("HF_TOKEN");

        public CleaningOptions CleaningOptions { get; set; } = new();

        public static AppConfig CreateDefault() =>
            new AppConfig
            {
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
    }
}
