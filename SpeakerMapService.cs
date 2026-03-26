using System;
using System.Collections.Generic;
using System.Text;

namespace MinuteMaker
{
    public static class SpeakerMapService
    {
        public static Dictionary<string, string> BuildInteractiveMap(IEnumerable<string> speakers)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Console.WriteLine();
            Console.WriteLine("Detected speakers:");
            foreach (var speaker in speakers)
            {
                Console.WriteLine($" - {speaker}");
            }

            Console.WriteLine();

            // Ask user if they want to rename
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

            // If No → return identity mapping
            if (choice == "2")
            {
                foreach (var speaker in speakers)
                {
                    result[speaker] = speaker;
                }

                return result;
            }

            // If Yes → run rename loop
            foreach (var speaker in speakers)
            {
                Console.Write($"Enter display name for {speaker} (blank to keep as-is): ");
                var input = Console.ReadLine()?.Trim();

                result[speaker] = string.IsNullOrWhiteSpace(input)
                    ? speaker
                    : input;
            }

            return result;
        }
    }
}
