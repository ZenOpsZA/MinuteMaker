namespace MinuteMaker.Models.Pipeline
{
    public sealed class PipelinePaths
    {
        public string BaseFolder { get; init; } = string.Empty;
        public string JobFolder { get; init; } = string.Empty;
        public string SourceMediaPath { get; init; } = string.Empty;
        public string WavPath { get; init; } = string.Empty;
        public string RawJsonPath { get; init; } = string.Empty;
        public string CleanTranscriptPath { get; init; } = string.Empty;
        public string ReviewTranscriptPath { get; init; } = string.Empty;
        public string SpeakerMapPath { get; init; } = string.Empty;
        public string PythonLogPath { get; init; } = string.Empty;

        public static PipelinePaths Create(string selectedFolder, string originalInputPath)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(originalInputPath);
            var safeName = MakeSafeFolderName(nameWithoutExt);
            var jobFolder = Path.Combine(selectedFolder, $"{safeName}_output");

            return new PipelinePaths
            {
                BaseFolder = selectedFolder,
                JobFolder = jobFolder,
                SourceMediaPath = originalInputPath,
                WavPath = Path.Combine(jobFolder, "audio.wav"),
                RawJsonPath = Path.Combine(jobFolder, "output_speakers.json"),
                CleanTranscriptPath = Path.Combine(jobFolder, "transcript_clean.txt"),
                ReviewTranscriptPath = Path.Combine(jobFolder, "transcript_review.txt"),
                SpeakerMapPath = Path.Combine(jobFolder, "speaker-map.json"),
                PythonLogPath = Path.Combine(jobFolder, "python-output.log")
            };
        }

        private static string MakeSafeFolderName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
            return new string(chars);
        }
    }
}
