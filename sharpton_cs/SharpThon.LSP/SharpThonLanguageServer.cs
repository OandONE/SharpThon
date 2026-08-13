using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Sharpton.Core;
using Sprache;
using StreamJsonRpc;
using LspPosition = Microsoft.VisualStudio.LanguageServer.Protocol.Position;
using LspRange = Microsoft.VisualStudio.LanguageServer.Protocol.Range;

namespace SharpThon.LSP;

public sealed class SharpThonLanguageServer
{
    private readonly ConcurrentDictionary<Uri, string> documents = new();
    private JsonRpc? rpc;

    internal void Attach(JsonRpc jsonRpc) => rpc = jsonRpc;

    [JsonRpcMethod(Methods.InitializeName)]
    public Task<InitializeResult> InitializeAsync(InitializeParams parameters)
    {
        var result = new InitializeResult
        {
            Capabilities = new ServerCapabilities
            {
                TextDocumentSync = new TextDocumentSyncOptions
                {
                    OpenClose = true,
                    Change = TextDocumentSyncKind.Full
                },
                HoverProvider = true
            }
        };

        return Task.FromResult(result);
    }

    [JsonRpcMethod(Methods.InitializedName)]
    public Task InitializedAsync() => Task.CompletedTask;

    [JsonRpcMethod(Methods.TextDocumentHoverName)]
    public Task<Hover?> TextDocumentHoverAsync(TextDocumentPositionParams parameters)
    {
        if (!TryGetText(parameters.TextDocument.Uri, out var text) ||
            !TryGetWord(text, parameters.Position, out var word, out var range))
            return Task.FromResult<Hover?>(null);

        var description = Describe(word, text);
        if (description is null)
            return Task.FromResult<Hover?>(null);

        return Task.FromResult<Hover?>(new Hover
        {
            Contents = new MarkupContent
            {
                Kind = MarkupKind.Markdown,
                Value = description
            },
            Range = range
        });
    }

    // This public analysis endpoint is also used by didOpen/didChange to
    // publish diagnostics to VS Code's Problems panel.
    public Task<Diagnostic[]> TextDocumentDiagnosticsAsync(
        TextDocumentIdentifier textDocument)
    {
        if (!TryGetText(textDocument.Uri, out var text))
            return Task.FromResult(Array.Empty<Diagnostic>());

        return Task.FromResult(Analyze(text));
    }

    [JsonRpcMethod(Methods.TextDocumentDidOpenName)]
    public async Task DidOpenAsync(DidOpenTextDocumentParams parameters)
    {
        documents[parameters.TextDocument.Uri] = parameters.TextDocument.Text;
        await PublishDiagnosticsAsync(parameters.TextDocument.Uri);
    }

    [JsonRpcMethod(Methods.TextDocumentDidChangeName)]
    public async Task DidChangeAsync(DidChangeTextDocumentParams parameters)
    {
        var change = parameters.ContentChanges.LastOrDefault();
        if (change is null)
            return;

        documents[parameters.TextDocument.Uri] = change.Text;
        await PublishDiagnosticsAsync(parameters.TextDocument.Uri);
    }

    [JsonRpcMethod(Methods.TextDocumentDidCloseName)]
    public async Task DidCloseAsync(DidCloseTextDocumentParams parameters)
    {
        documents.TryRemove(parameters.TextDocument.Uri, out _);
        if (rpc is not null)
        {
            await rpc.NotifyWithParameterObjectAsync(
                Methods.TextDocumentPublishDiagnosticsName,
                new PublishDiagnosticParams
                {
                    Uri = parameters.TextDocument.Uri,
                    Diagnostics = Array.Empty<Diagnostic>()
                });
        }
    }

    [JsonRpcMethod(Methods.ShutdownName)]
    public Task<object?> ShutdownAsync() => Task.FromResult<object?>(null);

    [JsonRpcMethod(Methods.ExitName)]
    public void Exit() { }

