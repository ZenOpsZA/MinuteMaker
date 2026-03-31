using MinuteMaker.Models.Corrections;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionBatchService
    {
        public static int ApplyToItems(
            CorrectionState correctionState,
            IEnumerable<ReviewItem> items,
            string assignedSpeaker)
        {
            ArgumentNullException.ThrowIfNull(correctionState);
            ArgumentNullException.ThrowIfNull(items);

            var appliedCount = 0;

            foreach (var item in items)
            {
                CorrectionStateMutationService.ApplyOverride(
                    correctionState,
                    CorrectionScope.Segment,
                    item.ItemId,
                    item.RawSpeakerLabel,
                    assignedSpeaker);

                appliedCount++;
            }

            return appliedCount;
        }
    }
}
