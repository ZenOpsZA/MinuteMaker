using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class ReviewRunBuilder
    {
        public static List<ReviewRun> BuildRuns(IReadOnlyList<ReviewItem> orderedItems)
        {
            ArgumentNullException.ThrowIfNull(orderedItems);

            var runs = new List<ReviewRun>();
            if (orderedItems.Count == 0)
                return runs;

            var currentRunItems = new List<ReviewItem> { orderedItems[0] };

            for (int i = 1; i < orderedItems.Count; i++)
            {
                var previous = currentRunItems[^1];
                var current = orderedItems[i];

                if (ShouldMerge(previous, current))
                {
                    currentRunItems.Add(current);
                    continue;
                }

                runs.Add(CreateRun(currentRunItems));
                currentRunItems = new List<ReviewItem> { current };
            }

            runs.Add(CreateRun(currentRunItems));

            return runs;
        }

        private static bool ShouldMerge(ReviewItem previous, ReviewItem current)
        {
            if (!string.Equals(previous.EffectiveSpeakerLabel, current.EffectiveSpeakerLabel, StringComparison.OrdinalIgnoreCase))
                return false;

            var gap = current.StartSeconds - previous.EndSeconds;
            return gap <= CorrectionWorkspaceDefaults.RunMergeGapSeconds;
        }

        private static ReviewRun CreateRun(List<ReviewItem> items)
        {
            var runId = BuildRunId(items[0].StartSeconds, items[^1].EndSeconds);
            var bucketIds = items
                .Select(x => x.BucketId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var item in items)
            {
                item.RunId = runId;
            }

            return new ReviewRun
            {
                RunId = runId,
                SpeakerLabel = items[0].EffectiveSpeakerLabel,
                BucketIds = bucketIds,
                SegmentIndexes = items.Select(x => x.SegmentIndex).ToList(),
                SegmentCount = items.Count,
                TotalDurationSeconds = items.Sum(x => Math.Max(0, x.EndSeconds - x.StartSeconds)),
                StartSeconds = items[0].StartSeconds,
                EndSeconds = items[^1].EndSeconds,
                Items = items
            };
        }

        internal static string BuildRunId(double startSeconds, double endSeconds)
        {
            return $"run:{FormatSeconds(startSeconds)}-{FormatSeconds(endSeconds)}";
        }

        private static string FormatSeconds(double seconds)
        {
            if (seconds < 0)
                seconds = 0;

            var ts = TimeSpan.FromSeconds(seconds);
            var totalHours = (int)ts.TotalHours;
            return $"{totalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
    }
}
