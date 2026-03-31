using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionSessionStateService
    {
        public static bool HasExistingSession(CorrectionState correctionState)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            return correctionState.Overrides.Count > 0 ||
                   correctionState.Session.AssignedBucketIds.Count > 0 ||
                   correctionState.Session.ReviewedItemIds.Count > 0 ||
                   correctionState.Session.SkippedItemIds.Count > 0;
        }

        public static CorrectionSessionSummary BuildSummary(
            SpeakerCorrectionWorkspace workspace,
            CorrectionState correctionState)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(correctionState);

            var session = correctionState.Session;
            var suspiciousItemIds = workspace.SuspiciousReviewItems
                .Select(x => x.ItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var reviewedSuspiciousCount = session.ReviewedItemIds.Count(x => suspiciousItemIds.Contains(x));
            var skippedSuspiciousCount = session.SkippedItemIds.Count(x => suspiciousItemIds.Contains(x));
            var remainingSuspiciousCount = workspace.SuspiciousReviewItems.Count -
                                           workspace.SuspiciousReviewItems.Count(x =>
                                               session.ReviewedItemIds.Contains(x.ItemId, StringComparer.OrdinalIgnoreCase));

            return new CorrectionSessionSummary
            {
                OverrideCount = correctionState.Overrides.Count,
                AssignedBucketCount = correctionState.Session.AssignedBucketIds.Count,
                TotalBucketCount = workspace.BucketCount,
                ReviewedSuspiciousCount = reviewedSuspiciousCount,
                RemainingSuspiciousCount = Math.Max(0, remainingSuspiciousCount),
                SkippedSuspiciousCount = skippedSuspiciousCount
            };
        }

        public static void MarkBucketAssigned(CorrectionState correctionState, string bucketId)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            AddUnique(correctionState.Session.AssignedBucketIds, bucketId);
            correctionState.Session.LastBucketId = bucketId;
        }

        public static void MarkReviewed(CorrectionState correctionState, IEnumerable<string> itemIds)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            foreach (var itemId in itemIds)
            {
                AddUnique(correctionState.Session.ReviewedItemIds, itemId);
                RemoveIgnoreCase(correctionState.Session.SkippedItemIds, itemId);
                correctionState.Session.LastReviewItemId = itemId;
            }
        }

        public static void MarkSkipped(CorrectionState correctionState, string itemId)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            AddUnique(correctionState.Session.SkippedItemIds, itemId);
            correctionState.Session.LastReviewItemId = itemId;
        }

        public static void ClearSkipped(CorrectionState correctionState, IEnumerable<string> itemIds)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            foreach (var itemId in itemIds)
            {
                RemoveIgnoreCase(correctionState.Session.SkippedItemIds, itemId);
            }
        }

        public static void SetLastBucket(CorrectionState correctionState, string? bucketId)
        {
            ArgumentNullException.ThrowIfNull(correctionState);
            correctionState.Session.LastBucketId = bucketId;
        }

        public static void SetLastReviewItem(CorrectionState correctionState, string? itemId)
        {
            ArgumentNullException.ThrowIfNull(correctionState);
            correctionState.Session.LastReviewItemId = itemId;
        }

        public static void Reset(CorrectionState correctionState)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            correctionState.Overrides.Clear();
            correctionState.Session = new CorrectionSessionState();
            correctionState.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            if (values.Contains(value, StringComparer.OrdinalIgnoreCase))
                return;

            values.Add(value);
        }

        private static void RemoveIgnoreCase(List<string> values, string value)
        {
            values.RemoveAll(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        }
    }
}
