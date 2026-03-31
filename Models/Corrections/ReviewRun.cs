namespace MinuteMaker.Models.Corrections
{
    public sealed class ReviewRun
    {
        public string RunId { get; set; } = string.Empty;
        public string BucketId { get; set; } = string.Empty;
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public List<ReviewItem> Items { get; set; } = new();
    }
}
