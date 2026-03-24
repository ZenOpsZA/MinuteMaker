using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MinuteMaker
{
    public sealed class WhisperXResult
    {
        [JsonPropertyName("segments")]
        public List<TranscriptSegment> Segments { get; set; } = new();
    }
}
