using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GLSLShaderLab.Core.Models;
using GLSLShaderLab.Core.Services;
using GLSLShaderLab.Engine.Services;
using OpenTK.GLControl;
using WpfBrush = System.Windows.Media.Brush;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace GLSLShaderLab.App.Wpf;

public partial class MainWindow : Window
{
    private static readonly string[] FundamentalDirectories = ["Shaders", "Mesh", "Textures"];
    private const double MinEditorFontSize = 10d;
    private const double MaxEditorFontSize = 30d;
    private const double EditorFontStep = 1d;
    private const string Indentation = "    ";
    
    // Theme Support
    private enum AppThemeMode { Dark, Light }
    private AppThemeMode _currentTheme = AppThemeMode.Dark;
    private static readonly WpfBrush EditorDefaultForeground = CreateBrush("#D4D4D4");
    private readonly SessionStore _sessionStore = new();
    private readonly ShaderToyRenderer _renderer = new();
    private readonly DispatcherTimer _renderTimer;
    private readonly DispatcherTimer _compileDebounceTimer;
    private readonly Stopwatch _frameStopwatch = Stopwatch.StartNew();

    private bool _videoFailed;
    private ShaderCompileMessage? _lastError;
    private GLControl? _glControl;
    private ShaderDocument _document = ShaderTemplateCatalog.CreateDefaultDocument();
    private string? _currentFilePath;
    private ShaderFileSync? _fragmentFile;
    private ShaderFileSync? _vertexFile;
    private readonly DispatcherTimer _autoSaveTimer;
    private readonly DispatcherTimer _filePollTimer;
    private readonly Dictionary<string, string> _fileWarnings = new();
    private DateTime _fpsWindowStart = DateTime.UtcNow;
    private int _framesInWindow;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private bool _previousTopmost;
    private GridLength _previousEditorColumnWidth;
    private GridLength _previousPreviewColumnWidth;
    private GridLength _previousDiagnosticsRowHeight;
    private GridLength _previousPreviewHeaderRowHeight;
    private GridLength _previousPreviewChannelsRowHeight;
    private Thickness _previousPreviewBorderMargin;
    private IReadOnlyList<ModelAsset> _availableModels = Array.Empty<ModelAsset>();
    private bool _isOrbitingCamera;
    private System.Drawing.Point _lastMousePosition;
    private bool _isApplyingSyntaxHighlighting;
    private readonly Dictionary<WpfRichTextBox, string> _editorTextStates = new();
    private readonly Dictionary<WpfRichTextBox, Stack<string>> _editorUndoHistory = new();
    private readonly Dictionary<WpfRichTextBox, Stack<string>> _editorRedoHistory = new();

