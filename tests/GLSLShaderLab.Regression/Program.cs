using System;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using GLSLShaderLab.App.Wpf;
using GLSLShaderLab.Core.Services;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        foreach (var (log, expected) in new (string, int?)[] {
            ("0(12) : error C0000: syntax error", 12),
            ("ERROR: 0:7: syntax error", 7),
            ("0:19(4): error: undeclared identifier", 19),
            ("WARNING: 2:31: warning", 31),
            ("link error: incompatible types", null),
            ("0(0): error", null),
            ("ERROR: 0:99999999999999: bad", null) })
            Check(ShaderDiagnosticParser.TryParseLine(log) == expected, log);

        var app = new App();
        app.InitializeComponent();
        var window = new MainWindow();
        var editor = (RichTextBox)window.FindName("EditorTextBox");
        editor.Document = new FlowDocument();
        editor.Document.Blocks.Add(new Paragraph(new Run("first")));
        editor.Document.Blocks.Add(new Paragraph(new Run("second")));
        var pointerMethod = typeof(MainWindow).GetMethod("GetTextPointerAtOffset", BindingFlags.NonPublic | BindingFlags.Static)!;
        var pointer = (TextPointer)pointerMethod.Invoke(null, new object[] { editor.Document, 7 })!;
        Check(new TextRange(pointer, editor.Document.ContentEnd).Text.StartsWith("second"), "paragraph offset");
        var setEditorText = typeof(MainWindow).GetMethod("SetEditorText", BindingFlags.NonPublic | BindingFlags.Instance)!;
        setEditorText.Invoke(window, new object[] { editor, "original", true });
        editor.CaretPosition = editor.Document.ContentEnd;
        editor.Selection.Text = " edit";
        var undoEditorChange = typeof(MainWindow).GetMethod("UndoEditorChange", BindingFlags.NonPublic | BindingFlags.Instance)!;
        undoEditorChange.Invoke(window, new object[] { editor });
        Check(new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.StartsWith("original"), "editor undo restores previous edit");
        var redoEditorChange = typeof(MainWindow).GetMethod("RedoEditorChange", BindingFlags.NonPublic | BindingFlags.Instance)!;
        redoEditorChange.Invoke(window, new object[] { editor });
        Check(new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.StartsWith("original edit"), "editor redo restores undone edit");
        var guard = typeof(MainWindow).GetMethod("RunVideoAction", BindingFlags.NonPublic | BindingFlags.Instance)!;
        guard.Invoke(window, new object[] { (Action)(() => throw new InvalidOperationException("simulated graphics failure")) });
        Check(((TextBlock)window.FindName("StatusTextBlock")).Text.Contains("salvar"), "editor survives graphics failure");
        Check(((TextBox)window.FindName("DiagnosticsTextBox")).Text.Contains("simulated graphics failure"), "graphics details retained");
        bool called = false;
        guard.Invoke(window, new object[] { (Action)(() => called = true) });
        Check(!called, "graphics stopped after failure");
        var timerField = typeof(MainWindow).GetField("_autoSaveTimer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(((System.Windows.Threading.DispatcherTimer)timerField.GetValue(window)!).Interval == TimeSpan.FromMinutes(10), "ten minute autosave interval");
        var integrationPath = Path.Combine(Path.GetTempPath(), "ShaderLab-" + Guid.NewGuid() + ".frag");
        try
        {
            setEditorText.Invoke(window, new object[] { editor, "original", true });
            File.WriteAllText(integrationPath, "external shader");
            typeof(MainWindow).GetField("_fragmentFile", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(window, new ShaderFileSync(integrationPath, "original"));
            var poll = typeof(MainWindow).GetMethod("PollShaderFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
            poll.Invoke(window, null);
            poll.Invoke(window, null);
            Check(new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text.StartsWith("external shader"), "external reload updates WPF editor even after video failure");
        }
        finally { File.Delete(integrationPath); }
        TestFileSync();
        Console.WriteLine("All regression checks passed (diagnostics, graphics failure and file synchronization).");
    }
    static void TestFileSync()
    {
        var root = Path.Combine(Path.GetTempPath(), "ShaderLabSync-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "shader.frag");
            File.WriteAllText(path, "original\r\n");
            var sync = new ShaderFileSync(path, "original\n");
            Check(sync.Poll("original\n") is null, "newline normalization");
            Check(sync.AutoSave("local"), "autosave local change");
            Check(File.ReadAllText(path) == "local", "autosave writes file");
            Check(sync.Poll("local") is null, "self save ignored");
            File.WriteAllText(path, "external");
            Check(!sync.AutoSave("local edit"), "autosave blocks unseen external changes");
            Check(File.ReadAllText(path) == "external", "external edit preserved");
            Check(sync.Poll("local") is null, "debounce first observation");
            Check(sync.Poll("local") == "external", "reload stable external save");
            File.WriteAllText(path, "conflicting");
            sync.Poll("unsaved local");
            Check(sync.Poll("unsaved local") is null && sync.HasConflict, "concurrent editor changes conflict");
            Check(sync.Poll("external") == "conflicting", "undo local edits resolves conflict");
            File.Delete(path);
            bool missing = false;
            try { sync.AutoSave("keep me"); } catch (FileNotFoundException) { missing = true; }
            Check(missing && !File.Exists(path), "deleted file is not recreated by autosave");
            var replacement = Path.Combine(root, "replacement.tmp");
            File.WriteAllText(replacement, "atomic replacement");
            File.Move(replacement, path);
            sync.Poll("conflicting");
            Check(sync.Poll("conflicting") == "atomic replacement", "atomic replacement detected");
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                bool blocked = false;
                try { sync.AutoSave("blocked"); } catch (IOException) { blocked = true; }
                Check(blocked, "locked file remains untouched");
            }
            Check(sync.AutoSave("short"), "retry after lock released");
            Check(File.ReadAllText(path) == "short", "shorter save truncates old content");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
