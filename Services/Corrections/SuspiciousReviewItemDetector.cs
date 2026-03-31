using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class SuspiciousReviewItemDetector
    {
        public static List<ReviewItem> MarkSuspiciousItems(IReadOnlyList<ReviewRun> runs)
        {
            ArgumentNullException.ThrowIfNull(runs);

            var allItems = runs
                .SelectMany(x => x.Items)
                .OrderBy(x => x.StartSeconds)
                .ThenBy(x => x.SegmentIndex)
                .ToList();

            for (int i = 0; i < allItems.Count; i++)
            {
                var current = allItems[i];
                var previous = i > 0 ? allItems[i - 1] : null;
                var next = i < allItems.Count - 1 ? allItems[i + 1] : null;

                AddShortSegmentFlag(current);
                AddRapidSwitchFlag(current, previous, next);
                AddMidSentenceChangeFlag(current, next);
            }

            foreach (var run in runs)
            {
                AddFragmentedRunFlags(run);
            }

            return allItems
                .Where(x => x.IsSuspicious)
                .ToList();
        }

        private static void AddShortSegmentFlag(ReviewItem item)
        {
            var duration = Math.Max(0, item.EndSeconds - item.StartSeconds);
            if (duration < CorrectionWorkspaceDefaults.ShortSegmentSeconds)
            {
                AddFlag(item, ReviewItemFlags.ShortSegment);
            }
        }

        private static void AddRapidSwitchFlag(ReviewItem item, ReviewItem? previous, ReviewItem? next)
        {
            if (HasRapidSwitch(item, previous) || HasRapidSwitch(item, next))
            {
                AddFlag(item, ReviewItemFlags.RapidSpeakerSwitch);
            }
        }

        private static bool HasRapidSwitch(ReviewItem item, ReviewItem? other)
        {
            if (other is null)
                return false;

            if (string.Equals(item.EffectiveSpeakerLabel, other.EffectiveSpeakerLabel, StringComparison.OrdinalIgnoreCase))
                return false;

            var gap = Math.Abs(item.StartSeconds - other.EndSeconds);
            if (other.StartSeconds > item.EndSeconds)
            {
                gap = other.StartSeconds - item.EndSeconds;
            }

            return gap <= CorrectionWorkspaceDefaults.RapidSpeakerSwitchGapSeconds;
        }

        private static void AddMidSentenceChangeFlag(ReviewItem current, ReviewItem? next)
        {
            if (next is null)
                return;

            if (string.Equals(current.EffectiveSpeakerLabel, next.EffectiveSpeakerLabel, StringComparison.OrdinalIgnoreCase))
                return;

            var gap = next.StartSeconds - current.EndSeconds;
            if (gap > CorrectionWorkspaceDefaults.MidSentenceGapSeconds)
                return;

            var currentText = (current.Text ?? string.Empty).Trim();
            var nextText = (next.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(currentText) || string.IsNullOrWhiteSpace(nextText))
                return;

            var currentEndsSentence = EndsSentence(currentText);
            var nextStartsLower = char.IsLower(nextText[0]);

            if (!currentEndsSentence || nextStartsLower)
            {
                AddFlag(current, ReviewItemFlags.MidSentenceSpeakerChange);
                AddFlag(next, ReviewItemFlags.MidSentenceSpeakerChange);
            }
        }

        private static void AddFragmentedRunFlags(ReviewRun run)
        {
            if (run.SegmentCount != 1)
                return;

            if (run.TotalDurationSeconds > CorrectionWorkspaceDefaults.FragmentedRunMaxDurationSeconds)
                return;

            AddFlag(run.Items[0], ReviewItemFlags.FragmentedRun);
        }

        private static bool EndsSentence(string text)
        {
            var lastChar = text[^1];
            return lastChar is '.' or '!' or '?' or ':' or ';';
        }

        private static void AddFlag(ReviewItem item, string flag)
        {
            if (item.ReviewFlags.Contains(flag, StringComparer.OrdinalIgnoreCase))
                return;

            item.ReviewFlags.Add(flag);
            item.IsSuspicious = true;
        }
    }
}
