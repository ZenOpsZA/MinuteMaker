using MinuteMaker.Models.Corrections;
using MinuteMaker.Models.Transcription;
using MinuteMaker.Persistence.Corrections;
using MinuteMaker.Services.Audio;
using MinuteMaker.Services.Output;

namespace MinuteMaker.Services.Corrections
{
    public static class CorrectionConsoleWorkflowService
    {
        private enum SessionStartMode
        {
            Continue,
            Restart,
            ReviewCompleted
        }

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
            DisplayWorkspaceOverview(workspace);

            var startMode = ResolveSessionStartMode(workspace, correctionState, correctionStatePath);
            workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, correctionState);

            if (startMode != SessionStartMode.ReviewCompleted)
            {
                workspace = ReviewBuckets(workspace, segments, correctionStatePath, originalMediaPath, vlcPath);
                workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, correctionState);
                DisplayBucketAssignmentSummary(workspace);
            }

            workspace = RunReviewHub(workspace, segments, correctionStatePath, originalMediaPath, vlcPath);
            workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, correctionState);

            DisplayReviewSummary(workspace);
            DisplayFinalSpeakerDistribution(workspace);

            return workspace;
        }

        private static SessionStartMode ResolveSessionStartMode(
            SpeakerCorrectionWorkspace workspace,
            CorrectionState correctionState,
            string correctionStatePath)
        {
            if (!CorrectionSessionStateService.HasExistingSession(correctionState))
                return SessionStartMode.Continue;

            Console.WriteLine();
            Console.WriteLine("Existing correction session detected.");
            DisplaySessionSummary(workspace);
            Console.WriteLine("1. Continue previous session");
            Console.WriteLine("2. Restart correction");
            Console.WriteLine("3. Review completed work");

            while (true)
            {
                Console.Write("Select option (1-3): ");
                var input = Console.ReadLine()?.Trim();

                if (input == "1")
                    return SessionStartMode.Continue;

                if (input == "2")
                {
                    if (!Confirm("Clear existing overrides and session progress?"))
                        continue;

                    CorrectionSessionStateService.Reset(correctionState);
                    CorrectionStateStore.Save(correctionStatePath, correctionState);
                    return SessionStartMode.Restart;
                }

                if (input == "3")
                    return SessionStartMode.ReviewCompleted;

                Console.WriteLine("Invalid selection. Try again.");
            }
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

            var bucketIds = workspace.SpeakerBuckets.Select(x => x.BucketId).ToList();
            if (bucketIds.Count == 0)
                return workspace;

            var currentIndex = ResolveBucketStartIndex(workspace);

            while (true)
            {
                workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, workspace.CorrectionState);
                bucketIds = workspace.SpeakerBuckets.Select(x => x.BucketId).ToList();

                if (bucketIds.Count == 0)
                    return workspace;

                currentIndex = Math.Clamp(currentIndex, 0, bucketIds.Count - 1);

                var bucket = workspace.SpeakerBuckets
                    .First(x => string.Equals(x.BucketId, bucketIds[currentIndex], StringComparison.OrdinalIgnoreCase));

                CorrectionSessionStateService.SetLastBucket(workspace.CorrectionState, bucket.BucketId);
                SaveState(workspace.CorrectionState, correctionStatePath);

                Console.WriteLine();
                Console.WriteLine($"Bucket {currentIndex + 1} of {bucketIds.Count}");
                DisplayBucket(bucket, workspace.CorrectionState);

                Console.WriteLine("Commands: assign, unknown, mixed, raw, sample, next, previous, jump, review, done");
                Console.Write("Command: ");
                var command = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

                if (command == "assign")
                {
                    var speaker = PromptForSpeakerName();
                    if (speaker is null)
                        continue;

                    ApplyAndSave(
                        workspace.CorrectionState,
                        CorrectionScope.SpeakerBucket,
                        bucket.BucketId,
                        bucket.RawSpeakerLabel,
                        speaker,
                        correctionStatePath);

                    CorrectionSessionStateService.MarkBucketAssigned(workspace.CorrectionState, bucket.BucketId);
                    SaveState(workspace.CorrectionState, correctionStatePath);
                    currentIndex = GetNextBucketIndex(bucketIds, currentIndex);
                    continue;
                }

                if (command is "unknown" or "mixed" or "raw")
                {
                    var assignedSpeaker = command switch
                    {
                        "unknown" => SpeakerAssignmentValues.Unknown,
                        "mixed" => SpeakerAssignmentValues.Mixed,
                        _ => bucket.RawSpeakerLabel
                    };

                    ApplyAndSave(
                        workspace.CorrectionState,
                        CorrectionScope.SpeakerBucket,
                        bucket.BucketId,
                        bucket.RawSpeakerLabel,
                        assignedSpeaker,
                        correctionStatePath);

                    CorrectionSessionStateService.MarkBucketAssigned(workspace.CorrectionState, bucket.BucketId);
                    SaveState(workspace.CorrectionState, correctionStatePath);
                    currentIndex = GetNextBucketIndex(bucketIds, currentIndex);
                    continue;
                }

                if (command == "sample")
                {
                    OpenRepresentativeSample(bucket, originalMediaPath, vlcPath);
                    continue;
                }

                if (command == "next")
                {
                    currentIndex = Math.Min(bucketIds.Count - 1, currentIndex + 1);
                    continue;
                }

                if (command == "previous")
                {
                    currentIndex = Math.Max(0, currentIndex - 1);
                    continue;
                }

                if (command == "jump")
                {
                    currentIndex = PromptForBucketIndex(workspace, currentIndex);
                    continue;
                }

                if (command == "review")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.SpecificBucket,
                            BucketId = bucket.BucketId,
                            IncludeReviewedItems = true
                        },
                        "Bucket review");
                    continue;
                }

                if (command == "done")
                    return workspace;

                Console.WriteLine("Invalid command. Try again.");
            }
        }

        private static SpeakerCorrectionWorkspace RunReviewHub(
            SpeakerCorrectionWorkspace workspace,
            IReadOnlyList<TranscriptSegment> segments,
            string correctionStatePath,
            string originalMediaPath,
            string? vlcPath)
        {
            while (true)
            {
                workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, workspace.CorrectionState);

                Console.WriteLine();
                Console.WriteLine("Review Modes");
                DisplaySessionSummary(workspace);
                Console.WriteLine("1. Continue suspicious review");
                Console.WriteLine("2. Revisit skipped suspicious items");
                Console.WriteLine("3. Revisit all suspicious items");
                Console.WriteLine("4. Review only Unknown speakers");
                Console.WriteLine("5. Review a specific bucket");
                Console.WriteLine("6. Full review mode");
                Console.WriteLine("7. Finish review");

                Console.Write("Select option (1-7): ");
                var input = Console.ReadLine()?.Trim();

                if (input == "1")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.SuspiciousOnly
                        },
                        "Suspicious review");
                    continue;
                }

                if (input == "2")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.SuspiciousOnly,
                            IncludeReviewedItems = true,
                            OnlySkippedItems = true
                        },
                        "Skipped suspicious items");
                    continue;
                }

                if (input == "3")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.SuspiciousOnly,
                            IncludeReviewedItems = true
                        },
                        "All suspicious items");
                    continue;
                }

                if (input == "4")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.UnassignedOnly
                        },
                        "Unknown speaker review");
                    continue;
                }

                if (input == "5")
                {
                    var bucketId = PromptForBucketId(workspace);
                    if (bucketId is null)
                        continue;

                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.SpecificBucket,
                            BucketId = bucketId,
                            IncludeReviewedItems = true
                        },
                        "Specific bucket review");
                    continue;
                }

                if (input == "6")
                {
                    workspace = RunQueueSession(
                        workspace,
                        segments,
                        correctionStatePath,
                        originalMediaPath,
                        vlcPath,
                        new ReviewQueueRequest
                        {
                            Mode = ReviewFilterMode.AllItems,
                            IncludeReviewedItems = true
                        },
                        "Full review mode");
                    continue;
                }

                if (input == "7")
                    return workspace;

                Console.WriteLine("Invalid selection. Try again.");
            }
        }

        private static SpeakerCorrectionWorkspace RunQueueSession(
            SpeakerCorrectionWorkspace workspace,
            IReadOnlyList<TranscriptSegment> segments,
            string correctionStatePath,
            string originalMediaPath,
            string? vlcPath,
            ReviewQueueRequest request,
            string heading)
        {
            Console.WriteLine();
            Console.WriteLine(heading);

            var currentIndex = 0;

            while (true)
            {
                workspace = SpeakerCorrectionWorkspaceFactory.Create(segments, workspace.CorrectionState);
                var queue = CorrectionReviewQueueService.BuildQueue(workspace, request);

                if (queue.Count == 0)
                {
                    Console.WriteLine("No items available for this review mode.");
                    return workspace;
                }

                if (!string.IsNullOrWhiteSpace(workspace.CorrectionState.Session.LastReviewItemId))
                {
                    var savedIndex = queue.FindIndex(x =>
                        string.Equals(
                            x.ItemId,
                            workspace.CorrectionState.Session.LastReviewItemId,
                            StringComparison.OrdinalIgnoreCase));

                    if (savedIndex >= 0)
                        currentIndex = savedIndex;
                }

                currentIndex = Math.Clamp(currentIndex, 0, queue.Count - 1);

                var item = queue[currentIndex];
                var run = workspace.ReviewRuns
                    .First(x => string.Equals(x.RunId, item.RunId, StringComparison.OrdinalIgnoreCase));
                var bucket = workspace.SpeakerBuckets
                    .First(x => string.Equals(x.BucketId, item.BucketId, StringComparison.OrdinalIgnoreCase));

                CorrectionSessionStateService.SetLastReviewItem(workspace.CorrectionState, item.ItemId);
                SaveState(workspace.CorrectionState, correctionStatePath);

                Console.WriteLine();
                Console.WriteLine($"Item {currentIndex + 1} of {queue.Count}");
                DisplayReviewItem(item, run, bucket);

                Console.WriteLine("Commands: next, previous, keep, skip, revisit, change, open, accept-all, done");
                Console.Write("Command: ");
                var command = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();

                if (command is "next" or "keep")
                {
                    CorrectionSessionStateService.MarkReviewed(workspace.CorrectionState, new[] { item.ItemId });
                    SaveState(workspace.CorrectionState, correctionStatePath);
                    currentIndex = Math.Min(currentIndex + 1, queue.Count - 1);
                    continue;
                }

                if (command == "previous")
                {
                    currentIndex = Math.Max(0, currentIndex - 1);
                    continue;
                }

                if (command == "skip")
                {
                    CorrectionSessionStateService.MarkSkipped(workspace.CorrectionState, item.ItemId);
                    SaveState(workspace.CorrectionState, correctionStatePath);
                    currentIndex = Math.Min(currentIndex + 1, queue.Count - 1);
                    continue;
                }

                if (command == "revisit")
                {
                    currentIndex = 0;
                    continue;
                }

                if (command == "open")
                {
                    var opened = MediaLauncherService.TryOpenInVlc(originalMediaPath, item.StartSeconds, vlcPath);
                    if (!opened)
                    {
                        Console.WriteLine("Unable to open VLC. Check VLC installation/path.");
                    }

                    continue;
                }

                if (command == "accept-all")
                {
                    var remainingItems = CorrectionReviewQueueService.GetRemainingItems(queue, currentIndex);
                    if (remainingItems.Count == 0)
                        continue;

                    if (!Confirm($"Mark {remainingItems.Count} remaining items as reviewed with no speaker changes?"))
                        continue;

                    CorrectionSessionStateService.MarkReviewed(
                        workspace.CorrectionState,
                        remainingItems.Select(x => x.ItemId));
                    SaveState(workspace.CorrectionState, correctionStatePath);
                    continue;
                }

                if (command == "change")
                {
                    var assignedSpeaker = PromptForSpeakerAssignment();
                    if (assignedSpeaker is null)
                        continue;

                    var applyMode = PromptForApplyMode();
                    if (string.IsNullOrWhiteSpace(applyMode))
                        continue;

                    if (applyMode == "segment" || applyMode == "run" || applyMode == "bucket")
                    {
                        var scope = applyMode switch
                        {
                            "segment" => CorrectionScope.Segment,
                            "run" => CorrectionScope.ReviewRun,
                            _ => CorrectionScope.SpeakerBucket
                        };

                        ApplyScopedCorrection(
                            workspace,
                            item,
                            run,
                            bucket,
                            assignedSpeaker,
                            scope,
                            correctionStatePath);

                        var reviewedIds = GetScopedItemIds(item, run, bucket, scope);
                        CorrectionSessionStateService.MarkReviewed(workspace.CorrectionState, reviewedIds);
                        CorrectionSessionStateService.ClearSkipped(workspace.CorrectionState, reviewedIds);
                        SaveState(workspace.CorrectionState, correctionStatePath);
                        continue;
                    }

                    if (applyMode == "remaining-bucket")
                    {
                        var remainingBucketItems = CorrectionReviewQueueService.GetRemainingItemsInBucket(
                            queue,
                            currentIndex,
                            bucket.BucketId);

                        if (remainingBucketItems.Count == 0)
                            continue;

                        if (!Confirm($"Apply '{assignedSpeaker}' to {remainingBucketItems.Count} remaining items in this bucket?"))
                            continue;

                        CorrectionBatchService.ApplyToItems(
                            workspace.CorrectionState,
                            remainingBucketItems,
                            assignedSpeaker);

                        var reviewedIds = remainingBucketItems.Select(x => x.ItemId).ToList();
                        CorrectionSessionStateService.MarkReviewed(workspace.CorrectionState, reviewedIds);
                        CorrectionSessionStateService.ClearSkipped(workspace.CorrectionState, reviewedIds);
                        SaveState(workspace.CorrectionState, correctionStatePath);
                        continue;
                    }

                    if (applyMode == "match-speaker")
                    {
                        var matchingItems = CorrectionReviewQueueService.GetItemsMatchingCurrentSpeaker(
                            workspace,
                            item.EffectiveSpeakerLabel);

                        if (matchingItems.Count == 0)
                            continue;

                        if (!Confirm($"Apply '{assignedSpeaker}' to {matchingItems.Count} items matching speaker '{item.EffectiveSpeakerLabel}'?"))
                            continue;

                        CorrectionBatchService.ApplyToItems(
                            workspace.CorrectionState,
                            matchingItems,
                            assignedSpeaker);

                        var reviewedIds = matchingItems.Select(x => x.ItemId).ToList();
                        CorrectionSessionStateService.MarkReviewed(workspace.CorrectionState, reviewedIds);
                        CorrectionSessionStateService.ClearSkipped(workspace.CorrectionState, reviewedIds);
                        SaveState(workspace.CorrectionState, correctionStatePath);
                        continue;
                    }

                    continue;
                }

                if (command == "done")
                    return workspace;

                Console.WriteLine("Invalid command. Try again.");
            }
        }

        private static void DisplayWorkspaceOverview(SpeakerCorrectionWorkspace workspace)
        {
            Console.WriteLine($"Buckets: {workspace.BucketCount}");
            Console.WriteLine($"Runs: {workspace.RunCount}");
            Console.WriteLine($"Suspicious items: {workspace.SuspiciousItemCount}");
        }

        private static void DisplaySessionSummary(SpeakerCorrectionWorkspace workspace)
        {
            var summary = CorrectionSessionStateService.BuildSummary(workspace, workspace.CorrectionState);
            Console.WriteLine($"Overrides: {summary.OverrideCount}");
            Console.WriteLine($"Buckets assigned: {summary.AssignedBucketCount}/{summary.TotalBucketCount}");
            Console.WriteLine($"Suspicious reviewed: {summary.ReviewedSuspiciousCount}");
            Console.WriteLine($"Suspicious remaining: {summary.RemainingSuspiciousCount}");
            Console.WriteLine($"Suspicious skipped: {summary.SkippedSuspiciousCount}");
        }

        private static void DisplayBucket(SpeakerBucket bucket, CorrectionState correctionState)
        {
            var currentAssignment = CorrectionStateOverlayService.GetBucketAssignedSpeaker(
                bucket.BucketId,
                bucket.RawSpeakerLabel,
                correctionState);

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

        private static void DisplayReviewItem(ReviewItem item, ReviewRun run, SpeakerBucket bucket)
        {
            Console.WriteLine($"Item: {item.ItemId}");
            Console.WriteLine(
                $"Time: [{TranscriptFormatter.FormatTimestamp(item.StartSeconds)} - {TranscriptFormatter.FormatTimestamp(item.EndSeconds)}]");
            Console.WriteLine($"Current speaker: {item.EffectiveSpeakerLabel}");
            Console.WriteLine($"Raw speaker: {item.RawSpeakerLabel}");
            Console.WriteLine($"Bucket: {bucket.RawSpeakerLabel}");
            Console.WriteLine($"Run: {run.RunId}");

            if (item.ReviewFlags.Count > 0)
            {
                var reasons = item.ReviewFlags
                    .Select(ReviewItemFlagLabelService.ToFriendlyLabel)
                    .ToList();
                Console.WriteLine($"Reasons: {string.Join(", ", reasons)}");
            }

            Console.WriteLine($"Text: {BuildExcerpt(item.Text)}");
        }

        private static void DisplayBucketAssignmentSummary(SpeakerCorrectionWorkspace workspace)
        {
            Console.WriteLine();
            Console.WriteLine("Bucket assignment summary");

            foreach (var bucket in workspace.SpeakerBuckets.OrderBy(x => x.RawSpeakerLabel, StringComparer.OrdinalIgnoreCase))
            {
                var assignedSpeaker = CorrectionStateOverlayService.GetBucketAssignedSpeaker(
                    bucket.BucketId,
                    bucket.RawSpeakerLabel,
                    workspace.CorrectionState);

                Console.WriteLine($"{bucket.RawSpeakerLabel} -> {assignedSpeaker}");
            }
        }

        private static void DisplayReviewSummary(SpeakerCorrectionWorkspace workspace)
        {
            Console.WriteLine();
            Console.WriteLine("Review summary");

            var summary = CorrectionSessionStateService.BuildSummary(workspace, workspace.CorrectionState);
            Console.WriteLine($"Reviewed suspicious items: {summary.ReviewedSuspiciousCount}");
            Console.WriteLine($"Remaining suspicious items: {summary.RemainingSuspiciousCount}");
            Console.WriteLine($"Overrides applied: {summary.OverrideCount}");
        }

        private static void DisplayFinalSpeakerDistribution(SpeakerCorrectionWorkspace workspace)
        {
            Console.WriteLine();
            Console.WriteLine("Final speaker distribution");

            var distribution = workspace.ReviewRuns
                .SelectMany(x => x.Items)
                .GroupBy(x => x.EffectiveSpeakerLabel, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(x => x.Count())
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var group in distribution)
            {
                Console.WriteLine($"{group.Key} -> {group.Count()} segments");
            }
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

        private static List<string> GetScopedItemIds(
            ReviewItem item,
            ReviewRun run,
            SpeakerBucket bucket,
            CorrectionScope scope)
        {
            return scope switch
            {
                CorrectionScope.Segment => new List<string> { item.ItemId },
                CorrectionScope.ReviewRun => run.Items.Select(x => x.ItemId).ToList(),
                CorrectionScope.SpeakerBucket => bucket.ReviewItemIds.ToList(),
                _ => new List<string> { item.ItemId }
            };
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

            SaveState(correctionState, correctionStatePath);
        }

        private static void SaveState(CorrectionState correctionState, string correctionStatePath)
        {
            CorrectionStateStore.Save(correctionStatePath, correctionState);
        }

        private static int ResolveBucketStartIndex(SpeakerCorrectionWorkspace workspace)
        {
            var bucketIds = workspace.SpeakerBuckets.Select(x => x.BucketId).ToList();
            if (bucketIds.Count == 0)
                return 0;

            if (!string.IsNullOrWhiteSpace(workspace.CorrectionState.Session.LastBucketId))
            {
                var lastIndex = bucketIds.FindIndex(x =>
                    string.Equals(x, workspace.CorrectionState.Session.LastBucketId, StringComparison.OrdinalIgnoreCase));

                if (lastIndex >= 0)
                    return lastIndex;
            }

            foreach (var bucketId in bucketIds)
            {
                if (!workspace.CorrectionState.Session.AssignedBucketIds.Contains(bucketId, StringComparer.OrdinalIgnoreCase))
                    return bucketIds.IndexOf(bucketId);
            }

            return 0;
        }

        private static int GetNextBucketIndex(IReadOnlyList<string> bucketIds, int currentIndex)
        {
            if (bucketIds.Count == 0)
                return 0;

            return Math.Min(bucketIds.Count - 1, currentIndex + 1);
        }

        private static int PromptForBucketIndex(SpeakerCorrectionWorkspace workspace, int currentIndex)
        {
            Console.WriteLine("Available buckets:");

            for (int i = 0; i < workspace.SpeakerBuckets.Count; i++)
            {
                var bucket = workspace.SpeakerBuckets[i];
                var currentAssignment = CorrectionStateOverlayService.GetBucketAssignedSpeaker(
                    bucket.BucketId,
                    bucket.RawSpeakerLabel,
                    workspace.CorrectionState);

                Console.WriteLine($"{i + 1}. {bucket.RawSpeakerLabel} -> {currentAssignment}");
            }

            Console.Write($"Select bucket (1-{workspace.SpeakerBuckets.Count}): ");
            var input = Console.ReadLine()?.Trim();

            if (int.TryParse(input, out var selectedIndex) &&
                selectedIndex >= 1 &&
                selectedIndex <= workspace.SpeakerBuckets.Count)
            {
                return selectedIndex - 1;
            }

            Console.WriteLine("Invalid bucket selection.");
            return currentIndex;
        }

        private static string? PromptForBucketId(SpeakerCorrectionWorkspace workspace)
        {
            var index = PromptForBucketIndex(workspace, 0);
            if (index < 0 || index >= workspace.SpeakerBuckets.Count)
                return null;

            return workspace.SpeakerBuckets[index].BucketId;
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

        private static string? PromptForApplyMode()
        {
            Console.WriteLine("Apply correction:");
            Console.WriteLine("1. This segment");
            Console.WriteLine("2. This run");
            Console.WriteLine("3. This bucket");
            Console.WriteLine("4. All remaining items in this bucket");
            Console.WriteLine("5. All items matching current speaker");
            Console.WriteLine("6. Cancel");
            Console.Write("Select option (1-6): ");

            var option = Console.ReadLine()?.Trim();
            return option switch
            {
                "1" => "segment",
                "2" => "run",
                "3" => "bucket",
                "4" => "remaining-bucket",
                "5" => "match-speaker",
                _ => null
            };
        }

        private static bool Confirm(string message)
        {
            Console.Write($"{message} [y/N]: ");
            var input = Console.ReadLine()?.Trim();
            return string.Equals(input, "y", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(input, "yes", StringComparison.OrdinalIgnoreCase);
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
