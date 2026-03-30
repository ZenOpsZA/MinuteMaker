using System;
using System.Collections.Generic;
using System.Text;

namespace MinuteMaker
{
    public sealed class SpeakerSample
    {
        public string SpeakerId { get; init; } = string.Empty;
        public double StartSeconds { get; init; }
        public double EndSeconds { get; init; }
        public string TextPreview { get; init; } = string.Empty;
        public double DurationSeconds => EndSeconds - StartSeconds;
    }
}
