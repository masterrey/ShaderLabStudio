using System;
using System.Collections.Generic;
using System.IO;
using GLSLShaderLab.Core.Models;
using GLSLShaderLab.Core.Services;
using GLSLShaderLab.Engine.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GLSLShaderLab.Engine.Services;

public sealed class ShaderToyRenderer : IDisposable
{
    private int _vao;
    private int _vbo;
    private int _ebo;
    private ShaderProgram? _shader;
    private ShaderProgram? _copyShader;
    private RenderBuffer? _frontBuffer;
    private RenderBuffer? _backBuffer;
    private readonly Dictionary<int, int> _channelTextures = new();
    private ModelGeometry? _model;
    private bool _initialized;
    private int _width;
    private int _height;
    private float _time;
    private bool _isPaused;
    private float _mouseX;
    private float _mouseY;
    private bool _mouseDown;
    private string _currentVertexSource = ShaderTemplateCatalog.ModelVertex;
    private string _currentFragmentSource = string.Empty;

    private Vector3 _cameraPos = new(0.0f, 0.0f, 3.0f);
    private Vector3 _cameraFront = new(0.0f, 0.0f, -1.0f);
    private Vector3 _cameraUp = Vector3.UnitY;
    private float _yaw = -90.0f;
    private float _pitch;
    private float _fov = 45.0f;
    private float _rotationY;

    public RenderMode Mode { get; private set; } = RenderMode.TwoD;
    public string? CurrentModelPath { get; private set; }
    public ShaderCompileMessage LastCompileMessage { get; private set; } = new(true, "Ready");

    public void Initialize(int width, int height, string initialFragment)
    {
        if (_initialized) return;

        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        CreateFullscreenQuad();
        _frontBuffer = new RenderBuffer(_width, _height);
        _backBuffer = new RenderBuffer(_width, _height);

        var copyFragment = """
#version 330 core
out vec4 FragColor;
uniform sampler2D inputTexture;
uniform vec2 iResolution;
void main()
{
    vec2 uv = gl_FragCoord.xy / iResolution;
    FragColor = texture(inputTexture, uv);
}
""";

        var (copyProgram, copyResult) = ShaderProgram.TryCreate(ShaderTemplateCatalog.FullscreenVertex, copyFragment);
        _copyShader = copyProgram;
        LastCompileMessage = copyResult;

        _currentFragmentSource = initialFragment;
        if (!copyResult.Success)
            throw new InvalidOperationException($"Falha no shader interno de vídeo: {copyResult.Message}");
        _initialized = true;
        Compile(initialFragment);
    }

    public IReadOnlyList<ModelAsset> DiscoverModels(string modelsDirectory)
    {
        if (!Directory.Exists(modelsDirectory))
        {
            return [];
        }

        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".glb", ".gltf", ".obj", ".fbx", ".dae", ".3ds"
        };

