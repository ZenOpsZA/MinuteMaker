namespace MinuteMaker.Models.Corrections
{
    public sealed class SpeakerCorrectionWorkspace
    {
        public List<SpeakerBucket> SpeakerBuckets { get; set; } = new();
        public List<ReviewRun> ReviewRuns { get; set; } = new();
        public List<ReviewItem> SuspiciousReviewItems { get; set; } = new();
        public int BucketCount { get; set; }
        public int RunCount { get; set; }
        public int SuspiciousItemCount { get; set; }
        public CorrectionState CorrectionState { get; set; } = new();
    }
}
