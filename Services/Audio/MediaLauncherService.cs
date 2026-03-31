using System.Diagnostics;

namespace MinuteMaker.Services.Audio
{
    public static class MediaLauncherService
    {
        public static bool TryOpenInVlc(string mediaPath, double startSeconds, string? vlcPath)
        {
            if (string.IsNullOrWhiteSpace(mediaPath) || !File.Exists(mediaPath))
                return false;

            var candidates = new List<string>();

            if (!string.IsNullOrWhiteSpace(vlcPath))
                candidates.Add(vlcPath);

            candidates.Add("vlc");

            // Optional: common Windows paths
            candidates.Add(@"C:\Program Files\VideoLAN\VLC\vlc.exe");
            candidates.Add(@"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe");

            var wholeSeconds = Math.Max(0, (int)Math.Floor(startSeconds));

            foreach (var exe in candidates)
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = $"--start-time={wholeSeconds} \"{mediaPath}\"",
                        UseShellExecute = true
                    };

                    Process.Start(startInfo);
                    return true;
                }
                catch
                {
                    // try next option
                }
            }

            return false;
        }

        public static string FormatTimestamp(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalHours >= 1
                ? ts.ToString(@"hh\:mm\:ss")
                : ts.ToString(@"mm\:ss");
        }
    }
}
