using MinuteMaker.Models.Corrections;
using MinuteMaker.Services.Output;

namespace MinuteMaker.Persistence.Corrections
{
    public static class CorrectionStateStore
    {
        public static CorrectionState Load(string path)
        {
            if (!File.Exists(path))
                return new CorrectionState();

            return JsonFileService.ReadJson<CorrectionState>(path) ?? new CorrectionState();
        }

        public static void Save(string path, CorrectionState state)
        {
            ArgumentNullException.ThrowIfNull(state);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            state.UpdatedAtUtc = DateTimeOffset.UtcNow;
            JsonFileService.WriteJson(path, state);
        }
    }
}
