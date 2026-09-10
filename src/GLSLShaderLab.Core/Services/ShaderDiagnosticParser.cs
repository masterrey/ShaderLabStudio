using System.Text.RegularExpressions;

namespace GLSLShaderLab.Core.Services;

public static class ShaderDiagnosticParser
{
    // NVIDIA: 0(12), Mesa: 0:12(4), AMD/Intel: ERROR: 0:12:
    public static int? TryParseLine(string log)
    {
        var match = Regex.Match(log, @"(?m)^\s*(?:(?:ERROR|WARNING):\s*)?\d+(?:\((?<line>\d+)\)|:(?<line>\d+)(?=[:(]))", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups["line"].Value, out var line) && line > 0
            ? line : null;
    }
}
