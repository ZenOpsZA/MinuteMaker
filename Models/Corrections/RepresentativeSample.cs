namespace MinuteMaker.Models.Corrections
{
    public sealed class RepresentativeSample
    {
        public int SegmentIndex { get; set; }
        public double StartSeconds { get; set; }
        public double EndSeconds { get; set; }
        public string TextPreview { get; set; } = string.Empty;
    }
}
