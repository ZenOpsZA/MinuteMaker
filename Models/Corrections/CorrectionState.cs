namespace MinuteMaker.Models.Corrections
{
    public sealed class CorrectionState
    {
        public int Version { get; set; } = 1;
        public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public List<SpeakerCorrectionOverride> Overrides { get; set; } = new();
    }
}
