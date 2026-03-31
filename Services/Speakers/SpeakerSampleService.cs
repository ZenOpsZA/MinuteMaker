using MinuteMaker.Models.Speakers;
using MinuteMaker.Models.Transcription;

namespace MinuteMaker.Services.Speakers
{
    public static class SpeakerSampleService
    {
        public static Dictionary<string, SpeakerSample> BuildSamples(
            IEnumerable<TranscriptSegment> segments)
        {
            var result = new Dictionary<string, SpeakerSample>(StringComparer.OrdinalIgnoreCase);

            var grouped = segments
                .Where(s => !string.IsNullOrWhiteSpace(s.Speaker))
                .GroupBy(s => s.Speaker!, StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var best = group
                    .Where(IsGoodCandidate)
                    .OrderByDescending(Score)
                    .FirstOrDefault();

                if (best is null)
                {
                    best = group
                        .OrderByDescending(s => s.End - s.Start)
                        .FirstOrDefault();
                }

                if (best is null)
                    continue;

                result[group.Key] = new SpeakerSample
                {
                    SpeakerId = group.Key,
                    StartSeconds = best.Start,
                    EndSeconds = best.End,
                    TextPreview = BuildPreview(best.Text)
                };
            }

            return result;
        }

        private static bool IsGoodCandidate(TranscriptSegment segment)
        {
            var text = segment.Text?.Trim() ?? string.Empty;
            var duration = segment.End - segment.Start;

            if (duration < 2.0)
                return false;

            if (text.Length < 12)
                return false;

            if (IsLikelyNoise(text))
                return false;

            return true;
        }

        private static double Score(TranscriptSegment segment)
        {
            var duration = Math.Max(0, segment.End - segment.Start);
            var textLength = segment.Text?.Trim().Length ?? 0;

            return duration * 10 + textLength;
        }

        private static bool IsLikelyNoise(string text)
        {
            var normalized = text.Trim().ToLowerInvariant();

            return normalized is
                "um" or "uh" or "mm" or "hmm" or "hm" or
                "yes" or "yeah" or "yep" or "no" or "okay" or "ok" or
                "right" or "sure";
        }

        private static string BuildPreview(string? text)
        {
            var value = (text ?? string.Empty).Trim();

            if (value.Length <= 60)
                return value;

            return value[..57] + "...";
        }
    }
}
