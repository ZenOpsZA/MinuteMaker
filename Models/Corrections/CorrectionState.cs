namespace MinuteMaker.Models.Corrections
{
    public sealed class CorrectionState
    {
        public int Version { get; set; } = 2;
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<SpeakerCorrectionOverride> Overrides { get; set; } = new();
        public CorrectionSessionState Session { get; set; } = new();
    }
}
