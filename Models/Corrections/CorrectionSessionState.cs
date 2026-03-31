namespace MinuteMaker.Models.Corrections
{
    public sealed class CorrectionSessionState
    {
        public List<string> AssignedBucketIds { get; set; } = new();
        public List<string> ReviewedItemIds { get; set; } = new();
        public List<string> SkippedItemIds { get; set; } = new();
        public string? LastBucketId { get; set; }
        public string? LastReviewItemId { get; set; }
    }
}
