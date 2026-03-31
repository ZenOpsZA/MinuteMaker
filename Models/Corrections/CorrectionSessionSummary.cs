namespace MinuteMaker.Models.Corrections
{
    public sealed class CorrectionSessionSummary
    {
        public int OverrideCount { get; set; }
        public int AssignedBucketCount { get; set; }
        public int TotalBucketCount { get; set; }
        public int ReviewedSuspiciousCount { get; set; }
        public int RemainingSuspiciousCount { get; set; }
        public int SkippedSuspiciousCount { get; set; }
    }
}
