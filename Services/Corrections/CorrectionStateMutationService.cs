using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionStateMutationService
    {
        public static void ApplyOverride(
            CorrectionState correctionState,
            CorrectionScope scope,
            string targetId,
            string originalSpeakerLabel,
            string assignedSpeaker)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("Target id is required.", nameof(targetId));

            if (string.IsNullOrWhiteSpace(assignedSpeaker))
                throw new ArgumentException("Assigned speaker is required.", nameof(assignedSpeaker));

            RemoveOverride(correctionState, scope, targetId);

            if (string.Equals(originalSpeakerLabel, assignedSpeaker, StringComparison.OrdinalIgnoreCase))
                return;

            correctionState.Overrides.Add(new SpeakerCorrectionOverride
            {
                TargetId = targetId,
                Scope = scope,
                OriginalSpeakerLabel = originalSpeakerLabel,
                AssignedSpeaker = assignedSpeaker.Trim(),
                AppliedAtUtc = DateTimeOffset.UtcNow
            });

            correctionState.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        public static void RemoveOverride(
            CorrectionState correctionState,
            CorrectionScope scope,
            string targetId)
        {
            ArgumentNullException.ThrowIfNull(correctionState);

            correctionState.Overrides.RemoveAll(x =>
                x.Scope == scope &&
                string.Equals(x.TargetId, targetId, StringComparison.OrdinalIgnoreCase));

            correctionState.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }
}
