using System.Text.Json.Serialization;

namespace MinuteMaker.Models.Transcription
{
    public sealed class WhisperXResult
    {
        [JsonPropertyName("segments")]
        public List<TranscriptSegment> Segments { get; set; } = new();
    }
}
