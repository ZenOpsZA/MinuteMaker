using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class ReviewItemFlagLabelService
    {
        public static string ToFriendlyLabel(string flag)
        {
            return flag switch
            {
                ReviewItemFlags.ShortSegment => "Very short segment",
                ReviewItemFlags.RapidSpeakerSwitch => "Rapid speaker change",
                ReviewItemFlags.MidSentenceSpeakerChange => "Possible mid-sentence switch",
                ReviewItemFlags.FragmentedRun => "Unusually fragmented run",
                _ => flag
            };
        }
    }
}
