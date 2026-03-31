using MinuteMaker.Models.Corrections;
using MinuteMaker.Models.Transcription;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionTranscriptProjectionService
    {
        public static List<TranscriptSegment> BuildCorrectedSegments(
            IReadOnlyList<TranscriptSegment> rawSegments,
            SpeakerCorrectionWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(rawSegments);
            ArgumentNullException.ThrowIfNull(workspace);

            var itemsBySegmentIndex = workspace.ReviewRuns
                .SelectMany(x => x.Items)
                .ToDictionary(x => x.SegmentIndex);

            return rawSegments
                .Select((segment, index) => new TranscriptSegment
                {
                    Start = segment.Start,
                    End = segment.End,
                    Text = segment.Text,
                    Speaker = itemsBySegmentIndex.TryGetValue(index, out var item)
                        ? item.EffectiveSpeakerLabel
                        : NormalizeSpeakerLabel(segment.Speaker)
                })
                .ToList();
        }

        public static Dictionary<string, string> BuildBucketSpeakerMap(
            SpeakerCorrectionWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);

            return workspace.SpeakerBuckets
                .OrderBy(x => x.RawSpeakerLabel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.RawSpeakerLabel,
                    x => CorrectionStateOverlayService.GetBucketAssignedSpeaker(
                        x.BucketId,
                        x.RawSpeakerLabel,
                        workspace.CorrectionState),
                    StringComparer.OrdinalIgnoreCase);
        }

        public static Dictionary<string, string> BuildIdentitySpeakerMap(
            IReadOnlyList<TranscriptSegment> segments)
        {
            ArgumentNullException.ThrowIfNull(segments);

            return segments
                .Select(x => NormalizeSpeakerLabel(x.Speaker))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeSpeakerLabel(string? speakerLabel)
        {
            return string.IsNullOrWhiteSpace(speakerLabel)
                ? SpeakerAssignmentValues.Unknown
                : speakerLabel.Trim();
        }
    }
}
