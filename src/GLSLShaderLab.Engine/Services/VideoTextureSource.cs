using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GLSLShaderLab.Engine.Services;

/// <summary>
/// Decodes a video file into raw RGBA frames by piping from an external ffmpeg process,
/// so iChannel inputs can be fed by video instead of static images.
/// </summary>
public sealed class VideoTextureSource : IChannelFrameSource
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".m4v", ".mpg", ".mpeg"
    };

    private static readonly Regex DimensionsPattern = new(@"Video:.*?(\d{2,5})x(\d{2,5})", RegexOptions.Compiled);
    private const int MaxConsecutiveRestartFailures = 3;

    private readonly string _ffmpegPath;
    private Process? _process;
    private Stream? _frameStream;
    private int _consecutiveFailures;

    public string FilePath { get; }
    public int Width { get; }
    public int Height { get; }
    public int FrameByteCount => Width * Height * 4;
    public bool Failed { get; private set; }

    private VideoTextureSource(string ffmpegPath, string filePath, int width, int height)
    {
        _ffmpegPath = ffmpegPath;
        FilePath = filePath;
        Width = width;
        Height = height;
    }

    public static bool IsVideoFile(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public static bool TryCreate(string filePath, out VideoTextureSource? source, out string message)
    {
        source = null;

        var ffmpegPath = ResolveFfmpegPath();
        if (ffmpegPath is null)
        {
            message = "ffmpeg not found. Install ffmpeg (or place ffmpeg.exe next to the app) to load videos into iChannels.";
            return false;
        }

        if (!TryGetDimensions(ffmpegPath, filePath, out var width, out var height, out message))
        {
            return false;
        }

        var video = new VideoTextureSource(ffmpegPath, filePath, width, height);
        if (!video.StartPipeline(out message))
        {
            return false;
        }

        source = video;
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Reads the next decoded frame into <paramref name="buffer"/>. At end of stream the
    /// video loops automatically. Returns false when no new frame is available, in which
    /// case the caller should keep displaying the previous frame.
    /// </summary>
    public bool TryReadNextFrame(byte[] buffer)
    {
        if (Failed || buffer.Length < FrameByteCount)
        {
            return false;
        }

        if (_frameStream is not null && ReadExact(buffer))
        {
            _consecutiveFailures = 0;
            return true;
        }

        // End of stream (or broken pipe): restart ffmpeg to loop the video.
        ClosePipeline();
        if (++_consecutiveFailures > MaxConsecutiveRestartFailures)
        {
            Failed = true;
            return false;
        }

        if (!StartPipeline(out _))
        {
            return false;
        }

        if (ReadExact(buffer))
        {
            _consecutiveFailures = 0;
            return true;
        }

        return false;
    }

    private bool ReadExact(byte[] buffer)
    {
        var stream = _frameStream;
        if (stream is null)
        {
            return false;
        }

        var total = 0;
        while (total < buffer.Length)
        {
            int read;
            try
            {
                read = stream.Read(buffer, total, buffer.Length - total);
            }
            catch (IOException)
            {
                return false;
            }

            if (read <= 0)
            {
                return false;
            }

            total += read;
        }

        return true;
    }

    private bool StartPipeline(out string message)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = $"-hide_banner -loglevel error -nostdin -i \"{FilePath}\" -an -f rawvideo -pix_fmt rgba -",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            _process = Process.Start(startInfo);
            if (_process is null)
            {
                message = $"Could not start ffmpeg for: {FilePath}";
                return false;
            }

            // Drain stderr so a chatty ffmpeg never blocks on a full pipe buffer.
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginErrorReadLine();
            _frameStream = _process.StandardOutput.BaseStream;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            _process = null;
            _frameStream = null;
            return false;
        }
    }

    private void ClosePipeline()
    {
        try
        {
            if (_process is { HasExited: false })
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }

        _process?.Dispose();
        _process = null;
        _frameStream = null;
    }

    private static bool TryGetDimensions(string ffmpegPath, string filePath, out int width, out int height, out string message)
    {
        width = 0;
        height = 0;

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-hide_banner -nostdin -i \"{filePath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        string output;
        try
        {
            using var probe = Process.Start(startInfo);
            if (probe is null)
            {
                message = "Could not start ffmpeg to probe the video.";
                return false;
            }

            output = probe.StandardError.ReadToEnd();
            if (!probe.WaitForExit(5000))
            {
                probe.Kill(entireProcessTree: true);
                message = "ffmpeg timed out while reading the video.";
                return false;
            }
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }

        var match = DimensionsPattern.Match(output);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out width)
            || !int.TryParse(match.Groups[2].Value, out height)
            || width <= 0 || height <= 0)
        {
            message = $"No video stream found in: {Path.GetFileName(filePath)}";
            width = 0;
            height = 0;
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string? ResolveFfmpegPath()
    {
        var baseDirectory = AppContext.BaseDirectory;
        foreach (var name in new[] { "ffmpeg.exe", "ffmpeg" })
        {
            var local = Path.Combine(baseDirectory, name);
            if (File.Exists(local))
            {
                return local;
            }
        }

        var pathEnvironment = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathEnvironment))
        {
            foreach (var directory in pathEnvironment.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                foreach (var name in new[] { "ffmpeg.exe", "ffmpeg" })
                {
                    var candidate = Path.Combine(directory, name);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        return null;
    }

    public void Dispose()
    {
        Failed = true;
        ClosePipeline();
    }
}
