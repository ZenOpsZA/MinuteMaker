using System;
using System.Collections.Generic;
using System.Text;

namespace MinuteMaker
{
    public static class SpeakerMapService
    {
        public static Dictionary<string, string> BuildInteractiveMap(
            IEnumerable<string> speakers,
            IReadOnlyDictionary<string, SpeakerSample> samples,
            string originalMediaPath,
            string? vlcPath)
        {
            var speakerList = speakers.ToList();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine();
            Console.WriteLine("Detected speakers:");
            foreach (var speaker in speakerList)
            {
                if (samples.TryGetValue(speaker, out var sample))
                {
                    Console.WriteLine(
                        $" - {speaker} (sample {MediaLauncherService.FormatTimestamp(sample.StartSeconds)})");
                }
                else
                {
                    Console.WriteLine($" - {speaker}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("Do you want to rename speakers?");
            Console.WriteLine("1. Yes");
            Console.WriteLine("2. No");

            string? choice;
            do
            {
                Console.Write("Select option (1-2): ");
                choice = Console.ReadLine()?.Trim();
            }
            while (choice != "1" && choice != "2");

            if (choice == "2")
            {
                foreach (var speaker in speakerList)
                {
                    result[speaker] = speaker;
                }

                return result;
            }

            foreach (var speaker in speakerList)
            {
                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Speaker: {speaker}");

                    if (samples.TryGetValue(speaker, out var sample))
                    {
                        Console.WriteLine(
                            $"Sample: {MediaLauncherService.FormatTimestamp(sample.StartSeconds)} - \"{sample.TextPreview}\"");
                        Console.WriteLine("1. Open recording in VLC at this point");
                        Console.WriteLine("2. Enter display name");
                        Console.WriteLine("3. Keep as-is");
                    }
                    else
                    {
                        Console.WriteLine("No sample available for this speaker.");
                        Console.WriteLine("1. Enter display name");
                        Console.WriteLine("2. Keep as-is");
                    }

                    Console.Write("Select option: ");
                    var option = Console.ReadLine()?.Trim();

                    if (samples.TryGetValue(speaker, out sample))
                    {
                        if (option == "1")
                        {
                            var opened = MediaLauncherService.TryOpenInVlc(
                                originalMediaPath,
                                sample.StartSeconds,
                                vlcPath);

                            if (!opened)
                            {
                                Console.WriteLine("Unable to open VLC. Check VLC installation/path.");
                            }

                            continue;
                        }

                        if (option == "2")
                        {
                            Console.Write($"Enter display name for {speaker} (blank to keep as-is): ");
                            var input = Console.ReadLine()?.Trim();

                            result[speaker] = string.IsNullOrWhiteSpace(input)
                                ? speaker
                                : input;

                            break;
                        }

                        if (option == "3")
                        {
                            result[speaker] = speaker;
                            break;
                        }
                    }
                    else
                    {
                        if (option == "1")
                        {
                            Console.Write($"Enter display name for {speaker} (blank to keep as-is): ");
                            var input = Console.ReadLine()?.Trim();

                            result[speaker] = string.IsNullOrWhiteSpace(input)
                                ? speaker
                                : input;

                            break;
                        }

                        if (option == "2")
                        {
                            result[speaker] = speaker;
                            break;
                        }
                    }

                    Console.WriteLine("Invalid selection. Try again.");
                }
            }

            return result;
        }
    }
}