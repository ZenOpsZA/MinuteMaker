using System;
using System.Collections.Generic;
using System.Text;

namespace MinuteMaker
{
    public static class TranscriptFormatter
    {
        public static string ToReadableTranscript(
            IReadOnlyList<TranscriptSegment> segments,
            IReadOnlyDictionary<string, string> speakerMap,
            CleaningOptions options)
        {
            var sb = new StringBuilder();

            foreach (var seg in segments)
            {
                var displaySpeaker = speakerMap.TryGetValue(seg.Speaker, out var mapped)
                    ? mapped
                    : (options.KeepUnknownSpeakers ? seg.Speaker : "Unknown");

                var startText = FormatTimestamp(seg.Start);

                if (options.IncludeEndTime)
                {
                    var endText = FormatTimestamp(seg.End);
                    sb.AppendLine($"[{startText} - {endText}] {displaySpeaker}:");
                }
                else
                {
                    sb.AppendLine($"[{startText}] {displaySpeaker}:");
                }

                sb.AppendLine(seg.Text.Trim());
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public static string FormatTimestamp(double seconds)
        {
            if (seconds < 0)
                seconds = 0;

            var ts = TimeSpan.FromSeconds(seconds);

            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

            return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
    }
}
