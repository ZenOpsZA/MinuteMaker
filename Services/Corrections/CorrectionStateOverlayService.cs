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
        /// Milestone 1.5 keeps runtime behavior narrow and only applies segment overrides today.
        /// </summary>
        public static string GetEffectiveSpeakerLabel(
            string reviewItemId,
            string rawSpeakerLabel,
            CorrectionState? correctionState)
        {
            if (correctionState?.Overrides is null || correctionState.Overrides.Count == 0)
                return rawSpeakerLabel;

            var match = correctionState.Overrides
                .Where(x => x.Scope == CorrectionScope.Segment)
                .LastOrDefault(x => string.Equals(x.TargetId, reviewItemId, StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(match?.AssignedSpeaker)
                ? rawSpeakerLabel
                : match.AssignedSpeaker;
        }
    }
}
