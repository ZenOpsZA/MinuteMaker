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
