namespace GLSLShaderLab.Engine.Services;

/// <summary>
/// Represents a source of RGBA frames that can be uploaded to a GL texture each render tick.
/// Implemented by <see cref="VideoTextureSource"/> (file/video) and <see cref="WebcamTextureSource"/> (live camera).
/// </summary>
public interface IChannelFrameSource : IDisposable
{
    int Width { get; }
    int Height { get; }
    int FrameByteCount { get; }
    bool Failed { get; }

    /// <summary>
    /// Attempts to read the next frame into <paramref name="buffer"/>.
    /// Returns true if a new frame was produced; false otherwise (caller keeps previous frame).
    /// </summary>
    bool TryReadNextFrame(byte[] buffer);
}
