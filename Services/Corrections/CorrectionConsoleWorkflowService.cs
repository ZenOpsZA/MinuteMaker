using MinuteMaker.Models.Corrections;
using MinuteMaker.Models.Transcription;
using MinuteMaker.Persistence.Corrections;
using MinuteMaker.Services.Audio;
using MinuteMaker.Services.Output;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionConsoleWorkflowService
    {
        public static SpeakerCorrectionWorkspace RunInteractiveReview(
            IReadOnlyList<TranscriptSegment> segments,
            CorrectionState correctionState,
            string correctionStatePath,
            string originalMediaPath,
            string? vlcPath)
        {
            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(correctionState);

            var workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, correctionState);

            Console.WriteLine("Step 4/4 - Reviewing speaker corrections...");
            Console.WriteLine();
            Console.WriteLine($"Buckets: {workspace.BucketCount}");
            Console.WriteLine($"Runs: {workspace.RunCount}");
            Console.WriteLine($"Suspicious items: {workspace.SuspiciousItemCount}");

            workspace = ReviewBuckets(workspace, segments, correctionStatePath, originalMediaPath, vlcPath);
            workspace = ReviewSuspiciousItems(workspace, segments, correctionStatePath, originalMediaPath, vlcPath);

            return workspace;
        }

        private static SpeakerCorrectionWorkspace ReviewBuckets(
            SpeakerCorrectionWorkspace workspace,
            IReadOnlyList<TranscriptSegment> segments,
            string correctionStatePath,
            string originalMediaPath,
            string? vlcPath)
        {
            Console.WriteLine();
            Console.WriteLine("Bucket Review");

            foreach (var bucketId in workspace.SpeakerBuckets.Select(x => x.BucketId).ToList())
            {
                while (true)
                {
                    var currentWorkspace = SpeakerCorrectionWorkspaceFactory.Create(segments, workspace.CorrectionState);
                    var bucket = currentWorkspace.SpeakerBuckets
                        .First(x => string.Equals(x.BucketId, bucketId, StringComparison.OrdinalIgnoreCase));

                    DisplayBucket(bucket, currentWorkspace.CorrectionState);

                    Console.WriteLine("1. Enter speaker name");
                    Console.WriteLine($"2. Assign {SpeakerAssignmentValues.Unknown}");
                    Console.WriteLine($"3. Assign {SpeakerAssignmentValues.Mixed}");
                    Console.WriteLine("4. Open sample in VLC");
                    Console.WriteLine("5. Use raw label");
                    Console.WriteLine("6. Skip");
                    Console.Write("Select option (1-6): ");

                    var option = Console.ReadLine()?.Trim();
                    if (option == "1")
                    {
                        var speaker = PromptForSpeakerName();
                        if (speaker is null)
                            continue;

                        ApplyAndSave(
                            currentWorkspace.CorrectionState,
                            CorrectionScope.SpeakerBucket,
                            bucket.BucketId,
                            bucket.RawSpeakerLabel,
                            speaker,
                            correctionStatePath);

                        workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, currentWorkspace.CorrectionState);
                        break;
                    }

                    if (option == "2" || option == "3" || option == "5")
                    {
                        var assignedSpeaker = option switch
                        {
                            "2" => SpeakerAssignmentValues.Unknown,
                            "3" => SpeakerAssignmentValues.Mixed,
                            _ => bucket.RawSpeakerLabel
                        };

                        ApplyAndSave(
                            currentWorkspace.CorrectionState,
                            CorrectionScope.SpeakerBucket,
                            bucket.BucketId,
                            bucket.RawSpeakerLabel,
                            assignedSpeaker,
                            correctionStatePath);

                        workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, currentWorkspace.CorrectionState);
                        break;
                    }

                    if (option == "4")
                    {
                        OpenRepresentativeSample(bucket, originalMediaPath, vlcPath);
                        continue;
                    }

                    if (option == "6")
                    {
                        workspace = currentWorkspace;
                        break;
                    }

                    Console.WriteLine("Invalid selection. Try again.");
                }
            }

            return workspace;
        }

        private static SpeakerCorrectionWorkspace ReviewSuspiciousItems(
            SpeakerCorrectionWorkspace workspace,
            IReadOnlyList<TranscriptSegment> segments,
            string correctionStatePath,
            string originalMediaPath,
            string? vlcPath)
        {
            Console.WriteLine();
            Console.WriteLine("Suspicious Item Review");

            var reviewedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var skippedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, workspace.CorrectionState);

                var nextItem = workspace.SuspiciousReviewItems
                    .OrderBy(x => x.StartSeconds)
                    .ThenBy(x => x.SegmentIndex)
                    .FirstOrDefault(x =>
                        !reviewedItemIds.Contains(x.ItemId) &&
                        !skippedItemIds.Contains(x.ItemId));

                if (nextItem is null)
                    break;

                var run = workspace.ReviewRuns
                    .First(x => string.Equals(x.RunId, nextItem.RunId, StringComparison.OrdinalIgnoreCase));
                var bucket = workspace.SpeakerBuckets
                    .First(x => string.Equals(x.BucketId, nextItem.BucketId, StringComparison.OrdinalIgnoreCase));

                DisplaySuspiciousItem(nextItem, run, bucket);

                Console.WriteLine("1. Keep current assignment");
                Console.WriteLine("2. Change speaker assignment");
                Console.WriteLine("3. Skip for later");
                Console.WriteLine("4. Open recording in VLC");
                Console.Write("Select option (1-4): ");

                var option = Console.ReadLine()?.Trim();
                if (option == "1")
                {
                    reviewedItemIds.Add(nextItem.ItemId);
                    continue;
                }

                if (option == "2")
                {
                    var assignedSpeaker = PromptForSpeakerAssignment();
                    if (assignedSpeaker is null)
                        continue;

                    var scope = PromptForCorrectionScope();
                    if (scope is null)
                        continue;

                    ApplyScopedCorrection(
                        workspace,
                        nextItem,
                        run,
                        bucket,
                        assignedSpeaker,
                        scope.Value,
                        correctionStatePath);

                    MarkReviewedByScope(reviewedItemIds, nextItem, run, bucket, scope.Value);
                    skippedItemIds.RemoveWhere(x => reviewedItemIds.Contains(x));
                    continue;
                }

                if (option == "3")
                {
                    skippedItemIds.Add(nextItem.ItemId);
                    continue;
                }

                if (option == "4")
                {
                    var opened = MediaLauncherService.TryOpenInVlc(
                        originalMediaPath,
                        nextItem.StartSeconds,
                        vlcPath);

                    if (!opened)
                    {
                        Console.WriteLine("Unable to open VLC. Check VLC installation/path.");
                    }

                    continue;
                }

                Console.WriteLine("Invalid selection. Try again.");
            }

            if (skippedItemIds.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"Skipped suspicious items: {skippedItemIds.Count}");
            }

            return workspace;
        }

        private static void DisplayBucket(SpeakerBucket bucket, CorrectionState correctionState)
        {
            var currentAssignment = CorrectionStateOverlayService.GetBucketAssignedSpeaker(
                bucket.BucketId,
                bucket.RawSpeakerLabel,
                correctionState);

            Console.WriteLine();
            Console.WriteLine($"Bucket: {bucket.RawSpeakerLabel}");
            Console.WriteLine($"Current assignment: {currentAssignment}");
            Console.WriteLine($"Segments: {bucket.SegmentCount}");
            Console.WriteLine($"Runs: {bucket.RunCount}");
            Console.WriteLine($"Total duration: {FormatDuration(bucket.TotalSpeakingDurationSeconds)}");

            if (bucket.RepresentativeSamples.Count == 0)
            {
                Console.WriteLine("Samples: none");
                return;
            }

            Console.WriteLine("Representative samples:");
            for (int i = 0; i < bucket.RepresentativeSamples.Count; i++)
            {
                var sample = bucket.RepresentativeSamples[i];
                Console.WriteLine(
                    $"{i + 1}. [{TranscriptFormatter.FormatTimestamp(sample.StartSeconds)} - {TranscriptFormatter.FormatTimestamp(sample.EndSeconds)}] {sample.TextPreview}");
            }
        }

        private static void DisplaySuspiciousItem(ReviewItem item, ReviewRun run, SpeakerBucket bucket)
        {
            Console.WriteLine();
            Console.WriteLine($"Suspicious item {item.ItemId}");
            Console.WriteLine(
                $"Time: [{TranscriptFormatter.FormatTimestamp(item.StartSeconds)} - {TranscriptFormatter.FormatTimestamp(item.EndSeconds)}]");
            Console.WriteLine($"Current speaker: {item.EffectiveSpeakerLabel}");
            Console.WriteLine($"Raw bucket: {bucket.RawSpeakerLabel}");
            Console.WriteLine($"Run: {run.RunId}");

            var reasons = item.ReviewFlags
                .Select(ReviewItemFlagLabelService.ToFriendlyLabel)
                .ToList();

            Console.WriteLine($"Reasons: {string.Join(", ", reasons)}");
            Console.WriteLine($"Text: {BuildExcerpt(item.Text)}");
        }

        private static void OpenRepresentativeSample(
            SpeakerBucket bucket,
            string originalMediaPath,
            string? vlcPath)
        {
            if (bucket.RepresentativeSamples.Count == 0)
            {
                Console.WriteLine("No representative samples available.");
                return;
            }

            Console.Write($"Select sample to open (1-{bucket.RepresentativeSamples.Count}): ");
            var input = Console.ReadLine()?.Trim();

            if (!int.TryParse(input, out var sampleIndex) ||
                sampleIndex < 1 ||
                sampleIndex > bucket.RepresentativeSamples.Count)
            {
                Console.WriteLine("Invalid sample selection.");
                return;
            }

            var sample = bucket.RepresentativeSamples[sampleIndex - 1];
            var opened = MediaLauncherService.TryOpenInVlc(originalMediaPath, sample.StartSeconds, vlcPath);

            if (!opened)
            {
                Console.WriteLine("Unable to open VLC. Check VLC installation/path.");
            }
        }

        private static void ApplyScopedCorrection(
            SpeakerCorrectionWorkspace workspace,
            ReviewItem item,
            ReviewRun run,
            SpeakerBucket bucket,
            string assignedSpeaker,
            CorrectionScope scope,
            string correctionStatePath)
        {
            var correctionState = workspace.CorrectionState;
            var targetId = scope switch
            {
                CorrectionScope.Segment => item.ItemId,
                CorrectionScope.ReviewRun => run.RunId,
                CorrectionScope.SpeakerBucket => bucket.BucketId,
                _ => item.ItemId
            };

            var originalSpeaker = scope switch
            {
                CorrectionScope.Segment => item.RawSpeakerLabel,
                CorrectionScope.ReviewRun => item.RawSpeakerLabel,
                CorrectionScope.SpeakerBucket => bucket.RawSpeakerLabel,
                _ => item.RawSpeakerLabel
            };

            ApplyAndSave(
                correctionState,
                scope,
                targetId,
                originalSpeaker,
                assignedSpeaker,
                correctionStatePath);
        }

        private static void ApplyAndSave(
            CorrectionState correctionState,
            CorrectionScope scope,
            string targetId,
            string originalSpeaker,
            string assignedSpeaker,
            string correctionStatePath)
        {
            CorrectionStateMutationService.ApplyOverride(
                correctionState,
                scope,
                targetId,
                originalSpeaker,
                assignedSpeaker);

            CorrectionStateStore.Save(correctionStatePath, correctionState);
        }

        private static void MarkReviewedByScope(
            HashSet<string> reviewedItemIds,
            ReviewItem item,
            ReviewRun run,
            SpeakerBucket bucket,
            CorrectionScope scope)
        {
            if (scope == CorrectionScope.Segment)
            {
                reviewedItemIds.Add(item.ItemId);
                return;
            }

            if (scope == CorrectionScope.ReviewRun)
            {
                foreach (var itemId in run.Items.Select(x => x.ItemId))
                {
                    reviewedItemIds.Add(itemId);
                }

                return;
            }

            foreach (var itemId in bucket.ReviewItemIds)
            {
                reviewedItemIds.Add(itemId);
            }
        }

        private static string? PromptForSpeakerName()
        {
            Console.Write("Enter speaker name (blank to cancel): ");
            var input = Console.ReadLine()?.Trim();
            return string.IsNullOrWhiteSpace(input) ? null : input;
        }

        private static string? PromptForSpeakerAssignment()
        {
            Console.WriteLine("Choose assignment:");
            Console.WriteLine("1. Enter speaker name");
            Console.WriteLine($"2. {SpeakerAssignmentValues.Unknown}");
            Console.WriteLine($"3. {SpeakerAssignmentValues.Mixed}");
            Console.WriteLine("4. Cancel");
            Console.Write("Select option (1-4): ");

            var option = Console.ReadLine()?.Trim();
            if (option == "1")
                return PromptForSpeakerName();

            if (option == "2")
                return SpeakerAssignmentValues.Unknown;

            if (option == "3")
                return SpeakerAssignmentValues.Mixed;

            return null;
        }

        private static CorrectionScope? PromptForCorrectionScope()
        {
            Console.WriteLine("Apply correction scope:");
            Console.WriteLine("1. This segment only");
            Console.WriteLine("2. This run");
            Console.WriteLine("3. This bucket");
            Console.WriteLine("4. Cancel");
            Console.Write("Select option (1-4): ");

            var option = Console.ReadLine()?.Trim();
            return option switch
            {
                "1" => CorrectionScope.Segment,
                "2" => CorrectionScope.ReviewRun,
                "3" => CorrectionScope.SpeakerBucket,
                _ => null
            };
        }

        private static string FormatDuration(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"mm\:ss");
        }

        private static string BuildExcerpt(string? text)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.Length <= 100)
                return value;

            return value[..97] + "...";
        }
    }
}
