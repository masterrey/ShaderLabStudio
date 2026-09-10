using System;
using GLSLShaderLab.Core.Services;
using GLSLShaderLab.Core.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace GLSLShaderLab.Engine.Rendering;

internal sealed class ShaderProgram : IDisposable
{
    public int Handle { get; private set; }

    private ShaderProgram(int handle)
    {
        Handle = handle;
    }

    public static (ShaderProgram? program, ShaderCompileMessage result) TryCreate(string vertexCode, string fragmentCode)
    {
        int vertex = 0;
        int fragment = 0;
        int program = 0;
        bool linkedSuccessfully = false;

        try
        {
            vertex = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertex, vertexCode);
            GL.CompileShader(vertex);
            GL.GetShader(vertex, ShaderParameter.CompileStatus, out int vertexOk);
            if (vertexOk == 0)
            {
                var log = GL.GetShaderInfoLog(vertex);
                return (null, CreateMessage(log, "vertex"));
            }

            fragment = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragment, fragmentCode);
            GL.CompileShader(fragment);
            GL.GetShader(fragment, ShaderParameter.CompileStatus, out int fragmentOk);
            if (fragmentOk == 0)
            {
                var log = GL.GetShaderInfoLog(fragment);
                return (null, CreateMessage(log, "fragment"));
            }

            program = GL.CreateProgram();
            GL.AttachShader(program, vertex);
            GL.AttachShader(program, fragment);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int programOk);
            if (programOk == 0)
            {
                var log = GL.GetProgramInfoLog(program);
                return (null, new ShaderCompileMessage(false, string.IsNullOrWhiteSpace(log) ? "Unknown shader link error." : log, "link"));
            }

            linkedSuccessfully = true;

            return (new ShaderProgram(program), new ShaderCompileMessage(true, "Shader compiled successfully."));
        }
        catch (Exception ex)
        {
            return (null, new ShaderCompileMessage(false, ex.Message, "runtime"));
        }
        finally
        {
            if (vertex != 0)
            {
                if (program != 0) GL.DetachShader(program, vertex);
                GL.DeleteShader(vertex);
            }

            if (fragment != 0)
            {
                if (program != 0) GL.DetachShader(program, fragment);
                GL.DeleteShader(fragment);
            }

            if (program != 0 && !linkedSuccessfully)
            {
                GL.DeleteProgram(program);
            }
        }
    }

    public void Use() => GL.UseProgram(Handle);

    public void SetFloat(string name, float value)
    {
        var loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform1(loc, value);
    }

    public void SetInt(string name, int value)
    {
        var loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform1(loc, value);
    }

    public void SetVector2(string name, float x, float y)
    {
        var loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform2(loc, x, y);
    }

    public void SetVector3(string name, Vector3 value)
    {
        var loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.Uniform3(loc, value);
    }

    public void SetMatrix4(string name, Matrix4 value)
    {
        var loc = GL.GetUniformLocation(Handle, name);
        if (loc >= 0) GL.UniformMatrix4(loc, false, ref value);
    }

    private static ShaderCompileMessage CreateMessage(string log, string stage)
    {
        if (string.IsNullOrWhiteSpace(log))
        {
            return new ShaderCompileMessage(false, "Unknown shader compile error.", stage);
        }

        var line = ShaderDiagnosticParser.TryParseLine(log);
        return new ShaderCompileMessage(false, log, stage, line);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            GL.DeleteProgram(Handle);
            Handle = 0;
        }
    }
}
