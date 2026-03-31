namespace MinuteMaker.Models.Corrections
{
    public sealed class SpeakerBucket
    {
        public string BucketId { get; set; } = string.Empty;
        public string RawSpeakerLabel { get; set; } = string.Empty;
        public List<int> SegmentIndexes { get; set; } = new();
        public List<string> ReviewRunIds { get; set; } = new();
        public List<string> ReviewItemIds { get; set; } = new();
        public List<RepresentativeSample> RepresentativeSamples { get; set; } = new();
        public int SegmentCount { get; set; }
        public int RunCount { get; set; }
        public double TotalSpeakingDurationSeconds { get; set; }
    }
}
