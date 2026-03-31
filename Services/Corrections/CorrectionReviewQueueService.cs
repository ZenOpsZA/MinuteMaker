using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionReviewQueueService
    {
        public static List<ReviewItem> BuildQueue(
            SpeakerCorrectionWorkspace workspace,
            ReviewQueueRequest request)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(request);

            IEnumerable<ReviewItem> items = workspace.ReviewRuns
                .SelectMany(x => x.Items);

            items = request.Mode switch
            {
                ReviewFilterMode.SuspiciousOnly => items.Where(x => x.IsSuspicious),
                ReviewFilterMode.UnassignedOnly => items.Where(IsUnassigned),
                ReviewFilterMode.SpecificBucket => items.Where(x =>
                    string.Equals(x.BucketId, request.BucketId, StringComparison.OrdinalIgnoreCase)),
                ReviewFilterMode.AllItems => items,
                _ => items
            };

            if (request.OnlySkippedItems)
            {
                items = items.Where(x => workspace.CorrectionState.Session.SkippedItemIds
                    .Contains(x.ItemId, StringComparer.OrdinalIgnoreCase));
            }

            if (!request.IncludeReviewedItems)
            {
                items = items.Where(x => !workspace.CorrectionState.Session.ReviewedItemIds
                    .Contains(x.ItemId, StringComparer.OrdinalIgnoreCase));
            }

            return items
                .OrderBy(x => x.StartSeconds)
                .ThenBy(x => x.SegmentIndex)
                .ToList();
        }

        public static List<ReviewItem> GetRemainingItems(
            IReadOnlyList<ReviewItem> queue,
            int currentIndex)
        {
            if (queue.Count == 0 || currentIndex < 0 || currentIndex >= queue.Count)
                return new List<ReviewItem>();

            return queue
                .Skip(currentIndex)
                .ToList();
        }

        public static List<ReviewItem> GetRemainingItemsInBucket(
            IReadOnlyList<ReviewItem> queue,
            int currentIndex,
            string bucketId)
        {
            return GetRemainingItems(queue, currentIndex)
                .Where(x => string.Equals(x.BucketId, bucketId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public static List<ReviewItem> GetItemsMatchingCurrentSpeaker(
            SpeakerCorrectionWorkspace workspace,
            string effectiveSpeakerLabel)
        {
            return workspace.ReviewRuns
                .SelectMany(x => x.Items)
                .Where(x => string.Equals(x.EffectiveSpeakerLabel, effectiveSpeakerLabel, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.StartSeconds)
                .ThenBy(x => x.SegmentIndex)
                .ToList();
        }

        private static bool IsUnassigned(ReviewItem item)
        {
            return string.Equals(item.EffectiveSpeakerLabel, SpeakerAssignmentValues.Unknown, StringComparison.OrdinalIgnoreCase);
        }
    }
}
