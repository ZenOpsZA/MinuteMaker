using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace MinuteMaker
{
    public static class JsonFileService
    {
        private static readonly JsonSerializerOptions ReadOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        public static T? ReadJson<T>(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }

        public static void WriteJson<T>(string path, T value)
        {
            var json = JsonSerializer.Serialize(value, WriteOptions);
            File.WriteAllText(path, json);
        }
    }
}
