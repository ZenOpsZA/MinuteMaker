using MinuteMaker.Models.Corrections;
using MinuteMaker.Models.Transcription;

namespace MinuteMaker.Services.Corrections
{
    public static class SpeakerCorrectionWorkspaceFactory
    {
        public static SpeakerCorrectionWorkspace Create(
            IReadOnlyList<TranscriptSegment> segments,
            CorrectionState? correctionState = null)
        {
            ArgumentNullException.ThrowIfNull(segments);

            correctionState ??= new CorrectionState();

            var orderedSegments = segments
                .Select((segment, index) => new IndexedSegment(index, segment))
                .OrderBy(x => x.Segment.Start)
                .ThenBy(x => x.Index)
                .ToList();

            var reviewItems = orderedSegments
                .Select(CreateReviewItem)
                .ToList();

            var reviewRuns = ReviewRunBuilder.BuildRuns(reviewItems);
            ApplyEffectiveSpeakerLabels(reviewRuns, correctionState);
            var suspiciousItems = SuspiciousReviewItemDetector.MarkSuspiciousItems(reviewRuns);
            var speakerBuckets = BuildBuckets(reviewItems, reviewRuns);

            return new SpeakerCorrectionWorkspace
            {
                SpeakerBuckets = speakerBuckets,
                ReviewRuns = reviewRuns,
                SuspiciousReviewItems = suspiciousItems,
                BucketCount = speakerBuckets.Count,
                RunCount = reviewRuns.Count,
                SuspiciousItemCount = suspiciousItems.Count,
                CorrectionState = correctionState
            };

            ReviewItem CreateReviewItem(IndexedSegment indexedSegment)
            {
                var rawSpeakerLabel = NormalizeSpeakerLabel(indexedSegment.Segment.Speaker);
                var itemId = BuildReviewItemId(indexedSegment.Index);

                return new ReviewItem
                {
                    ItemId = itemId,
                    SegmentIndex = indexedSegment.Index,
                    BucketId = BuildBucketId(rawSpeakerLabel),
                    RawSpeakerLabel = rawSpeakerLabel,
                    EffectiveSpeakerLabel = rawSpeakerLabel,
                    StartSeconds = indexedSegment.Segment.Start,
                    EndSeconds = indexedSegment.Segment.End,
                    Text = indexedSegment.Segment.Text ?? string.Empty
                };
            }
        }

        private static void ApplyEffectiveSpeakerLabels(
            IReadOnlyList<ReviewRun> reviewRuns,
            CorrectionState correctionState)
        {
            foreach (var run in reviewRuns)
            {
                foreach (var item in run.Items)
                {
                    item.EffectiveSpeakerLabel = CorrectionStateOverlayService.GetEffectiveSpeakerLabel(
                        item.ItemId,
                        run.RunId,
                        item.BucketId,
                        item.RawSpeakerLabel,
                        correctionState);
                }

                run.SpeakerLabel = run.Items.Count == 0
                    ? string.Empty
                    : run.Items[0].EffectiveSpeakerLabel;
            }
        }

        private static List<SpeakerBucket> BuildBuckets(
            IReadOnlyList<ReviewItem> reviewItems,
            IReadOnlyList<ReviewRun> reviewRuns)
        {
            var runsByBucketId = reviewRuns
                .SelectMany(run => run.BucketIds.Select(bucketId => new { bucketId, run }))
                .GroupBy(x => x.bucketId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x.Select(y => y.run).DistinctBy(y => y.RunId).ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return reviewItems
                .GroupBy(x => x.BucketId, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var items = group
                        .OrderBy(x => x.StartSeconds)
                        .ThenBy(x => x.SegmentIndex)
                        .ToList();

                    var bucketRuns = runsByBucketId.TryGetValue(group.Key, out var matchedRuns)
                        ? matchedRuns
                        : new List<ReviewRun>();

                    return new SpeakerBucket
                    {
                        BucketId = group.Key,
                        RawSpeakerLabel = items[0].RawSpeakerLabel,
                        SegmentIndexes = items.Select(x => x.SegmentIndex).ToList(),
                        ReviewRunIds = bucketRuns.Select(x => x.RunId).ToList(),
                        ReviewItemIds = items.Select(x => x.ItemId).ToList(),
                        RepresentativeSamples = RepresentativeSampleSelector.SelectSamples(items),
                        SegmentCount = items.Count,
                        RunCount = bucketRuns.Count,
                        TotalSpeakingDurationSeconds = items.Sum(x => Math.Max(0, x.EndSeconds - x.StartSeconds))
                    };
                })
                .ToList();
        }

        private static string NormalizeSpeakerLabel(string? speakerLabel)
        {
            return string.IsNullOrWhiteSpace(speakerLabel)
                ? SpeakerAssignmentValues.Unknown
                : speakerLabel.Trim();
        }

        private static string BuildBucketId(string speakerLabel)
        {
            return $"bucket:{speakerLabel}";
        }

        private static string BuildReviewItemId(int segmentIndex)
        {
            return $"segment:{segmentIndex}";
        }

        private sealed record IndexedSegment(int Index, TranscriptSegment Segment);
    }
}
