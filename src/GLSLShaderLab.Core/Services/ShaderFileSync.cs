namespace GLSLShaderLab.Core.Services;

/// <summary>Tracks one shader independently of the UI and graphics context.</summary>
public sealed class ShaderFileSync(string path, string source)
{
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    private string _baseline = Normalize(source);
    private string? _candidate;
    public bool HasConflict { get; private set; }
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    // Two identical reads debounce multi-step/atomic saves made by external editors.
    public string? Poll(string editorSource)
    {
        var disk = Normalize(File.ReadAllText(Path));
        if (disk == _baseline) { _candidate = null; HasConflict = false; return null; }
        if (_candidate != disk) { _candidate = disk; return null; }
        HasConflict = Normalize(editorSource) != _baseline && Normalize(editorSource) != disk;
        if (HasConflict) return null;
        _baseline = disk;
        _candidate = null;
        return disk;
    }

    public bool AutoSave(string editorSource)
    {
        // Hold an exclusive handle across comparison and write: an external edit
        // between polling and autosave must never be overwritten.
        using var stream = new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var disk = Normalize(reader.ReadToEnd());
        var local = Normalize(editorSource);
        if (disk != _baseline && disk != local) return false;
        if (disk != local)
        {
            stream.Position = 0;
            using var writer = new StreamWriter(stream, reader.CurrentEncoding, leaveOpen: true);
            writer.Write(editorSource);
            writer.Flush();
            stream.SetLength(stream.Position);
        }
        _baseline = local;
        _candidate = null;
        HasConflict = false;
        return true;
    }
}
