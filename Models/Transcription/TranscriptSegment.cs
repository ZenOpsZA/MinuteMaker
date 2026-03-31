using System.Text.Json.Serialization;

namespace MinuteMaker.Models.Transcription
{
    public sealed class TranscriptSegment
    {
        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("speaker")]
        public string Speaker { get; set; } = string.Empty;
    }
}