    private async Task PublishDiagnosticsAsync(Uri uri)
    {
        if (rpc is null)
            return;

        var diagnostics = await TextDocumentDiagnosticsAsync(
            new TextDocumentIdentifier { Uri = uri });

        await rpc.NotifyWithParameterObjectAsync(
            Methods.TextDocumentPublishDiagnosticsName,
            new PublishDiagnosticParams
            {
                Uri = uri,
                Diagnostics = diagnostics
            });
    }

    private bool TryGetText(Uri uri, out string text)
    {
        if (documents.TryGetValue(uri, out text!))
            return true;

        if (uri.IsFile && File.Exists(uri.LocalPath))
        {
            text = File.ReadAllText(uri.LocalPath);
            return true;
        }

        text = string.Empty;
        return false;
    }

    private static Diagnostic[] Analyze(string text)
    {
        var diagnostics = new List<Diagnostic>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var transpiler = new Transpiler();

        // Transpiler exercises source preparation, parser integration, type
        // inference and symbol handling without writing generated files.
        try
        {
            transpiler.TranspileWithMapping(text);
        }
        catch (Exception exception)
        {
            diagnostics.Add(CreateDiagnostic(0, 0, exception.Message));
            return diagnostics.ToArray();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var code = StripComment(lines[i]).Trim();
            if (code.Length == 0 || code == "{" || code.StartsWith("import "))
                continue;

            try
            {
                SharpThonParser.Line.End().Parse(code);
            }
            catch (ParseException exception)
            {
                diagnostics.Add(CreateDiagnostic(
                    i,
                    Math.Max(0, lines[i].IndexOf(code, StringComparison.Ordinal)),
                    $"Syntax error: {exception.Message}"));
            }
        }

        return diagnostics.ToArray();
    }

    private static Diagnostic CreateDiagnostic(int line, int character, string message) =>
        new()
        {
            Range = new LspRange
            {
                Start = new LspPosition { Line = line, Character = character },
                End = new LspPosition { Line = line, Character = character + 1 }
            },
            Severity = DiagnosticSeverity.Error,
            Source = "SharpThon",
            Message = message
        };

    private static string? Describe(string word, string source)
    {
        var builtIns = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["def"] = "`def name(args) -> Type`\n\nDeclares a SharpThon function.",
            ["Write"] = "`Write(value)`\n\nWrites a value followed by a newline.",
            ["go"] = "`go expression`\n\nRuns an expression asynchronously.",
            ["await"] = "`await go expression`\n\nRuns and awaits an asynchronous expression.",
            ["import"] = "`import module`\n\nImports a sibling `.spy` module.",
            ["Any"] = "`Any` maps to C# `object`.",
            ["str"] = "`str` maps to C# `string`."
        };

        if (builtIns.TryGetValue(word, out var description))
            return description;

        var declaration = Regex.Match(
            source,
            $@"(?m)^\s*(?:def\s+)?{Regex.Escape(word)}\s*(?:\(([^)]*)\))?\s*(?:->\s*([A-Za-z_]\w*))?");

        return declaration.Success
            ? $"```sharpthon\n{declaration.Value.Trim()}\n```"
            : null;
    }

    private static bool TryGetWord(
        string text,
        LspPosition position,
        out string word,
        out LspRange range)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        if (position.Line < 0 || position.Line >= lines.Length)
        {
            word = string.Empty;
            range = new LspRange();
            return false;
        }

        var line = lines[position.Line];
        var cursor = Math.Clamp(position.Character, 0, line.Length);
        var start = cursor;
        var end = cursor;
        while (start > 0 && IsIdentifierCharacter(line[start - 1])) start--;
        while (end < line.Length && IsIdentifierCharacter(line[end])) end++;

        word = line[start..end];
        range = new LspRange
        {
            Start = new LspPosition { Line = position.Line, Character = start },
            End = new LspPosition { Line = position.Line, Character = end }
        };
        return word.Length > 0;
    }

    private static bool IsIdentifierCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static string StripComment(string line)
    {
        var index = line.IndexOf("//", StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }
}
