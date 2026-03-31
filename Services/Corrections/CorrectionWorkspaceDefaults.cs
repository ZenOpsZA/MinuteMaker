namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionWorkspaceDefaults
    {
        public const double RunMergeGapSeconds = 0.75;
        public const int RepresentativeSamplesPerBucket = 3;
        public const double ShortSegmentSeconds = 1.0;
        public const double RapidSpeakerSwitchGapSeconds = 0.35;
        public const double FragmentedRunMaxDurationSeconds = 2.5;
        public const double MidSentenceGapSeconds = 0.2;
    }
}
