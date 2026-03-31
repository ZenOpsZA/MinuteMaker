namespace MinuteMaker.Models.Corrections
{
    public sealed class SpeakerBucket
    {
        public string BucketId { get; set; } = string.Empty;
        public string RawSpeakerLabel { get; set; } = string.Empty;
        public RepresentativeSample? RepresentativeSample { get; set; }
        public List<int> SegmentIndexes { get; set; } = new();
    }
}
