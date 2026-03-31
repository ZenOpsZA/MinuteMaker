namespace MinuteMaker.Models.Corrections
{
    public sealed class ReviewItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string RunId { get; set; } = string.Empty;
        public int SegmentIndex { get; set; }
        public string BucketId { get; set; } = string.Empty;
        public string RawSpeakerLabel { get; set; } = string.Empty;
        public string EffectiveSpeakerLabel { get; set; } = string.Empty;
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> ReviewFlags { get; set; } = new();
        public bool IsSuspicious { get; set; }
    }
}
