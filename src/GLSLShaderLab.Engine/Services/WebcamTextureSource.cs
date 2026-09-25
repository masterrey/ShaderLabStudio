using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GLSLShaderLab.Engine.Services;

/// <summary>
/// Captures live frames from a webcam via ffmpeg DirectShow and exposes them as RGBA buffers,
/// so an iChannel can be fed by a real-time camera stream.
/// </summary>
public sealed class WebcamTextureSource : IChannelFrameSource
{
    private const int MaxConsecutiveFailures = 5;

    private static readonly Regex CameraPattern = new(
        @"Video\s+([0-9]+):\s*(.+?)(?:\r\n|\n|$)",
        RegexOptions.Compiled);

    private readonly string _ffmpegPath;
    private readonly string _deviceName;
    private Process? _process;
    private Stream? _frameStream;
    private int _consecutiveFailures;

    public string DeviceName { get; }
    public int Width { get; }
    public int Height { get; }
    public int FrameByteCount => Width * Height * 4;
    public bool Failed { get; private set; }

    private WebcamTextureSource(string ffmpegPath, string deviceName, int width, int height)
    {
        _ffmpegPath = ffmpegPath;
        _deviceName = deviceName;
        DeviceName = deviceName;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Enumerates available DirectShow video devices on the system.
    /// </summary>
    public static IReadOnlyList<string> ListCameras()
    {
        var ffmpegPath = ResolveFfmpegPath();
        if (ffmpegPath is null)
        {
            return [];
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -f dshow -list_devices 1 -i dummy",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null) return [];

            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            var cameras = new List<string>();
            foreach (Match match in CameraPattern.Matches(error))
            {
                var name = match.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    cameras.Add(name);
                }
            }

            return cameras;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Attempts to start capturing from the specified webcam device.
    /// </summary>
    public static bool TryCreate(string deviceName, out WebcamTextureSource? source, out string message)
    {
        source = null;

        var ffmpegPath = ResolveFfmpegPath();
        if (ffmpegPath is null)
        {
            message = "ffmpeg não encontrado. Instale o ffmpeg (ou coloque ffmpeg.exe ao lado do app) para usar webcam.";
            return false;
        }

        // First, try to get the resolution by doing a quick test capture of one frame.
        if (!TryDetectResolution(ffmpegPath, deviceName, out var width, out var height, out message))
        {
            return false;
        }

        var webcam = new WebcamTextureSource(ffmpegPath, deviceName, width, height);
        if (!webcam.StartCapture(out message))
        {
            return false;
        }

        source = webcam;
        message = string.Empty;
        return true;
    }

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

        // Stream broke (camera disconnected, etc.) — try to restart.
        ClosePipeline();
        if (++_consecutiveFailures > MaxConsecutiveFailures)
        {
            Failed = true;
            return false;
        }

        if (!StartCapture(out _))
        {
            return false;
        }

        return ReadExact(buffer);
    }

    public void Dispose()
    {
        ClosePipeline();
    }

    private bool ReadExact(byte[] buffer)
    {
        var stream = _frameStream;
        if (stream is null) return false;

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

            if (read <= 0) return false;
            total += read;
        }

        return true;
    }

    private bool StartCapture(out string message)
    {
        ClosePipeline();

        var startInfo = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            // -f dshow: DirectShow input on Windows
            // -i video="...": select the camera by name
            // -an: no audio
            // -f rawvideo -pix_fmt rgba: output raw RGBA frames
            // -r 30: target ~30 fps (ffmpeg will output at native rate, this is a hint)
            Arguments = $"-hide_banner -loglevel error -nostdin -f dshow -i video=\"{_deviceName}\" -an -f rawvideo -pix_fmt rgba -",
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
                message = "Falha ao iniciar o processo ffmpeg.";
                return false;
            }

            _frameStream = _process.StandardOutput.BaseStream;
            _consecutiveFailures = 0;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = $"Erro ao capturar da webcam: {ex.Message}";
            return false;
        }
    }

    private static bool TryDetectResolution(string ffmpegPath, string deviceName, out int width, out int height, out string message)
    {
        width = 640;
        height = 480;
        message = string.Empty;

        // Capture exactly one frame to determine resolution.
        // Use -frames:v 1 to grab just one frame, then check stderr for dimensions.
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-hide_banner -f dshow -i video=\"{deviceName}\" -an -frames:v 1 -f rawvideo -pix_fmt rgba NUL",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null) return true; // fallback to 640x480

            var errorOutput = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(10000))
            {
                process.Kill();
            }

            // Try to extract dimensions from stderr.
            // ffmpeg outputs something like: "video:640x480" or "Stream #0:0 ... 640x480"
            var match = Regex.Match(errorOutput, @"(\d{2,5})x(\d{2,5})");
            if (match.Success)
            {
                width = int.Parse(match.Groups[1].Value);
                height = int.Parse(match.Groups[2].Value);
            }

            return true;
        }
        catch
        {
            // Fallback to default resolution.
            return true;
        }
    }

    private void ClosePipeline()
    {
        try { _frameStream?.Dispose(); } catch { /* ignore */ }
        _frameStream = null;

        try
        {
            if (_process is not null)
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                }
                _process.Dispose();
            }
        }
        catch { /* ignore */ }
        _process = null;
    }

    private static string? ResolveFfmpegPath()
    {
        // Check next to the app.
        var appDir = AppContext.BaseDirectory;
        var localPath = Path.Combine(appDir, "ffmpeg.exe");
        if (File.Exists(localPath)) return localPath;

        // Check PATH.
        try
        {
            var probe = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(probe);
            p?.WaitForExit(3000);
            if (p is not null && p.ExitCode == 0)
            {
                return "ffmpeg";
            }
        }
        catch { /* not in PATH */ }

        return null;
    }
}
