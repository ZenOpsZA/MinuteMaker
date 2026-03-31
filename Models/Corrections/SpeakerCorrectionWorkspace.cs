namespace MinuteMaker.Models.Corrections
{
    public sealed class SpeakerCorrectionWorkspace
    {
        public List<SpeakerBucket> SpeakerBuckets { get; set; } = new();
        public List<ReviewRun> ReviewRuns { get; set; } = new();
        public CorrectionState CorrectionState { get; set; } = new();
    }
}
