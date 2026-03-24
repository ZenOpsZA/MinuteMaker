using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MinuteMaker
{
    public static class TranscriptCleaner
    {
        public static List<TranscriptSegment> CleanSegments(
            IEnumerable<TranscriptSegment> rawSegments,
            CleaningOptions options)
        {
            var cleaned = new List<TranscriptSegment>();

            foreach (var raw in rawSegments.OrderBy(s => s.Start))
            {
                if (raw is null)
                    continue;

                var text = raw.Text?.Trim() ?? string.Empty;
                var speaker = string.IsNullOrWhiteSpace(raw.Speaker) ? "UNKNOWN" : raw.Speaker.Trim();
                var start = raw.Start;
                var end = raw.End;

                if (end < start)
                    (start, end) = (end, start);

                var duration = end - start;

                text = NormalizeText(text, options);

                if (ShouldDropSegment(text, duration, options))
                    continue;

                var candidate = new TranscriptSegment
                {
                    Start = start,
                    End = end,
                    Speaker = speaker,
                    Text = text
                };

                if (cleaned.Count == 0)
                {
                    cleaned.Add(candidate);
                    continue;
                }

                var last = cleaned[^1];

                if (CanMerge(last, candidate, options))
                {
                    last.Text = MergeText(last.Text, candidate.Text);
                    last.End = Math.Max(last.End, candidate.End);
                }
                else
                {
                    cleaned.Add(candidate);
                }
            }

            return cleaned;
        }

        private static bool CanMerge(
            TranscriptSegment previous,
            TranscriptSegment current,
            CleaningOptions options)
        {
            if (!string.Equals(previous.Speaker, current.Speaker, StringComparison.OrdinalIgnoreCase))
                return false;

            var gap = current.Start - previous.End;
            if (gap > options.MergeGapSeconds)
                return false;

            return true;
        }

        private static string MergeText(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return right.Trim();

            if (string.IsNullOrWhiteSpace(right))
                return left.Trim();

            var needsSpace =
                !left.EndsWith(' ') &&
                !right.StartsWith(' ') &&
                !EndsWithOpeningPunctuation(left) &&
                !StartsWithClosingPunctuation(right);

            return needsSpace
                ? $"{left.Trim()} {right.Trim()}"
                : $"{left.Trim()}{right.Trim()}";
        }

        private static bool EndsWithOpeningPunctuation(string text) =>
            text.EndsWith('(') || text.EndsWith('[') || text.EndsWith('"');

        private static bool StartsWithClosingPunctuation(string text) =>
            text.StartsWith('.') || text.StartsWith(',') || text.StartsWith('!') ||
            text.StartsWith('?') || text.StartsWith(':') || text.StartsWith(';');

        private static bool ShouldDropSegment(
            string text,
            double duration,
            CleaningOptions options)
        {
            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (duration < options.MinDurationSeconds)
                return true;

            if (text.Length < options.MinTextLength)
                return true;

            if (!options.DropLikelyNoise)
                return false;

            if (LikelyNoise(text))
                return true;

            return false;
        }

        private static bool LikelyNoise(string text)
        {
            var lower = text.Trim().ToLowerInvariant();

            // Common noise / fragments / placeholders.
            var exactNoise = new HashSet<string>
        {
            "you",
            "you.",
            "okay",
            "ok",
            "ok.",
            "yes",
            "yes.",
            "yeah",
            "yeah.",
            "right",
            "right.",
            "100%",
            "100%.",
            "mm",
            "mmm",
            "uh",
            "uh.",
            "um",
            "um.",
            "hmm",
            "hmm."
        };

            if (exactNoise.Contains(lower))
                return true;

            // Only punctuation or symbols.
            if (Regex.IsMatch(lower, @"^[\p{P}\p{S}\d\s]+$"))
                return true;

            // Very short single token that is often junk.
            var tokenCount = lower
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

            if (tokenCount == 1 && lower.Length <= 4)
                return true;

            return false;
        }

        private static string NormalizeText(string text, CleaningOptions options)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var result = text.Trim();

            if (options.NormalizeWhitespace)
            {
                result = Regex.Replace(result, @"\s+", " ");
            }

            if (options.RemoveRepeatedPunctuation)
            {
                result = Regex.Replace(result, @"([!?.,])\1{1,}", "$1");
                result = Regex.Replace(result, @"\s+([.,!?;:])", "$1");
            }

            return result.Trim();
        }
    }

}
