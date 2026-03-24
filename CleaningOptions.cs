using System;
using System.Collections.Generic;
using System.Text;

namespace MinuteMaker
{
    public sealed class CleaningOptions
    {
        public double MinDurationSeconds { get; set; } = 0.45;
        public int MinTextLength { get; set; } = 4;
        public double MergeGapSeconds { get; set; } = 1.50;
        public bool DropLikelyNoise { get; set; } = true;
        public bool NormalizeWhitespace { get; set; } = true;
        public bool RemoveRepeatedPunctuation { get; set; } = true;
        public bool IncludeEndTime { get; set; } = false;
        public bool KeepUnknownSpeakers { get; set; } = true;

        public CleaningOptions Clone() =>
            new()
            {
                MinDurationSeconds = MinDurationSeconds,
                MinTextLength = MinTextLength,
                MergeGapSeconds = MergeGapSeconds,
                DropLikelyNoise = DropLikelyNoise,
                NormalizeWhitespace = NormalizeWhitespace,
                RemoveRepeatedPunctuation = RemoveRepeatedPunctuation,
                IncludeEndTime = IncludeEndTime,
                KeepUnknownSpeakers = KeepUnknownSpeakers
            };
    }
}
