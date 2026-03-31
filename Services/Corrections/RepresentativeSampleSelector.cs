using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class RepresentativeSampleSelector
    {
        public static List<RepresentativeSample> SelectSamples(
            IReadOnlyList<ReviewItem> items,
            int maxSamples = CorrectionWorkspaceDefaults.RepresentativeSamplesPerBucket)
        {
            ArgumentNullException.ThrowIfNull(items);

            var orderedItems = items
                .OrderBy(x => x.StartSeconds)
                .ToList();

            if (orderedItems.Count == 0 || maxSamples <= 0)
                return new List<RepresentativeSample>();

            var selected = new List<ReviewItem>();
            var windowCount = Math.Min(maxSamples, orderedItems.Count);

            for (int windowIndex = 0; windowIndex < windowCount; windowIndex++)
            {
                var start = windowIndex * orderedItems.Count / windowCount;
                var length = (windowIndex + 1) * orderedItems.Count / windowCount - start;
                var windowItems = orderedItems
                    .Skip(start)
                    .Take(length)
                    .OrderByDescending(Score)
                    .ThenBy(x => x.StartSeconds)
                    .ThenBy(x => x.SegmentIndex)
                    .ToList();

                var chosen = windowItems.FirstOrDefault(x => selected.All(y => y.ItemId != x.ItemId));
                if (chosen is not null)
                {
                    selected.Add(chosen);
                }
            }

            if (selected.Count < windowCount)
            {
                var remaining = orderedItems
                    .OrderByDescending(Score)
                    .ThenBy(x => x.StartSeconds)
                    .ThenBy(x => x.SegmentIndex);

                foreach (var item in remaining)
                {
                    if (selected.Any(x => x.ItemId == item.ItemId))
                        continue;

                    selected.Add(item);
                    if (selected.Count == windowCount)
                        break;
                }
            }

            return selected
                .OrderBy(x => x.StartSeconds)
                .Select(x => new RepresentativeSample
                {
                    SegmentIndex = x.SegmentIndex,
                    StartSeconds = x.StartSeconds,
                    EndSeconds = x.EndSeconds,
                    TextPreview = BuildPreview(x.Text)
                })
                .ToList();
        }

        private static double Score(ReviewItem item)
        {
            var duration = Math.Max(0, item.EndSeconds - item.StartSeconds);
            var textLength = (item.Text ?? string.Empty).Trim().Length;
            var penalty = item.IsSuspicious ? 5 : 0;

            return duration * 10 + textLength - penalty;
        }

        private static string BuildPreview(string? text)
        {
            var value = (text ?? string.Empty).Trim();

            if (value.Length <= 80)
                return value;

            return value[..77] + "...";
        }
    }
}
