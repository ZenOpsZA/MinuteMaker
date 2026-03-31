namespace MinuteMaker.Models.Corrections
{
    public sealed class ReviewRun
    {
        public string RunId { get; set; } = string.Empty;
        public string SpeakerLabel { get; set; } = string.Empty;
        public List<string> BucketIds { get; set; } = new();
        public List<int> SegmentIndexes { get; set; } = new();
        public int SegmentCount { get; set; }
        public double TotalDurationSeconds { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public List<ReviewItem> Items { get; set; } = new();
    }
}
