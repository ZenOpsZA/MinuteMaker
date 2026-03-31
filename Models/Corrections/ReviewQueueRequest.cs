namespace MinuteMaker.Models.Corrections
{
    public sealed class ReviewQueueRequest
    {
        public ReviewFilterMode Mode { get; set; } = ReviewFilterMode.SuspiciousOnly;
        public string? BucketId { get; set; }
        public bool IncludeReviewedItems { get; set; }
        public bool OnlySkippedItems { get; set; }
    }
}
