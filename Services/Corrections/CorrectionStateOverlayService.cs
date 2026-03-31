using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionStateOverlayService
    {
        /// <summary>
        /// Resolves the speaker label to use for a review item.
        /// Intended precedence is:
        /// 1. Segment override
        /// 2. ReviewRun override
        /// 3. SpeakerBucket override
        /// 4. Raw speaker label
        ///
        /// Runtime support now applies all three manual override scopes using the documented precedence.
        /// </summary>
        public static string GetEffectiveSpeakerLabel(
            ReviewItem reviewItem,
            CorrectionState? correctionState)
        {
            ArgumentNullException.ThrowIfNull(reviewItem);

            return GetEffectiveSpeakerLabel(
                reviewItem.ItemId,
                reviewItem.RunId,
                reviewItem.BucketId,
                reviewItem.RawSpeakerLabel,
                correctionState);
        }

        public static string GetEffectiveSpeakerLabel(
            string reviewItemId,
            string? runId,
            string? bucketId,
            string rawSpeakerLabel,
            CorrectionState? correctionState)
        {
            if (correctionState?.Overrides is null || correctionState.Overrides.Count == 0)
                return rawSpeakerLabel;

            var segmentMatch = correctionState.Overrides
                .Where(x => x.Scope == CorrectionScope.Segment)
                .LastOrDefault(x => string.Equals(x.TargetId, reviewItemId, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(segmentMatch?.AssignedSpeaker))
                return segmentMatch.AssignedSpeaker;

            if (!string.IsNullOrWhiteSpace(runId))
            {
                var runMatch = correctionState.Overrides
                    .Where(x => x.Scope == CorrectionScope.ReviewRun)
                    .LastOrDefault(x => string.Equals(x.TargetId, runId, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(runMatch?.AssignedSpeaker))
                    return runMatch.AssignedSpeaker;
            }

            if (!string.IsNullOrWhiteSpace(bucketId))
            {
                var bucketMatch = correctionState.Overrides
                    .Where(x => x.Scope == CorrectionScope.SpeakerBucket)
                    .LastOrDefault(x => string.Equals(x.TargetId, bucketId, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(bucketMatch?.AssignedSpeaker))
                    return bucketMatch.AssignedSpeaker;
            }

            return rawSpeakerLabel;
        }

        public static string GetBucketAssignedSpeaker(
            string bucketId,
            string rawSpeakerLabel,
            CorrectionState? correctionState)
        {
            if (correctionState?.Overrides is null || correctionState.Overrides.Count == 0)
                return rawSpeakerLabel;

            var match = correctionState.Overrides
                .Where(x => x.Scope == CorrectionScope.SpeakerBucket)
                .LastOrDefault(x => string.Equals(x.TargetId, bucketId, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(match?.AssignedSpeaker)
                ? rawSpeakerLabel
                : match.AssignedSpeaker;
        }
    }
}
