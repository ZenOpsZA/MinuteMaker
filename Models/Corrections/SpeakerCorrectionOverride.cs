namespace MinuteMaker.Models.Corrections
{
    /// <summary>
    /// Stores a manual speaker assignment against a deterministic correction target.
    /// </summary>
    public sealed class SpeakerCorrectionOverride
    {
        /// <summary>
        /// Identifies the correction target using a stable string key.
        /// Examples:
        /// - segment:12
        /// - bucket:SPEAKER_00
        /// - run:00:01:15.200-00:02:04.900
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        public CorrectionScope Scope { get; set; } = CorrectionScope.Segment;

        public string OriginalSpeakerLabel { get; set; } = string.Empty;

        /// <summary>
        /// The speaker assignment chosen during review.
        /// This may be a real speaker name, a canonical speaker label, or a reserved value such as
        /// <see cref="SpeakerAssignmentValues.Unknown"/> or <see cref="SpeakerAssignmentValues.Mixed"/>.
        /// </summary>
        public string AssignedSpeaker { get; set; } = string.Empty;

        public DateTimeOffset AppliedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
