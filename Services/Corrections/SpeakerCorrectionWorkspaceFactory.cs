using MinuteMaker.Models.Corrections;
using MinuteMaker.Models.Speakers;
using MinuteMaker.Models.Transcription;
using MinuteMaker.Services.Speakers;

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

            var samples = SpeakerSampleService.BuildSamples(segments);
            var orderedSegments = segments
                .Select((segment, index) => new IndexedSegment(index, segment))
                .OrderBy(x => x.Segment.Start)
                .ToList();

            var buckets = orderedSegments
                .GroupBy(x => NormalizeSpeakerLabel(x.Segment.Speaker), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => CreateBucket(group.Key, group.ToList(), samples))
                .ToList();

            var reviewRuns = buckets
                .Select(bucket => CreateReviewRun(bucket, orderedSegments, correctionState))
                .ToList();

            return new SpeakerCorrectionWorkspace
            {
                SpeakerBuckets = buckets,
                ReviewRuns = reviewRuns,
                CorrectionState = correctionState
            };
        }

        private static SpeakerBucket CreateBucket(
            string speakerLabel,
            IReadOnlyList<IndexedSegment> segments,
            IReadOnlyDictionary<string, SpeakerSample> samples)
        {
            var bucket = new SpeakerBucket
            {
                BucketId = BuildBucketId(speakerLabel),
                RawSpeakerLabel = speakerLabel,
                SegmentIndexes = segments
                    .Select(x => x.Index)
                    .OrderBy(x => x)
                    .ToList()
            };

            if (samples.TryGetValue(speakerLabel, out var sample))
            {
                var sampleSegment = segments.FirstOrDefault(x =>
                    x.Segment.Start.Equals(sample.StartSeconds) &&
                    x.Segment.End.Equals(sample.EndSeconds));

                bucket.RepresentativeSample = new RepresentativeSample
                {
                    SegmentIndex = sampleSegment?.Index ?? bucket.SegmentIndexes.First(),
                    StartSeconds = sample.StartSeconds,
                    EndSeconds = sample.EndSeconds,
                    TextPreview = sample.TextPreview
                };
            }

            return bucket;
        }

        private static ReviewRun CreateReviewRun(
            SpeakerBucket bucket,
            IReadOnlyList<IndexedSegment> segments,
            CorrectionState correctionState)
        {
            var items = segments
                .Where(x => bucket.SegmentIndexes.Contains(x.Index))
                .Select(x => CreateReviewItem(bucket, x, correctionState))
                .ToList();

            return new ReviewRun
            {
                RunId = $"run:{bucket.BucketId}",
                BucketId = bucket.BucketId,
                StartSeconds = items.Count == 0 ? 0 : items.Min(x => x.StartSeconds),
                EndSeconds = items.Count == 0 ? 0 : items.Max(x => x.EndSeconds),
                Items = items
            };
        }

        private static ReviewItem CreateReviewItem(
            SpeakerBucket bucket,
            IndexedSegment indexedSegment,
            CorrectionState correctionState)
        {
            var itemId = BuildReviewItemId(indexedSegment.Index);
            var rawSpeakerLabel = NormalizeSpeakerLabel(indexedSegment.Segment.Speaker);

            return new ReviewItem
            {
                ItemId = itemId,
                SegmentIndex = indexedSegment.Index,
                BucketId = bucket.BucketId,
                RawSpeakerLabel = rawSpeakerLabel,
                EffectiveSpeakerLabel = CorrectionStateOverlayService.GetEffectiveSpeakerLabel(
                    itemId,
                    rawSpeakerLabel,
                    correctionState),
                StartSeconds = indexedSegment.Segment.Start,
                EndSeconds = indexedSegment.Segment.End,
                Text = indexedSegment.Segment.Text ?? string.Empty
            };
        }

        private static string NormalizeSpeakerLabel(string? speakerLabel)
        {
            return string.IsNullOrWhiteSpace(speakerLabel)
                ? "UNKNOWN"
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
