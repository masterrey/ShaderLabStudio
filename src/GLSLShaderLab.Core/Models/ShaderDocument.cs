using System.Collections.Generic;

namespace GLSLShaderLab.Core.Models;

public sealed class ShaderDocument
{
    public string Name { get; set; } = "Untitled";
    public string VertexSource { get; set; } = string.Empty;
    public string FragmentSource { get; set; } = string.Empty;
    public bool AutoCompile { get; set; } = true;
    public bool IsPaused { get; set; }
    public bool AutoRotateModel { get; set; } = true;
    public bool IsFullscreen { get; set; }
    public int ResolutionWidth { get; set; } = 1280;
    public int ResolutionHeight { get; set; } = 720;
    public RenderMode RenderMode { get; set; } = RenderMode.TwoD;
    public string? SelectedModelPath { get; set; }
    public List<ShaderChannel> Channels { get; set; } =
    [
        new() { Index = 0 },
        new() { Index = 1 },
        new() { Index = 2 },
        new() { Index = 3 }
    ];
}