        var results = new List<ModelAsset>();
        foreach (var file in Directory.EnumerateFiles(modelsDirectory, "*.*", SearchOption.AllDirectories))
        {
            if (!extensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            var relativeDir = Path.GetRelativePath(modelsDirectory, Path.GetDirectoryName(file) ?? modelsDirectory);
            var baseName = Path.GetFileNameWithoutExtension(file);
            var name = relativeDir == "." ? baseName : $"{relativeDir}/{baseName}";
            results.Add(new ModelAsset(name, file));
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public bool TryLoadModel(string? modelPath, out string message)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            message = "Model path is empty.";
            return false;
        }

        if (!File.Exists(modelPath))
        {
            message = $"Model file not found: {modelPath}";
            return false;
        }

        try
        {
            _model?.Dispose();
            _model = new ModelGeometry(modelPath);
            CurrentModelPath = modelPath;
            message = $"Loaded model: {Path.GetFileName(modelPath)}";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public ShaderCompileMessage Compile(string fragmentSource, string? vertexSource = null)
    {
        if (!_initialized && _copyShader == null)
        {
            LastCompileMessage = new ShaderCompileMessage(false, "Renderer not initialized.");
            return LastCompileMessage;
        }

        if (!string.IsNullOrWhiteSpace(vertexSource))
        {
            _currentVertexSource = vertexSource;
        }

        _currentFragmentSource = fragmentSource;
        var vertexCode = Mode == RenderMode.ThreeD
            ? _currentVertexSource
            : ShaderTemplateCatalog.FullscreenVertex;

        var (newProgram, result) = ShaderProgram.TryCreate(vertexCode, fragmentSource);
        if (newProgram is not null)
        {
            _shader?.Dispose();
            _shader = newProgram;
        }

        LastCompileMessage = result;
        return LastCompileMessage;
    }

    public ShaderCompileMessage SetRenderMode(RenderMode mode, string? vertexSource = null)
    {
        Mode = mode;
        return Compile(_currentFragmentSource, vertexSource ?? _currentVertexSource);
    }

    public void SetPaused(bool paused) => _isPaused = paused;

    public void ResetTime() => _time = 0f;

    public void ResetCamera()
    {
        _cameraPos = new Vector3(0.0f, 0.0f, 3.0f);
        _cameraFront = new Vector3(0.0f, 0.0f, -1.0f);
        _cameraUp = Vector3.UnitY;
        _yaw = -90.0f;
        _pitch = 0.0f;
        _fov = 45.0f;
        _rotationY = 0.0f;
    }

    public void MoveCamera(float forwardAxis, float rightAxis, float deltaSeconds)
    {
        if (Mode != RenderMode.ThreeD)
        {
            return;
        }

        var speed = 2.5f * deltaSeconds;
        var right = Vector3.Normalize(Vector3.Cross(_cameraFront, _cameraUp));
        _cameraPos += _cameraFront * forwardAxis * speed;
        _cameraPos += right * rightAxis * speed;
    }

    public void RotateCamera(float deltaYaw, float deltaPitch)
    {
        if (Mode != RenderMode.ThreeD)
        {
            return;
        }

        _yaw += deltaYaw;
        _pitch = Math.Clamp(_pitch + deltaPitch, -89.0f, 89.0f);
        UpdateCameraFront();
    }

    public void ZoomCamera(float delta)
    {
        if (Mode != RenderMode.ThreeD)
        {
            return;
        }

        _fov = Math.Clamp(_fov - delta, 20.0f, 80.0f);
    }

    public void SetMouse(float x, float y, bool isDown)
    {
        _mouseX = x;
        _mouseY = y;
        _mouseDown = isDown;
    }

    public bool TrySetChannelTexture(int channel, string? path, out string message)
    {
        if (channel < 0 || channel > 3)
        {
            message = "Channel must be between 0 and 3.";
            return false;
        }

        if (_channelTextures.TryGetValue(channel, out int oldTex))
        {
            GL.DeleteTexture(oldTex);
            _channelTextures.Remove(channel);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            message = $"Cleared iChannel{channel}.";
            return true;
        }

        if (!File.Exists(path))
        {
            message = $"Texture file not found: {path}";
            return false;
        }

        try
        {
            using var image = Image.Load<Rgba32>(path);
            image.Mutate(x => x.Flip(FlipMode.Vertical));
            var pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels);

            int texId = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            _channelTextures[channel] = texId;
            message = $"Loaded iChannel{channel}: {Path.GetFileName(path)}";
            return true;
        }
        catch (Exception ex)
        {
            message = ex.Message;
            return false;
        }
    }

    public void Resize(int width, int height)
    {
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        _frontBuffer?.Resize(_width, _height);
        _backBuffer?.Resize(_width, _height);
        GL.Viewport(0, 0, _width, _height);
    }

    public void Render(double elapsedSeconds)
    {
        if (!_initialized || _shader is null || _copyShader is null || _frontBuffer is null || _backBuffer is null)
        {
            return;
        }

        if (!_isPaused)
        {
            _time += (float)elapsedSeconds;
            if (Mode == RenderMode.ThreeD)
            {
                _rotationY += (float)elapsedSeconds * 30.0f;
            }
        }

        RenderToBuffer(_frontBuffer, _backBuffer.TextureId);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, _width, _height);
        GL.Disable(EnableCap.DepthTest);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        _copyShader.Use();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, _frontBuffer.TextureId);
        _copyShader.SetInt("inputTexture", 0);
        _copyShader.SetVector2("iResolution", _width, _height);

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);

        (_frontBuffer, _backBuffer) = (_backBuffer, _frontBuffer);
    }

    private void RenderToBuffer(RenderBuffer target, int previousFrameTexture)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, target.FramebufferId);
        GL.Viewport(0, 0, _width, _height);
        GL.Enable(EnableCap.DepthTest);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _shader!.Use();
        _shader.SetFloat("iTime", _time);
        _shader.SetVector2("iResolution", _width, _height);
        _shader.SetVector2("iMouse", _mouseX, _height - _mouseY);
        _shader.SetInt("iMouseClick", _mouseDown ? 1 : 0);
        _shader.SetVector3("viewPos", _cameraPos);

        BindChannels(previousFrameTexture);

        if (Mode == RenderMode.ThreeD && _model is not null)
        {
            var model = Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_rotationY));
            var view = Matrix4.LookAt(_cameraPos, _cameraPos + _cameraFront, _cameraUp);
            var projection = Matrix4.CreatePerspectiveFieldOfView(
                MathHelper.DegreesToRadians(_fov),
                _width / (float)_height,
                0.1f,
                100.0f);

            _shader.SetMatrix4("model", model);
            _shader.SetMatrix4("view", view);
            _shader.SetMatrix4("projection", projection);

            _model.Render();
            return;
        }

        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, 0);
    }

    private void BindChannels(int previousFrameTexture)
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, previousFrameTexture);
        _shader!.SetInt("iChannel0", 0);
        _shader.SetInt("texture0", 0);
        _shader.SetInt("texture1", 0);

        for (int i = 1; i <= 3; i++)
        {
            GL.ActiveTexture(TextureUnit.Texture0 + i);
            if (_channelTextures.TryGetValue(i, out int channelTex))
            {
                GL.BindTexture(TextureTarget.Texture2D, channelTex);
            }
            else
            {
                GL.BindTexture(TextureTarget.Texture2D, previousFrameTexture);
            }

            _shader!.SetInt($"iChannel{i}", i);
            _shader.SetInt($"texture{i}", i);
            _shader.SetInt($"texture{i + 1}", i);
        }

        if (_channelTextures.TryGetValue(0, out int texture0))
        {
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, texture0);
            _shader!.SetInt("iChannel0", 0);
            _shader.SetInt("texture0", 0);
            _shader.SetInt("texture1", 0);
        }
    }

    private void UpdateCameraFront()
    {
        var front = Vector3.Zero;
        front.X = (float)Math.Cos(MathHelper.DegreesToRadians(_yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch));
        front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(_pitch));
        front.Z = (float)Math.Sin(MathHelper.DegreesToRadians(_yaw)) * (float)Math.Cos(MathHelper.DegreesToRadians(_pitch));
        _cameraFront = Vector3.Normalize(front);
    }

    private void CreateFullscreenQuad()
    {
        float[] vertices =
        [
            -1f, -1f, 0f, 0f,
             1f, -1f, 1f, 0f,
             1f,  1f, 1f, 1f,
            -1f,  1f, 0f, 1f
        ];

        uint[] indices = [0, 1, 2, 2, 3, 0];

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), indices, BufferUsageHint.StaticDraw);

        int stride = 4 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        GL.BindVertexArray(0);
    }

    public void Dispose()
    {
        _shader?.Dispose();
        _copyShader?.Dispose();
        _model?.Dispose();

        foreach (var tex in _channelTextures.Values)
        {
            GL.DeleteTexture(tex);
        }

        _channelTextures.Clear();

        _frontBuffer?.Dispose();
        _backBuffer?.Dispose();

        if (_vao != 0) GL.DeleteVertexArray(_vao);
        if (_vbo != 0) GL.DeleteBuffer(_vbo);
        if (_ebo != 0) GL.DeleteBuffer(_ebo);
    }
}