    private (Regex Pattern, WpfBrush Brush, FontWeight? Weight)[] GetGlslHighlightRules()
    {
        return _currentTheme switch
        {
            AppThemeMode.Dark => new []
            {
                (new Regex(@"//.*$", RegexOptions.Compiled | RegexOptions.Multiline), CreateBrush("#91B879"), (FontWeight?)null),
                (new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline), CreateBrush("#91B879"), (FontWeight?)null),
                (new Regex("^\\s*#\\w+.*$", RegexOptions.Compiled | RegexOptions.Multiline), CreateBrush("#C586C0"), (FontWeight?)null),
                (new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled), CreateBrush("#CE9178"), (FontWeight?)null),
                (new Regex(@"\b\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?[fFuU]?\b", RegexOptions.Compiled), CreateBrush("#B5CEA8"), (FontWeight?)null),
                (new Regex(@"\b(?:if|else|for|while|do|switch|case|default|break|continue|discard|return|const|in|out|inout|uniform|layout|precision|struct)\b", RegexOptions.Compiled), CreateBrush("#569CD6"), FontWeights.SemiBold),
                (new Regex(@"\b(?:void|bool|int|uint|float|double|vec2|vec3|vec4|ivec2|ivec3|ivec4|uvec2|uvec3|uvec4|bvec2|bvec3|bvec4|mat2|mat3|mat4|mat2x2|mat2x3|mat2x4|mat3x2|mat3x3|mat3x4|mat4x2|mat4x3|mat4x4|sampler1D|sampler2D|sampler3D|samplerCube|sampler2DArray|sampler2DShadow)\b", RegexOptions.Compiled), CreateBrush("#4EC9B0"), (FontWeight?)null),
                (new Regex(@"\bgl_[A-Za-z0-9_]*\b", RegexOptions.Compiled), CreateBrush("#DCDCAA"), (FontWeight?)null),
                (new Regex(@"\b(?:iResolution|iTime|iTimeDelta|iFrame|iMouse|iDate|iChannelTime|iChannelResolution|iChannel0|iChannel1|iChannel2|iChannel3|mainImage|fragColor|fragCoord|VertexPos|vertex)\b", RegexOptions.Compiled), CreateBrush("#DCDCAA"), (FontWeight?)null),
            },
            AppThemeMode.Light => new []
            {
                (new Regex(@"//.*$", RegexOptions.Compiled | RegexOptions.Multiline), CreateBrush("#008000"), (FontWeight?)null),
                (new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline), CreateBrush("#008000"), (FontWeight?)null),
                (new Regex("^\\s*#\\w+.*$", RegexOptions.Compiled | RegexOptions.Multiline), CreateBrush("#0000FF"), (FontWeight?)null),
                (new Regex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled), CreateBrush("#A31515"), (FontWeight?)null),
                (new Regex(@"\b\d+(?:\.\d+)?(?:[eE][+\-]?\d+)?[fFuU]?\b", RegexOptions.Compiled), CreateBrush("#098658"), (FontWeight?)null),
                (new Regex(@"\b(?:if|else|for|while|do|switch|case|default|break|continue|discard|return|const|in|out|inout|uniform|layout|precision|struct)\b", RegexOptions.Compiled), CreateBrush("#0000FF"), FontWeights.SemiBold),
                (new Regex(@"\b(?:void|bool|int|uint|float|double|vec2|vec3|vec4|ivec2|ivec3|ivec4|uvec2|uvec3|uvec4|bvec2|bvec3|bvec4|mat2|mat3|mat4|mat2x2|mat2x3|mat2x4|mat3x2|mat3x3|mat3x4|mat4x2|mat4x3|mat4x4|sampler1D|sampler2D|sampler3D|samplerCube|sampler2DArray|sampler2DShadow)\b", RegexOptions.Compiled), CreateBrush("#17657A"), (FontWeight?)null),
                (new Regex(@"\bgl_[A-Za-z0-9_]*\b", RegexOptions.Compiled), CreateBrush("#795E26"), (FontWeight?)null),
                (new Regex(@"\b(?:iResolution|iTime|iTimeDelta|iFrame|iMouse|iDate|iChannelTime|iChannelResolution|iChannel0|iChannel1|iChannel2|iChannel3|mainImage|fragColor|fragCoord|VertexPos|vertex)\b", RegexOptions.Compiled), CreateBrush("#795E26"), (FontWeight?)null),
            },
            _ => Array.Empty<(Regex, WpfBrush, FontWeight?)>()
        };
    }

    public MainWindow()
    {
        InitializeComponent();

        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += RenderTimer_Tick;

        _compileDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _compileDebounceTimer.Tick += CompileDebounceTimer_Tick;
        _autoSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _autoSaveTimer.Tick += (_, _) => AutoSaveEditors();
        _filePollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _filePollTimer.Tick += (_, _) => PollShaderFiles();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EnsureFundamentalDirectories();
        _document = _sessionStore.LoadOrDefault();
        if (string.IsNullOrWhiteSpace(_document.VertexSource))
        {
            _document.VertexSource = ShaderTemplateCatalog.ModelVertex;
        }

        AutoCompileCheckBox.IsChecked = _document.AutoCompile;
        SetEditorText(EditorTextBox, _document.FragmentSource);
        SetEditorText(VertexEditorTextBox, _document.VertexSource);

        foreach (var template in ShaderTemplateCatalog.Templates)
        {
            TemplateComboBox.Items.Add(template.Name);
        }

        PopulateModelsCombo();
        RenderModeComboBox.SelectedIndex = _document.RenderMode == RenderMode.ThreeD ? 1 : 0;

        InitializeGlHost();
        if (!_videoFailed && _glControl is not null)
        {
            RunVideoAction(() =>
            {
                _glControl.MakeCurrent();
                AppendCompileResult(_renderer.SetRenderMode(_document.RenderMode, _document.VertexSource));
            });
        }

        Update3DControlsState();
        _renderer.SetPaused(_document.IsPaused);
        PlayPauseButton.Content = _document.IsPaused ? "Play" : "Pause";
        AutoRotateCheckBox.IsChecked = _document.AutoRotateModel;
        _renderer.SetModelAutoRotation(_document.AutoRotateModel);

        if (_document.IsFullscreen)
        {
            ToggleFullscreen();
        }

        if (!_videoFailed)
        {
            _renderTimer.Start();
            AppendDiagnostic("Studio ready.");
        }

        SetupLineNumberScrollSync();

        _autoSaveTimer.Start();
        _filePollTimer.Start();
    }

    private void InitializeGlHost() => RunVideoAction(() => InitializeGlHostCore());

    private void InitializeGlHostCore()
    {
        var glSettings = new GLControlSettings
        {
            API = OpenTK.Windowing.Common.ContextAPI.OpenGL,
            APIVersion = new Version(3, 3),
            Profile = OpenTK.Windowing.Common.ContextProfile.Core,
            IsEventDriven = false
        };

        _glControl = new GLControl(glSettings)
        {
            Dock = System.Windows.Forms.DockStyle.Fill,
            BackColor = System.Drawing.Color.Black
        };

        _glControl.Resize += (_, _) =>
        {
            if (_videoFailed || _glControl.ClientSize.Width <= 0 || _glControl.ClientSize.Height <= 0) return;
            RunVideoAction(() =>
            {
                _glControl.MakeCurrent();
                _renderer.Resize(_glControl.ClientSize.Width, _glControl.ClientSize.Height);
            });
        };

        _glControl.MouseMove += (_, args) =>
        {
            _renderer.SetMouse(args.X, args.Y, args.Button == System.Windows.Forms.MouseButtons.Left);

            if (_document.RenderMode == RenderMode.ThreeD && _isOrbitingCamera)
            {
                var dx = args.X - _lastMousePosition.X;
                var dy = args.Y - _lastMousePosition.Y;
                _renderer.RotateCamera(dx * 0.2f, -dy * 0.2f);
                _lastMousePosition = new System.Drawing.Point(args.X, args.Y);
            }
        };

        _glControl.MouseDown += (_, args) =>
        {
            _renderer.SetMouse(args.X, args.Y, true);
            if (_document.RenderMode == RenderMode.ThreeD && args.Button == System.Windows.Forms.MouseButtons.Right)
            {
                _isOrbitingCamera = true;
                _lastMousePosition = new System.Drawing.Point(args.X, args.Y);
            }
            _glControl.Focus();
        };

        _glControl.MouseUp += (_, args) =>
        {
            _renderer.SetMouse(args.X, args.Y, false);
            if (args.Button == System.Windows.Forms.MouseButtons.Right)
            {
                _isOrbitingCamera = false;
            }
        };

        _glControl.MouseWheel += (_, args) =>
        {
            if (_document.RenderMode == RenderMode.ThreeD)
            {
                _renderer.ZoomCamera(args.Delta / 120f * 2f);
            }
        };

        _glControl.KeyDown += (_, args) =>
        {
            if (args.KeyCode == System.Windows.Forms.Keys.Escape)
            {
                ExitFullscreenIfActive();
            }
        };

        PreviewHost.Child = _glControl;
        _glControl.MakeCurrent();

        _renderer.Initialize(
            Math.Max(1, _glControl.ClientSize.Width),
            Math.Max(1, _glControl.ClientSize.Height),
            _document.FragmentSource);

        TryLoadInitialModel();

        foreach (var channel in _document.Channels.Where(c => !string.IsNullOrWhiteSpace(c.TexturePath)))
        {
            _renderer.TrySetChannelTexture(channel.Index, channel.TexturePath, out _);
        }

        var message = _renderer.LastCompileMessage;
        AppendCompileResult(message);
    }

    private void RenderTimer_Tick(object? sender, EventArgs e) => RunVideoAction(() => RenderTimer_TickCore(sender, e));

    private void RenderTimer_TickCore(object? sender, EventArgs e)
    {
        if (_videoFailed || _glControl is null || !_glControl.IsHandleCreated)
        {
            return;
        }

        var elapsed = _frameStopwatch.Elapsed.TotalSeconds;
        _frameStopwatch.Restart();

        HandleCameraInput((float)elapsed);
        _glControl.MakeCurrent();
        _renderer.Render(elapsed);
        _glControl.SwapBuffers();

        _framesInWindow++;
        var now = DateTime.UtcNow;
        var dt = (now - _fpsWindowStart).TotalSeconds;
        if (dt >= 1.0)
        {
            FpsTextBlock.Text = $"FPS: {(int)(_framesInWindow / dt)}";
            _fpsWindowStart = now;
            _framesInWindow = 0;
        }
    }

    private void CompileCurrentShader() => RunVideoAction(() => CompileCurrentShaderCore());

    private void CompileCurrentShaderCore()
    {
        if (_videoFailed || _glControl is null)
        {
            return;
        }

        _document.FragmentSource = GetEditorText(EditorTextBox);
        _document.VertexSource = GetEditorText(VertexEditorTextBox);
        _glControl.MakeCurrent();

        var result = _renderer.Compile(_document.FragmentSource, _document.VertexSource);
        AppendCompileResult(result);

        _sessionStore.Save(_document);
    }

    private void AppendCompileResult(ShaderCompileMessage message)
    {
        var prefix = message.Success ? "[OK]" : "[ERR]";
        var stage = string.IsNullOrWhiteSpace(message.Stage) ? string.Empty : $"[{message.Stage}]";
        var line = message.Line.HasValue ? $" line {message.Line.Value}" : string.Empty;
        AppendDiagnostic($"{prefix}{stage}{line} {message.Message}");
        _lastError = message.Success ? null : message;
        GoToErrorButton.IsEnabled = !message.Success && message.Line.HasValue && message.Stage is "vertex" or "fragment";
        CompileSummaryTextBlock.Text = message.Success ? "Compilado com sucesso" :
            $"ERRO • {message.Stage ?? "shader"} • {(message.Line.HasValue ? $"linha {message.Line}" : "linha não informada pelo driver")} — consulte os detalhes abaixo";
        CompileSummaryTextBlock.SetResourceReference(TextBlock.ForegroundProperty, message.Success ? "PrimaryForegroundBrush" : "ErrorForegroundBrush");
        StatusTextBlock.Text = message.Success ? "Shader compilado" : "Erro no shader — prévia mantém a última compilação válida";
    }

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
        DiagnosticsTextBox.Clear();
    }

    private void AppendDiagnostic(string text)
    {
        DiagnosticsTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        DiagnosticsTextBox.ScrollToEnd();
    }

    private void EditorTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (!_isApplyingSyntaxHighlighting && sender is WpfRichTextBox editor)
        {
            RecordEditorChange(editor);
            ApplySyntaxHighlighting(editor);

            if (editor == EditorTextBox)
            {
                UpdateLineNumbers(EditorTextBox, EditorLineNumbers);
            }
            else if (editor == VertexEditorTextBox)
            {
                UpdateLineNumbers(VertexEditorTextBox, VertexEditorLineNumbers);
            }
        }

        if (AutoCompileCheckBox.IsChecked != true)
        {
            return;
        }

        _compileDebounceTimer.Stop();
        _compileDebounceTimer.Start();
    }

    private void EditorTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not WpfRichTextBox editor)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.Z)
        {
            UndoEditorChange(editor);
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.Y)
        {
            RedoEditorChange(editor);
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        // Indent code instead of moving focus to the next control.
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            OutdentSelection(editor);
        }
        else
        {
            IndentSelection(editor);
        }

        e.Handled = true;
    }

    private void IndentSelection(WpfRichTextBox editor)
    {
        var selection = editor.Selection;
        var text = GetEditorText(editor);
        GetSelectionOffsets(editor, out var selStart, out var selEnd);

        if (selection.IsEmpty)
        {
            // Caret without selection: insert indentation in place.
            selection.Text = Indentation;
            editor.CaretPosition = selection.End;
            return;
        }

        var lineStart = FindLineStart(text, selStart);
        var range = new TextRange(
            GetTextPointerAtOffset(editor.Document, lineStart),
            GetTextPointerAtOffset(editor.Document, selEnd));
        var block = range.Text;
        var indented = IndentLines(block);
        range.Text = indented;

        // Reselect the same lines so repeated Tab keeps indenting them.
        selection.Select(
            GetTextPointerAtOffset(editor.Document, lineStart),
            GetTextPointerAtOffset(editor.Document, lineStart + indented.Length));
    }

    private void OutdentSelection(WpfRichTextBox editor)
    {
        var selection = editor.Selection;
        var text = GetEditorText(editor);
        GetSelectionOffsets(editor, out var selStart, out var selEnd);

        var lineStart = FindLineStart(text, selStart);
        var rangeEnd = selEnd;
        if (selection.IsEmpty)
        {
            // Caret without selection: outdent the whole current line.
            var nextNewline = text.IndexOf('\n', lineStart);
            rangeEnd = nextNewline < 0 ? text.Length : nextNewline;
        }

        var range = new TextRange(
            GetTextPointerAtOffset(editor.Document, lineStart),
            GetTextPointerAtOffset(editor.Document, rangeEnd));
        var block = range.Text;
        var outdented = OutdentLines(block, out var removedFirstLine, out var removedTotal);
        if (removedTotal == 0)
        {
            return;
        }

        range.Text = outdented;

        if (selection.IsEmpty)
        {
            var column = selStart - lineStart;
            var newColumn = Math.Max(0, column - Math.Min(column, removedFirstLine));
            editor.CaretPosition = GetTextPointerAtOffset(editor.Document, lineStart + newColumn);
            return;
        }

        var newStart = selStart == lineStart
            ? lineStart
            : selStart - Math.Min(selStart - lineStart, removedFirstLine);
        var newEnd = Math.Max(newStart, selEnd - removedTotal);
        selection.Select(
            GetTextPointerAtOffset(editor.Document, newStart),
            GetTextPointerAtOffset(editor.Document, newEnd));
    }

    private static string IndentLines(string block)
    {
        var lines = block.Split("\r\n");
        var result = new System.Text.StringBuilder(block.Length + lines.Length * Indentation.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                result.Append("\r\n");
            }

            // Skip empty lines, mirroring common code editors.
            if (lines[i].Length > 0)
            {
                result.Append(Indentation);
            }

            result.Append(lines[i]);
        }

        return result.ToString();
    }

    private static string OutdentLines(string block, out int removedFirstLine, out int removedTotal)
    {
        var lines = block.Split("\r\n");
        var result = new System.Text.StringBuilder(block.Length);
        removedFirstLine = 0;
        removedTotal = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                result.Append("\r\n");
            }

            var line = lines[i];
            var removed = 0;
            if (line.StartsWith(Indentation, StringComparison.Ordinal))
            {
                removed = Indentation.Length;
            }
            else if (line.StartsWith('\t'))
            {
                removed = 1;
            }

            if (i == 0)
            {
                removedFirstLine = removed;
            }

            removedTotal += removed;
            result.Append(line, removed, line.Length - removed);
        }

        return result.ToString();
    }

    private static void GetSelectionOffsets(WpfRichTextBox editor, out int start, out int end)
    {
        var document = editor.Document;
        start = new TextRange(document.ContentStart, editor.Selection.Start).Text.Length;
        end = new TextRange(document.ContentStart, editor.Selection.End).Text.Length;
        if (start > end)
        {
            (start, end) = (end, start);
        }
    }

    private static int FindLineStart(string text, int offset)
    {
        var clamped = Math.Clamp(offset, 0, text.Length);
        if (clamped == 0)
        {
            return 0;
        }

        var newline = text.LastIndexOf('\n', clamped - 1, clamped);
        return newline < 0 ? 0 : newline + 1;
    }

    private void EditorTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        if (sender is not WpfRichTextBox editor)
        {
            return;
        }

        var direction = e.Delta > 0 ? 1d : -1d;
        var nextSize = Math.Clamp(editor.FontSize + direction * EditorFontStep, MinEditorFontSize, MaxEditorFontSize);

        EditorTextBox.FontSize = nextSize;
        VertexEditorTextBox.FontSize = nextSize;
        EditorLineNumbers.FontSize = nextSize;
        EditorLineNumbers.LineHeight = nextSize * 1.1;
        VertexEditorLineNumbers.FontSize = nextSize;
        VertexEditorLineNumbers.LineHeight = nextSize * 1.1;
        UpdateLineNumbers(EditorTextBox, EditorLineNumbers);
        UpdateLineNumbers(VertexEditorTextBox, VertexEditorLineNumbers);
        e.Handled = true;
    }

    private void CompileDebounceTimer_Tick(object? sender, EventArgs e)
    {
        _compileDebounceTimer.Stop();
        CompileCurrentShader();
    }

    private void CompileButton_Click(object sender, RoutedEventArgs e)
    {
        CompileCurrentShader();
    }

    private void AutoCompileCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        _document.AutoCompile = AutoCompileCheckBox.IsChecked == true;
        _sessionStore.Save(_document);
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        _document.IsPaused = !_document.IsPaused;
        _renderer.SetPaused(_document.IsPaused);
        PlayPauseButton.Content = _document.IsPaused ? "Play" : "Pause";
        _sessionStore.Save(_document);
    }

    private void ResetTimeButton_Click(object sender, RoutedEventArgs e)
    {
        _renderer.ResetTime();
        AppendDiagnostic("Time reset.");
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
        _document.IsFullscreen = _isFullscreen;
        _sessionStore.Save(_document);
    }

    private void ExitFullscreenIfActive()
    {
        if (!_isFullscreen)
        {
            return;
        }

        ToggleFullscreen();
        _document.IsFullscreen = _isFullscreen;
        _sessionStore.Save(_document);
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousTopmost = Topmost;
            _previousEditorColumnWidth = EditorColumnDefinition.Width;
            _previousPreviewColumnWidth = PreviewColumnDefinition.Width;
            _previousDiagnosticsRowHeight = DiagnosticsRowDefinition.Height;
            _previousPreviewHeaderRowHeight = PreviewHeaderRowDefinition.Height;
            _previousPreviewChannelsRowHeight = PreviewChannelsRowDefinition.Height;
            _previousPreviewBorderMargin = PreviewBorder.Margin;

            MainToolBar.Visibility = Visibility.Collapsed;
            MainStatusBar.Visibility = Visibility.Collapsed;
            EditorPaneGrid.Visibility = Visibility.Collapsed;
            DiagnosticsGroupBox.Visibility = Visibility.Collapsed;
            PreviewTitleTextBlock.Visibility = Visibility.Collapsed;
            ChannelButtonsGrid.Visibility = Visibility.Collapsed;

            EditorColumnDefinition.Width = new GridLength(0);
            PreviewColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
            DiagnosticsRowDefinition.Height = new GridLength(0);
            PreviewHeaderRowDefinition.Height = new GridLength(0);
            PreviewChannelsRowDefinition.Height = new GridLength(0);
            PreviewBorder.Margin = new Thickness(0);

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
            _isFullscreen = true;
            FullscreenButton.Content = "Windowed";
            return;
        }

        Topmost = _previousTopmost;
        WindowStyle = _previousWindowStyle;
        ResizeMode = _previousResizeMode;
        WindowState = _previousWindowState;

        MainToolBar.Visibility = Visibility.Visible;
        MainStatusBar.Visibility = Visibility.Visible;
        EditorPaneGrid.Visibility = Visibility.Visible;
        DiagnosticsGroupBox.Visibility = Visibility.Visible;
        PreviewTitleTextBlock.Visibility = Visibility.Visible;
        ChannelButtonsGrid.Visibility = Visibility.Visible;

        EditorColumnDefinition.Width = _previousEditorColumnWidth;
        PreviewColumnDefinition.Width = _previousPreviewColumnWidth;
        DiagnosticsRowDefinition.Height = _previousDiagnosticsRowHeight;
        PreviewHeaderRowDefinition.Height = _previousPreviewHeaderRowHeight;
        PreviewChannelsRowDefinition.Height = _previousPreviewChannelsRowHeight;
        PreviewBorder.Margin = _previousPreviewBorderMargin;

        _isFullscreen = false;
        FullscreenButton.Content = "Fullscreen";
    }

    private void RenderModeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var mode = RenderModeComboBox.SelectedIndex == 1 ? RenderMode.ThreeD : RenderMode.TwoD;
        _document.RenderMode = mode;
        _document.VertexSource = GetEditorText(VertexEditorTextBox);
        if (_videoFailed || _glControl is null)
        {
            Update3DControlsState();
            _sessionStore.Save(_document);
            return;
        }

        RunVideoAction(() =>
        {
            _glControl.MakeCurrent();
            AppendCompileResult(_renderer.SetRenderMode(mode, _document.VertexSource));
        });
        Update3DControlsState();

        if (mode == RenderMode.ThreeD)
        {
            if (ModelComboBox.SelectedIndex >= 0)
            {
                TryLoadModelAt(ModelComboBox.SelectedIndex, persistSelection: false);
            }
            AppendDiagnostic("3D mode active. Use WASD + right mouse drag + wheel.");
        }

        _sessionStore.Save(_document);
    }

    private void TemplateComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || TemplateComboBox.SelectedItem is not string selectedName)
        {
            return;
        }

        var template = ShaderTemplateCatalog.Templates.FirstOrDefault(t => t.Name == selectedName);
        if (template is null)
        {
            return;
        }

        SetEditorText(EditorTextBox, template.FragmentSource);
        CompileCurrentShader();
        AppendDiagnostic($"Loaded template: {selectedName}");
    }

    private void LoadChannel0_Click(object sender, RoutedEventArgs e) => LoadChannel(0);
    private void LoadChannel1_Click(object sender, RoutedEventArgs e) => LoadChannel(1);
    private void LoadChannel2_Click(object sender, RoutedEventArgs e) => LoadChannel(2);
    private void LoadChannel3_Click(object sender, RoutedEventArgs e) => LoadChannel(3);
    private void ClearChannel0_Click(object sender, RoutedEventArgs e) => ClearChannel(0);
    private void ClearChannel1_Click(object sender, RoutedEventArgs e) => ClearChannel(1);
    private void ClearChannel2_Click(object sender, RoutedEventArgs e) => ClearChannel(2);
    private void ClearChannel3_Click(object sender, RoutedEventArgs e) => ClearChannel(3);

    private void WebcamChannel0_Click(object sender, RoutedEventArgs e) => SelectWebcamChannel(0);
    private void WebcamChannel1_Click(object sender, RoutedEventArgs e) => SelectWebcamChannel(1);
    private void WebcamChannel2_Click(object sender, RoutedEventArgs e) => SelectWebcamChannel(2);
    private void WebcamChannel3_Click(object sender, RoutedEventArgs e) => SelectWebcamChannel(3);

    private void SelectWebcamChannel(int channel)
    {
        var cameras = RunVideoActionAndCapture(() => _renderer.ListCameras());
        if (cameras is null)
        {
            AppendDiagnostic("[ERRO DE VÍDEO] Webcam indisponível.");
            return;
        }

        if (cameras.Count == 0)
        {
            AppendDiagnostic($"Nenhuma webcam encontrada. Verifique se a câmera está conectada e o ffmpeg está instalado.");
            return;
        }

        // Show a simple selection dialog.
        var deviceName = PromptSelectCamera(cameras);
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return;
        }

        var message = RunVideoActionAndCapture(() =>
        {
            _glControl?.MakeCurrent();
            return _renderer.TrySetChannelWebcam(channel, deviceName, out var msg) ? msg : $"ERRO: {msg}";
        });

        if (message is not null)
        {
            AppendDiagnostic(message);
            if (message.StartsWith("ERRO"))
            {
                StatusTextBlock.Text = "Falha ao conectar webcam";
            }
            else
            {
                StatusTextBlock.Text = $"Webcam ativa em iChannel{channel}";
                _document.Channels.First(c => c.Index == channel).TexturePath = deviceName;
                _sessionStore.Save(_document);
            }
        }
    }

    private static string? PromptSelectCamera(IReadOnlyList<string> cameras)
    {
        if (cameras.Count == 1)
        {
            return cameras[0];
        }

        // Build a simple WPF dialog with a ListBox.
        var window = new Window
        {
            Title = "Selecionar Webcam",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize
        };

        var panel = new DockPanel { Margin = new Thickness(10) };
        var cancelBtn = new System.Windows.Controls.Button { Content = "Cancelar", Width = 80, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(cancelBtn, Dock.Top);

        var listBox = new System.Windows.Controls.ListBox { Margin = new Thickness(0, 8, 0, 8) };
        foreach (var cam in cameras)
        {
            listBox.Items.Add(new ListBoxItem { Content = cam });
        }
        listBox.SelectedIndex = 0;

        var confirmBtn = new System.Windows.Controls.Button { Content = "OK", Width = 80, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };

        cancelBtn.Click += (_, _) => { window.DialogResult = false; };
        confirmBtn.Click += (_, _) => { window.DialogResult = true; };

        panel.Children.Add(cancelBtn);
        panel.Children.Add(listBox);
        panel.Children.Add(confirmBtn);
        window.Content = panel;

        return window.ShowDialog() == true ? (listBox.SelectedItem as ListBoxItem)?.Content.ToString() : null;
    }

    private void ResetCameraButton_Click(object sender, RoutedEventArgs e)
    {
        _renderer.ResetCamera();
        AppendDiagnostic("Camera reset.");
    }

    private void AutoRotateCheckBox_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _document.AutoRotateModel = AutoRotateCheckBox.IsChecked == true;
        _renderer.SetModelAutoRotation(_document.AutoRotateModel);
        _sessionStore.Save(_document);
    }

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _currentTheme = _currentTheme == AppThemeMode.Dark ? AppThemeMode.Light : AppThemeMode.Dark;
        ApplyTheme();
        AppendDiagnostic($"Theme changed to: {_currentTheme}");
        
        // Re-highlight code with new theme colors
        if (EditorTextBox.Document.ContentEnd != EditorTextBox.Document.ContentStart)
        {
            ApplySyntaxHighlighting(EditorTextBox);
        }
        if (VertexEditorTextBox.Document.ContentEnd != VertexEditorTextBox.Document.ContentStart)
        {
            ApplySyntaxHighlighting(VertexEditorTextBox);
        }
    }

    private void ApplyTheme()
    {
        // Update all merged resources with new theme colors
        var updateColors = new Dictionary<string, SolidColorBrush>
        {
            { "ErrorForegroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#FF9999" : "#A31515") },
            { "PrimaryBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#1E1E1E" : "#FFFFFF") },
            { "SecondaryBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#252526" : "#F5F5F5") },
            { "TertiaryBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#2D2D30" : "#EFEFEF") },
            { "PrimaryForegroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#E0E0E0" : "#1E1E1E") },
            { "SecondaryForegroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#A0A0A0" : "#5A5A5A") },
            { "AccentBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#59B9FF" : "#005AA3") },
            { "EditorBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#1E1E1E" : "#FFFFFF") },
            { "EditorForegroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#D4D4D4" : "#000000") },
            { "ScrollBarBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#252526" : "#F5F5F5") },
            { "ToolBarBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#2D2D30" : "#F5F5F5") },
            { "StatusBarBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#2D2D30" : "#F5F5F5") },
            { "TabBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#252526" : "#EFEFEF") },
            { "TabForegroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#CCCCCC" : "#333333") },
            { "TabSelectedBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#1E1E1E" : "#FFFFFF") },
            { "BorderBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#3E3E42" : "#CCCCCC") },
            { "ButtonHoverBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#3E3E42" : "#E0E0E0") },
            { "ButtonPressedBackgroundBrush", CreateColorBrush(_currentTheme == AppThemeMode.Dark ? "#59B9FF" : "#005AA3") },
        };

        foreach (var kvp in updateColors)
        {
            System.Windows.Application.Current.Resources[kvp.Key] = kvp.Value;
        }

        // Force window background and foreground update
        Background = (SolidColorBrush)FindResource("PrimaryBackgroundBrush");
        Foreground = (SolidColorBrush)FindResource("PrimaryForegroundBrush");
    }

    private static SolidColorBrush CreateColorBrush(string hexColor)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void LoadChannel(int channel) => RunVideoAction(() => LoadChannelCore(channel));

    private void LoadChannelCore(int channel)
    {
        if (_videoFailed || _glControl is null)
        {
            return;
        }

        private void ClearChannel(int channel) => RunVideoAction(() => ClearChannelCore(channel));

        private void ClearChannelCore(int channel)
        {
            if (_videoFailed || _glControl is null)
            {
                return;
            }

            _glControl.MakeCurrent();
            var ok = _renderer.TrySetChannelTexture(channel, null, out var message);
            AppendDiagnostic(message);
            if (!ok)
            {
                return;
            }

            _document.Channels.First(c => c.Index == channel).TexturePath = null;
            _sessionStore.Save(_document);
            StatusTextBlock.Text = $"iChannel{channel} using buffer";
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Images & Videos|*.png;*.jpg;*.jpeg;*.bmp;*.tga;*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv;*.m4v;*.mpg;*.mpeg|Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.tga|Video Files|*.mp4;*.mov;*.avi;*.mkv;*.webm;*.wmv;*.m4v;*.mpg;*.mpeg",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _glControl.MakeCurrent();
        var ok = _renderer.TrySetChannelTexture(channel, dialog.FileName, out var message);
        AppendDiagnostic(message);

        if (ok)
        {
            var entry = _document.Channels.First(c => c.Index == channel);
            entry.TexturePath = dialog.FileName;
            _sessionStore.Save(_document);
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            ExitFullscreenIfActive();
            e.Handled = true;
            return;
        }

        if (e.Key == System.Windows.Input.Key.S &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            SaveButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private bool IsVertexTabSelected()
    {
        return ShaderEditorsTabControl.SelectedIndex == 1;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        bool isVertexTab = IsVertexTabSelected();
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = isVertexTab ? "Vertex Shaders|*.vert|All Files|*.*" : "Fragment Shaders|*.frag|All Files|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        if (isVertexTab)
        {
            SetEditorText(VertexEditorTextBox, File.ReadAllText(dialog.FileName));
            _document.VertexSource = GetEditorText(VertexEditorTextBox);
            _vertexFile = new ShaderFileSync(dialog.FileName, _document.VertexSource);
            AppendDiagnostic($"Opened vertex shader: {dialog.FileName}");
        }
        else
        {
            SetEditorText(EditorTextBox, File.ReadAllText(dialog.FileName));
            _fragmentFile = new ShaderFileSync(dialog.FileName, GetEditorText(EditorTextBox));
            _vertexFile = null;
            var vertexSidecarPath = GetVertexSidecarPath(dialog.FileName);
            if (File.Exists(vertexSidecarPath))
            {
                SetEditorText(VertexEditorTextBox, File.ReadAllText(vertexSidecarPath));
                _vertexFile = new ShaderFileSync(vertexSidecarPath, GetEditorText(VertexEditorTextBox));
                AppendDiagnostic($"Opened vertex: {vertexSidecarPath}");
            }
            else if (_document.RenderMode == RenderMode.ThreeD)
            {
                AppendDiagnostic($"Vertex file not found: {vertexSidecarPath}");
            }

            _currentFilePath = dialog.FileName;
            _document.FragmentSource = GetEditorText(EditorTextBox);
            _document.VertexSource = GetEditorText(VertexEditorTextBox);
            AppendDiagnostic($"Opened fragment shader: {dialog.FileName}");
        }

        _sessionStore.Save(_document);
        UpdateTitle();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var activePath = IsVertexTabSelected() ? _vertexFile?.Path : _currentFilePath;
        if (!string.IsNullOrWhiteSpace(activePath))
        {
            SaveToFile(activePath, confirmOverwrite: true, isVertexOnly: IsVertexTabSelected());
        }
        else
        {
            SaveAsButton_Click(sender, e);
        }
    }

    private void SaveAsButton_Click(object sender, RoutedEventArgs e)
    {
        bool isVertexTab = IsVertexTabSelected();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = isVertexTab ? "Vertex Shaders|*.vert|All Files|*.*" : "Fragment Shaders|*.frag|All Files|*.*",
            FileName = string.IsNullOrWhiteSpace(_currentFilePath)
                ? (isVertexTab ? "shader.vert" : "shader.frag")
                : (isVertexTab ? Path.ChangeExtension(Path.GetFileName(_currentFilePath), ".vert") : Path.GetFileName(_currentFilePath))
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SaveToFile(dialog.FileName, isVertexOnly: isVertexTab);
    }

    private void SaveToFile(string path, bool confirmOverwrite = false, bool isVertexOnly = false)
    {
        if (confirmOverwrite && File.Exists(path))
        {
            var answer = System.Windows.MessageBox.Show(
                this,
                $"O arquivo \"{Path.GetFileName(path)}\" já existe. Deseja sobrescrever?",
                "Confirmar sobrescrita",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                AppendDiagnostic($"Save canceled: {path}");
                StatusTextBlock.Text = "Save canceled";
                return;
            }
        }

        _document.FragmentSource = GetEditorText(EditorTextBox);
        _document.VertexSource = GetEditorText(VertexEditorTextBox);

        if (isVertexOnly)
        {
            File.WriteAllText(path, _document.VertexSource);
            _vertexFile = new ShaderFileSync(path, _document.VertexSource);
            AppendDiagnostic($"Saved vertex shader: {path}");
        }
        else
        {
            File.WriteAllText(path, _document.FragmentSource);
            _currentFilePath = path;
            _fragmentFile = new ShaderFileSync(path, _document.FragmentSource);

            var vertexSidecarPath = GetVertexSidecarPath(path);
            var shouldSaveVertex = _document.RenderMode == RenderMode.ThreeD
                || !string.IsNullOrWhiteSpace(_document.VertexSource)
                || File.Exists(vertexSidecarPath);

            if (shouldSaveVertex)
            {
                File.WriteAllText(vertexSidecarPath, _document.VertexSource);
                _vertexFile = new ShaderFileSync(vertexSidecarPath, _document.VertexSource);
                AppendDiagnostic($"Saved vertex: {vertexSidecarPath}");
            }

            AppendDiagnostic($"Saved fragment shader: {path}");
        }

        _sessionStore.Save(_document);
        UpdateTitle();
        StatusTextBlock.Text = "Saved";
    }

    private void ReportFileWarning(string path, string message)
    {
        if (_fileWarnings.TryGetValue(path, out var previous) && previous == message) return;
        _fileWarnings[path] = message;
        AppendDiagnostic($"[ARQUIVO] {path}: {message}");
        FileSyncStatusTextBlock.Text = message;
    }

    private void PollShaderFiles()
    {
        bool changed = false;
        foreach (var path in _fileWarnings.Keys.ToArray())
            if (path != _fragmentFile?.Path && path != _vertexFile?.Path) _fileWarnings.Remove(path);
        foreach (var (file, editor) in new[] { (_fragmentFile, EditorTextBox), (_vertexFile, VertexEditorTextBox) })
        {
            if (file is null) continue;
            try
            {
                var source = file.Poll(GetEditorText(editor));
                if (file.HasConflict)
                {
                    ReportFileWarning(file.Path, "Conflito: arquivo e editor mudaram. Use Save As para preservar sua edição ou Open para carregar o disco.");
                    continue;
                }
                _fileWarnings.Remove(file.Path);
                if (source is null) continue;
                SetEditorText(editor, source);
                changed = true;
                AppendDiagnostic($"[EXTERNO] Atualizado: {file.Path}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ReportFileWarning(file.Path, $"Arquivo indisponível; nova tentativa automática: {ex.Message}");
            }
        }
        if (changed)
        {
            _document.FragmentSource = GetEditorText(EditorTextBox);
            _document.VertexSource = GetEditorText(VertexEditorTextBox);
            _compileDebounceTimer.Stop();
            CompileCurrentShader(); // External saves compile even when the editor's Auto checkbox is off.
        }
        if (_fileWarnings.Count == 0)
            FileSyncStatusTextBlock.Text = "Autosave: 10 min • edição externa: ativa";
    }

    private void AutoSaveEditors()
    {
        _document.FragmentSource = GetEditorText(EditorTextBox);
        _document.VertexSource = GetEditorText(VertexEditorTextBox);
        try
        {
            // Also protects unnamed shaders and remains available if graphics fails.
            _sessionStore.Save(_document);
            AppendDiagnostic("[AUTOSAVE] Sessão salva (intervalo: 10 minutos).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppendDiagnostic($"[AUTOSAVE] Falha ao salvar sessão: {ex.Message}");
        }
        foreach (var (file, source) in new[] { (_fragmentFile, _document.FragmentSource), (_vertexFile, _document.VertexSource) })
        {
            if (file is null) continue;
            try
            {
                if (!file.AutoSave(source))
                    ReportFileWarning(file.Path, "Autosave adiado: há mudanças externas pendentes. Nenhum código foi sobrescrito.");
                else
                    AppendDiagnostic($"[AUTOSAVE] Arquivo salvo: {file.Path}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ReportFileWarning(file.Path, $"Autosave indisponível: {ex.Message}");
            }
        }
    }

    private static string GetVertexSidecarPath(string fragmentPath)
    {
        return Path.ChangeExtension(fragmentPath, ".vert");
    }

    private void UpdateTitle()
    {
        Title = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "GLSL Shader Lab Studio"
            : $"GLSL Shader Lab Studio — {Path.GetFileName(_currentFilePath)}";
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _autoSaveTimer.Stop();
        _filePollTimer.Stop();
        _renderTimer.Stop();
        _compileDebounceTimer.Stop();

        _document.FragmentSource = GetEditorText(EditorTextBox);
        _document.VertexSource = GetEditorText(VertexEditorTextBox);
        _document.AutoCompile = AutoCompileCheckBox.IsChecked == true;
        _document.IsFullscreen = _isFullscreen;
        _document.AutoRotateModel = AutoRotateCheckBox.IsChecked == true;
        _document.SelectedModelPath = _renderer.CurrentModelPath;
        _sessionStore.Save(_document);

        if (!_videoFailed && _glControl is not null)
        {
            RunVideoAction(() =>
            {
                _glControl.MakeCurrent();
                _renderer.Dispose();
                _glControl.Dispose();
            });
        }
    }

    private void PopulateModelsCombo()
    {
        var modelsRoot = ResolveModelsRoot();
        _availableModels = _renderer.DiscoverModels(modelsRoot);
        ModelComboBox.Items.Clear();
        foreach (var model in _availableModels)
        {
            ModelComboBox.Items.Add(model.Name);
        }

        if (_availableModels.Count == 0)
        {
            AppendDiagnostic($"No models found in: {modelsRoot}");
            ModelComboBox.SelectedIndex = -1;
            return;
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(_document.SelectedModelPath))
        {
            for (var i = 0; i < _availableModels.Count; i++)
            {
                if (string.Equals(_availableModels[i].Path, _document.SelectedModelPath, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        ModelComboBox.SelectedIndex = selectedIndex;
    }

    private string ResolveModelsRoot()
    {
        var directory = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(directory, "Mesh");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(directory);
            if (parent is null)
            {
                break;
            }

            directory = parent.FullName;
        }

        var fallback = Path.Combine(Directory.GetCurrentDirectory(), "Mesh");
        return fallback;
    }

    private void EnsureFundamentalDirectories()
    {
        var roots = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            foreach (var directoryName in FundamentalDirectories)
            {
                var directoryPath = Path.Combine(root, directoryName);
                if (Directory.Exists(directoryPath))
                {
                    continue;
                }

                Directory.CreateDirectory(directoryPath);
                AppendDiagnostic($"Created folder: {directoryPath}");
            }
        }
    }

    private void TryLoadInitialModel()
    {
        if (_availableModels.Count == 0 || ModelComboBox.SelectedIndex < 0)
        {
            return;
        }

        TryLoadModelAt(ModelComboBox.SelectedIndex, persistSelection: false);
    }

    private void ModelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || ModelComboBox.SelectedIndex < 0)
        {
            return;
        }

        TryLoadModelAt(ModelComboBox.SelectedIndex, persistSelection: true);
    }

    private void TryLoadModelAt(int index, bool persistSelection) => RunVideoAction(() => TryLoadModelAtCore(index, persistSelection));

    private void TryLoadModelAtCore(int index, bool persistSelection)
    {
        if (_videoFailed || _glControl is null || index < 0 || index >= _availableModels.Count)
        {
            return;
        }

        _glControl.MakeCurrent();
        var selectedModel = _availableModels[index];
        if (_renderer.TryLoadModel(selectedModel.Path, out var message))
        {
            _document.SelectedModelPath = selectedModel.Path;
            if (persistSelection)
            {
                _sessionStore.Save(_document);
            }
        }

        AppendDiagnostic(message);
    }

    private void Update3DControlsState()
    {
        var is3d = _document.RenderMode == RenderMode.ThreeD;
        ModelComboBox.IsEnabled = is3d && _availableModels.Count > 0;
        ResetCameraButton.IsEnabled = is3d;
        AutoRotateCheckBox.IsEnabled = is3d;
        VertexEditorTab.IsEnabled = is3d;
    }

    private void HandleCameraInput(float deltaSeconds)
    {
        if (_document.RenderMode != RenderMode.ThreeD)
        {
            return;
        }

        float forward = 0f;
        float right = 0f;

        if (Keyboard.IsKeyDown(Key.W)) forward += 1f;
        if (Keyboard.IsKeyDown(Key.S)) forward -= 1f;
        if (Keyboard.IsKeyDown(Key.A)) right -= 1f;
        if (Keyboard.IsKeyDown(Key.D)) right += 1f;

        if (forward != 0f || right != 0f)
        {
            _renderer.MoveCamera(forward, right, deltaSeconds);
        }
    }

    private void SetEditorText(WpfRichTextBox editor, string text, bool resetUndoHistory = true)
    {
        _isApplyingSyntaxHighlighting = true;
        try
        {
            var paragraph = new Paragraph(new Run(text))
            {
                Margin = new Thickness(0),
                Padding = new Thickness(0)
            };

            editor.Document = new FlowDocument(paragraph)
            {
                PagePadding = new Thickness(0)
            };
        }
        finally
        {
            _isApplyingSyntaxHighlighting = false;
        }

        ApplySyntaxHighlighting(editor);

        if (editor == EditorTextBox)
        {
            UpdateLineNumbers(EditorTextBox, EditorLineNumbers);
        }
        else if (editor == VertexEditorTextBox)
        {
            UpdateLineNumbers(VertexEditorTextBox, VertexEditorLineNumbers);
        }

        if (resetUndoHistory)
        {
            ResetEditorHistory(editor, text);
        }
    }

    private void RecordEditorChange(WpfRichTextBox editor)
    {
        var currentText = GetEditorText(editor);
        if (_editorTextStates.TryGetValue(editor, out var previousText) && previousText != currentText)
        {
            GetHistory(_editorUndoHistory, editor).Push(previousText);
            GetHistory(_editorRedoHistory, editor).Clear();
        }

        _editorTextStates[editor] = currentText;
    }

    private void UndoEditorChange(WpfRichTextBox editor)
    {
        var undoHistory = GetHistory(_editorUndoHistory, editor);
        if (undoHistory.Count == 0)
        {
            return;
        }

        GetHistory(_editorRedoHistory, editor).Push(GetEditorText(editor));
        SetEditorText(editor, undoHistory.Pop(), resetUndoHistory: false);
        _editorTextStates[editor] = GetEditorText(editor);
    }

    private void RedoEditorChange(WpfRichTextBox editor)
    {
        var redoHistory = GetHistory(_editorRedoHistory, editor);
        if (redoHistory.Count == 0)
        {
            return;
        }

        GetHistory(_editorUndoHistory, editor).Push(GetEditorText(editor));
        SetEditorText(editor, redoHistory.Pop(), resetUndoHistory: false);
        _editorTextStates[editor] = GetEditorText(editor);
    }

    private void ResetEditorHistory(WpfRichTextBox editor, string text)
    {
        _editorTextStates[editor] = text;
        GetHistory(_editorUndoHistory, editor).Clear();
        GetHistory(_editorRedoHistory, editor).Clear();
    }

    private static Stack<string> GetHistory(Dictionary<WpfRichTextBox, Stack<string>> histories, WpfRichTextBox editor)
    {
        if (!histories.TryGetValue(editor, out var history))
        {
            history = new Stack<string>();
            histories[editor] = history;
        }

        return history;
    }

    private static string GetEditorText(WpfRichTextBox editor)
    {
        var text = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;
        return text.EndsWith("\r\n", StringComparison.Ordinal) ? text[..^2] : text;
    }

    private void ApplySyntaxHighlighting(WpfRichTextBox editor)
    {
        if (_isApplyingSyntaxHighlighting)
        {
            return;
        }

        _isApplyingSyntaxHighlighting = true;
        try
        {
            var document = editor.Document;

            // Batch all highlighting into ONE undo operation so Ctrl+Z
            // undoes the user's text edit, not a formatting change.
            editor.BeginChange();

            var defaultForeground = _currentTheme == AppThemeMode.Dark 
                ? CreateBrush("#D4D4D4") 
                : CreateBrush("#000000");

            var fullRange = new TextRange(document.ContentStart, document.ContentEnd);
            fullRange.ApplyPropertyValue(TextElement.ForegroundProperty, defaultForeground);
            fullRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);

            var text = GetEditorText(editor);
            var highlightRules = GetGlslHighlightRules();
            foreach (var rule in highlightRules)
            {
                foreach (Match match in rule.Pattern.Matches(text))
                {
                    if (match.Length == 0)
                    {
                        continue;
                    }

                    var start = GetTextPointerAtOffset(document, match.Index);
                    var end = GetTextPointerAtOffset(document, match.Index + match.Length);
                    var range = new TextRange(start, end);
                    range.ApplyPropertyValue(TextElement.ForegroundProperty, rule.Brush);
                    if (rule.Weight.HasValue)
                    {
                        range.ApplyPropertyValue(TextElement.FontWeightProperty, rule.Weight.Value);
                    }
                }
            }

            editor.EndChange();
        }
        finally
        {
            _isApplyingSyntaxHighlighting = false;
        }
    }


    private static TextPointer GetTextPointerAtOffset(FlowDocument document, int offset)
    {
        var navigator = document.ContentStart;
        var remaining = offset;
        while (navigator is not null)
        {
            var context = navigator.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var run = navigator.GetTextInRun(LogicalDirection.Forward);
                if (remaining <= run.Length) return navigator.GetPositionAtOffset(remaining)!;
                remaining -= run.Length;
            }
            else if ((context == TextPointerContext.ElementEnd && navigator.Parent is Paragraph) ||
                     (context == TextPointerContext.ElementStart && navigator.GetAdjacentElement(LogicalDirection.Forward) is LineBreak))
            {
                if (remaining <= 0) return navigator;
                remaining = Math.Max(0, remaining - 2); // TextRange represents breaks as CRLF.
            }
            navigator = navigator.GetNextContextPosition(LogicalDirection.Forward);
        }
        return document.ContentEnd;
    }

    private void UpdateLineNumbers(WpfRichTextBox editor, TextBlock lineNumberBlock)
    {
        var text = GetEditorText(editor);
        var lineCount = text.Split('\n').Length;

        var lineNumbers = new System.Text.StringBuilder();
        for (int i = 1; i <= lineCount; i++)
        {
            lineNumbers.AppendLine(i.ToString());
        }

        lineNumberBlock.Text = lineNumbers.ToString();

        // Usar o mesmo line height do editor para alinhamento perfeito
        var lineHeight = editor.FontSize * 1.4;
        lineNumberBlock.LineHeight = lineHeight;
        lineNumberBlock.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        editor.Document.LineHeight = lineHeight;
        editor.Document.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        editor.Document.PageWidth = 10000; // Keep source lines unwrapped, matching the gutter.
        foreach (var block in editor.Document.Blocks) block.Margin = new Thickness(0);
    }

    private void SetupLineNumberScrollSync()
    {
        EditorTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(EditorTextBox_ScrollChanged));
        VertexEditorTextBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(VertexEditorTextBox_ScrollChanged));
    }

    private void EditorTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        EditorLineNumbersScroller.ScrollToVerticalOffset(e.VerticalOffset);
    }

    private void VertexEditorTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        VertexEditorLineNumbersScroller.ScrollToVerticalOffset(e.VerticalOffset);
    }

    private void RunVideoAction(Action action)
    {
        if (_videoFailed) return;
        try { action(); }
        catch (Exception ex)
        {
            _videoFailed = true;
            _renderTimer.Stop();
            _compileDebounceTimer.Stop();
            ExitFullscreenIfActive();
            PreviewHost.Visibility = Visibility.Collapsed;
            PreviewTitleTextBlock.Text = "Prévia indisponível — falha de vídeo";
            StatusTextBlock.Text = "Vídeo interrompido. Você pode editar e salvar o código.";
            CompileSummaryTextBlock.Text = "ERRO DE VÍDEO — salve o código e reinicie a aplicação para tentar novamente.";
            CompileSummaryTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ErrorForegroundBrush");
            AppendDiagnostic($"[ERRO DE VÍDEO] {ex}");
        }
    }

    /// <summary>
    /// Runs an action that returns a value within the video guard.
    /// Returns null if the video subsystem has failed.
    /// </summary>
    private T? RunVideoActionAndCapture<T>(Func<T> action) where T : class
    {
        if (_videoFailed) return null;
        try { return action(); }
        catch (Exception ex)
        {
            _videoFailed = true;
            _renderTimer.Stop();
            _compileDebounceTimer.Stop();
            ExitFullscreenIfActive();
            PreviewHost.Visibility = Visibility.Collapsed;
            PreviewTitleTextBlock.Text = "Prévia indisponível — falha de vídeo";
            StatusTextBlock.Text = "Vídeo interrompido. Você pode editar e salvar o código.";
            CompileSummaryTextBlock.Text = "ERRO DE VÍDEO — salve o código e reinicie a aplicação para tentar novamente.";
            CompileSummaryTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ErrorForegroundBrush");
            AppendDiagnostic($"[ERRO DE VÍDEO] {ex}");
            return null;
        }
    }

    private void GoToErrorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_lastError?.Line is not int line) return;
        var editor = _lastError.Stage == "vertex" ? VertexEditorTextBox : EditorTextBox;
        ShaderEditorsTabControl.SelectedIndex = _lastError.Stage == "vertex" ? 1 : 0;
        var text = GetEditorText(editor);
        var offset = 0;
        for (var i = 1; i < line; i++)
        {
            var next = text.IndexOf('\n', offset);
            if (next < 0) return;
            offset = next + 1;
        }
        var end = text.IndexOf('\n', offset);
        if (end < 0) end = text.Length;
        editor.Focus();
        editor.Selection.Select(GetTextPointerAtOffset(editor.Document, offset), GetTextPointerAtOffset(editor.Document, end));
        editor.ScrollToVerticalOffset((line - 1) * editor.Document.LineHeight);
    }

    private static WpfBrush CreateBrush(string hexColor)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
