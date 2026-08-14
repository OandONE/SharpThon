using System.Text.RegularExpressions;
using Sprache;

namespace Sharpton.Core;

public class Transpiler
{
    private sealed class VariableSymbol
    {
        public string Name { get; init; } = "";
        public string Kind { get; set; } = "variable";
        public string? Type { get; set; }
        public string? KeyType { get; set; }
        public string? ValueType { get; set; }
    }

    private string? currentClass;
    private string? sourceDirectory;

    // Prevents circular imports:
    // A -> B -> A
    private readonly HashSet<string> visitedModules = new();

    // Keeps track of modules currently being expanded.
    private readonly HashSet<string> modulesInProgress = new();

    public string TranspileFile(string filepath)
    {
        sourceDirectory = Path.GetDirectoryName(
            Path.GetFullPath(filepath)
        )!;

        visitedModules.Clear();
        modulesInProgress.Clear();
        currentClass = null;

        var spCode = File.ReadAllText(filepath);

        var (processedCode, moduleBodies) =
            ProcessImports(spCode, allowUsingStatements: true);

        var mainCode = Transpile(processedCode);

        if (moduleBodies.Count == 0)
            return mainCode;

        return mainCode
            + "\n\n// --- Imported Modules ---\n\n"
            + string.Join("\n\n", moduleBodies);
    }

    public (string Code, List<int> SourceLineNumbers)
        TranspileFileWithMapping(string filepath)
    {
        sourceDirectory = Path.GetDirectoryName(
            Path.GetFullPath(filepath)
        )!;

        visitedModules.Clear();
        modulesInProgress.Clear();
        currentClass = null;

        var spCode = File.ReadAllText(filepath);

        var (processedCode, moduleBodies) =
            ProcessImports(spCode, allowUsingStatements: true);

        var result = TranspileWithMapping(processedCode);

        var mainCode = result.Code;
        var sourceLineNumbers = result.SourceLineNumbers;

        if (moduleBodies.Count > 0)
        {
            mainCode += "\n\n// --- Imported Modules ---\n\n";
            mainCode += string.Join("\n\n", moduleBodies);
        }

        return (mainCode, sourceLineNumbers);
    }

    // IMPORTS

    private (string processedCode, List<string> moduleBodies)
        ProcessImports(
            string spCode,
            bool allowUsingStatements)
    {
        var moduleBodies = new List<string>();
        var moduleMap = new Dictionary<string, string>();

        foreach (Match match in ModuleImportRegex.Matches(spCode))
        {
            var moduleName = match.Groups[1].Value;

            var modulePath = Path.Combine(
                sourceDirectory!,
                moduleName + ".spy"
            );

            if (!File.Exists(modulePath))
                continue;

            var className = ToPascalCase(moduleName);

            moduleMap[moduleName] = className;

            /*
             * Circular import protection.
             *
             * Example:
             *
             * test1 -> test2
             * test2 -> test1
             *
             * When test2 tries to import test1 again,
             * test1 has already been visited, so we do not
             * generate Test1 for a second time.
             */
            if (visitedModules.Contains(modulePath))
                continue;

            if (modulesInProgress.Contains(modulePath))
                continue;

            visitedModules.Add(modulePath);
            modulesInProgress.Add(modulePath);

            var body = TranspileModule(
                modulePath,
                className
            );

            modulesInProgress.Remove(modulePath);

            if (!string.IsNullOrWhiteSpace(body))
                moduleBodies.Add(body);
        }

        var lines = spCode.Split('\n');
        var newLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            var match = ModuleImportRegex.Match(trimmed);

            if (match.Success &&
                moduleMap.TryGetValue(
                    match.Groups[1].Value,
                    out var className))
            {
                /*
                 * using static is valid only at namespace/file level.
                 *
                 * Never put:
                 *
                 * public static class Test2
                 * {
                 *     using static Test1;   <-- INVALID C#
                 * }
                 *
                 * Therefore modules themselves do not receive
                 * using static statements.
                 */
                if (allowUsingStatements)
                {
                    newLines.Add(
                        $"using static {className};"
                    );
                }

                continue;
            }

            newLines.Add(line);
        }

        var processedCode = string.Join("\n", newLines);

        /*
         * Convert:
         *
         * math_utils.add()
         *
         * to:
         *
         * MathUtils.add()
         */
        foreach (var (moduleName, className) in moduleMap)
        {
            processedCode = Regex.Replace(
                processedCode,
                $@"\b{Regex.Escape(moduleName)}\.",
                $"{className}."
            );
        }

        return (processedCode, moduleBodies);
    }

    private string TranspileModule(
        string modulePath,
        string className)
    {
        var spCode = File.ReadAllText(modulePath);

        /*
         * Important:
         *
         * Modules must NOT generate "using static ..."
         * statements inside their class body.
         */
        var (processedCode, nestedModules) =
            ProcessImports(
                spCode,
                allowUsingStatements: false
            );

        /*
         * Transpile the module normally.
         *
         * Function return-type inference happens inside
         * Transpile(), exactly like the main file.
         */
        var body = Transpile(processedCode);

        /*
         * Only after return types have been resolved,
         * make module members public.
         */
        body = MakeModuleMembersPublic(body);

        var result =
            $"public static class {className}\n" +
            "{\n" +
            body +
            "\n}";

        if (nestedModules.Count > 0)
        {
            result +=
                "\n\n" +
                string.Join(
                    "\n\n",
                    nestedModules
                );
        }

        return result;
    }

    private static string MakeModuleMembersPublic(
        string transpiled)
    {
        /*
         * static void foo()
         * static object foo()
         * static int foo()
         *
         * become:
         *
         * public static void foo()
         * public static object foo()
         * public static int foo()
         */
        return Regex.Replace(
            transpiled,
            @"^(\s*)static\s+",
            "$1public static ",
            RegexOptions.Multiline
        );
    }

    private static string ToPascalCase(string snakeCase)
    {
        return string.Concat(
            snakeCase
                .Split('_')
                .Select(part =>
                    part.Length > 0
                        ? char.ToUpper(part[0]) + part[1..]
                        : "")
        );
    }

    private static readonly Regex ModuleImportRegex =
        new(
            @"^import\s+([A-Za-z_][A-Za-z0-9_]*)$",
            RegexOptions.Multiline
        );

    // MAIN TRANSPILER

    public string Transpile(string spCode)
    {
        var result = TranspileCore(spCode);
        return result.Code;
    }

    // MAPPING TRANSPILER

    public TranspileResult TranspileWithMapping(string spCode)
    {
        var result = TranspileCore(spCode);
        return new TranspileResult(
            result.Code,
            result.SourceLineNumbers
        );
    }

    // CORE TRANSPILATION + SYMBOL TABLE

    private (string Code, List<int> SourceLineNumbers)
        TranspileCore(string spCode)
    {
        spCode = PrepareSource(spCode);
        spCode = FixFloatLiterals(spCode);

        var results = new List<string>();
        var sourceLineNumbers = new List<int>();
        var sourceLines = spCode.Split('\n');

        // Analyze dictionary declarations and later index assignments before emitting C#.
        // This lets a dictionary widen to object when its value types are not stable.
        var dictionaryTypes = AnalyzeDictionaryTypes(sourceLines);

        // SharpThon variable symbol table.
        // Each block gets its own scope. Lookup walks from the
        // innermost scope to outer scopes.
        var variableScopes = new Stack<Dictionary<string, VariableSymbol>>();
        variableScopes.Push(new Dictionary<string, VariableSymbol>(StringComparer.Ordinal));

        for (int lineNumber = 0;
             lineNumber < sourceLines.Length;
             lineNumber++)
        {
            var line = sourceLines[lineNumber];
            var (rawCode, comment) = SplitLineComment(line);
            var code = rawCode.Trim();

            if (string.IsNullOrEmpty(code))
            {
                results.Add("");
                sourceLineNumbers.Add(lineNumber + 1);
                continue;
            }

            // Close scopes before resolving the current line. This makes
            // both `}` and `} else {` behave correctly.
            CloseVariableScopes(variableScopes, code);

            if (code.StartsWith("class "))
            {
                var parts = code.Split(
                    new[] { ' ', '{' },
                    StringSplitOptions.RemoveEmptyEntries
                );

                if (parts.Length >= 2)
                    currentClass = parts[1];
            }

            try
            {
                var result = SharpThonParser.Line.Parse(code);

                if (currentClass != null &&
                    result.StartsWith($"static void {currentClass}("))
                {
                    result = result.Replace(
                        $"static void {currentClass}(",
                        $"public {currentClass}("
                    );
                }

                if (IsFunctionDeclaration(code))
                {
                    var inferredType =
                        InferFunctionReturnType(
                            sourceLines,
                            lineNumber
                        );

                    result = ReplaceInferredFunctionReturnType(
                        result,
                        inferredType
                    );
                }

                // Parser intentionally treats `name = value` as a
                // declaration. The symbol table decides whether it is
                // actually a declaration or an assignment.
                result = NormalizeVariableStatement(
                    code,
                    result,
                    variableScopes,
                    dictionaryTypes
                );

                if (!string.IsNullOrEmpty(comment))
                    result += " " + comment;

                results.Add(result);
                sourceLineNumbers.Add(lineNumber + 1);

                // Open a scope after processing the line that contains `{`.
                OpenVariableScopes(
                    variableScopes,
                    code,
                    result
                );
            }
            catch (Sprache.ParseException)
            {
                results.Add(code);
                sourceLineNumbers.Add(lineNumber + 1);

                // Keep scope tracking alive even when a line is not handled
                // by the parser yet.
                OpenVariableScopes(
                    variableScopes,
                    code,
                    code
                );
            }
        }

        var formatted = FormatResults(
            results,
            sourceLineNumbers
        );

        return (formatted.Code, formatted.SourceLineNumbers);
    }

    private static string NormalizeVariableStatement(
        string sourceCode,
        string transpiledCode,
        Stack<Dictionary<string, VariableSymbol>> scopes,
        Dictionary<string, (string KeyType, string ValueType)> dictionaryTypes)
    {
        var name = GetAssignmentName(sourceCode);
        if (name == null)
            return transpiledCode;

        // Dictionary declarations are inferred globally so a later incompatible
        // assignment can widen the declaration to Dictionary<key, object>.
        if (dictionaryTypes.TryGetValue(name, out var dictionaryType) &&
            IsDictionaryDeclaration(sourceCode, name))
        {
            var desiredType =
                $"Dictionary<{dictionaryType.KeyType}, {dictionaryType.ValueType}>";

            transpiledCode = Regex.Replace(
                transpiledCode,
                @"new\s+Dictionary<[^>]+>",
                _ => $"new {desiredType}",
                RegexOptions.None);

            if (HasExplicitType(sourceCode))
            {
                scopes.Peek()[name] = new VariableSymbol
                {
                    Name = name,
                    Kind = "dictionary",
                    KeyType = dictionaryType.KeyType,
                    ValueType = dictionaryType.ValueType,
                    Type = desiredType
                };
                return transpiledCode;
            }
        }

        // Explicitly typed declarations always remain declarations.
        if (HasExplicitType(sourceCode))
        {
            scopes.Peek()[name] = new VariableSymbol
            {
                Name = name,
                Type = ExtractExplicitType(sourceCode)
            };
            return transpiledCode;
        }

        if (IsKnownVariable(scopes, name))
        {
            // Existing symbol => assignment, never `var` again.
            return transpiledCode.Replace(
                $"var {name} =",
                $"{name} =",
                StringComparison.Ordinal
            );
        }

        var symbol = new VariableSymbol { Name = name };

        if (dictionaryTypes.TryGetValue(name, out var info))
        {
            symbol.Kind = "dictionary";
            symbol.KeyType = info.KeyType;
            symbol.ValueType = info.ValueType;
            symbol.Type = $"Dictionary<{info.KeyType}, {info.ValueType}>";
        }

        scopes.Peek()[name] = symbol;
        return transpiledCode;
    }

    private static bool IsDictionaryDeclaration(string sourceCode, string name)
    {
        return Regex.IsMatch(
            sourceCode.Trim(),
            $@"^(?:(?:public|private|protected)\s+)?{Regex.Escape(name)}\s*=\s*\{{");
    }

    private static string? ExtractExplicitType(string code)
    {
        var match = Regex.Match(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*:\s*([A-Za-z_][A-Za-z0-9_]*)"
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    private static Dictionary<string, (string KeyType, string ValueType)> AnalyzeDictionaryTypes(
        string[] sourceLines)
    {
        var result = new Dictionary<string, (string KeyType, string ValueType)>(
            StringComparer.Ordinal
        );

        foreach (var rawLine in sourceLines)
        {
            var code = RemoveLineComment(rawLine).Trim();
            if (code.Length == 0)
                continue;

            var declaration = Regex.Match(
                code,
                @"^(?:(?:public|private|protected)\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*\{(.*)\}\s*;?$"
            );

            if (declaration.Success)
            {
                var name = declaration.Groups[1].Value;
                var entries = SplitTopLevel(declaration.Groups[2].Value, ',');
                var keyTypes = new HashSet<string>(StringComparer.Ordinal);
                var valueTypes = new HashSet<string>(StringComparer.Ordinal);

                foreach (var entry in entries)
                {
                    var pair = SplitDictionaryEntry(entry);
                    if (pair == null)
                        continue;

                    keyTypes.Add(InferSharpThonValueType(pair.Value.Key));
                    valueTypes.Add(InferSharpThonValueType(pair.Value.Value));
                }

                result[name] = (
                    keyTypes.Count == 1 ? keyTypes.First() : "object",
                    valueTypes.Count == 1 ? valueTypes.First() : "object"
                );
            }
        }

        // A later index assignment can change the value type.
        foreach (var rawLine in sourceLines)
        {
            var code = RemoveLineComment(rawLine).Trim();
            var assignment = Regex.Match(
                code,
                @"^([A-Za-z_][A-Za-z0-9_]*)\s*\[([^]]+)\]\s*=\s*(.+?)\s*;?$"
            );

            if (!assignment.Success)
                continue;

            var name = assignment.Groups[1].Value;
            if (!result.TryGetValue(name, out var info))
                continue;

            var keyType = InferSharpThonValueType(assignment.Groups[2].Value);
            var valueType = InferSharpThonValueType(assignment.Groups[3].Value);

            if (keyType != info.KeyType && info.KeyType != "object")
                info.KeyType = "object";

            if (valueType != info.ValueType && info.ValueType != "object")
                info.ValueType = "object";

            result[name] = info;
        }

        return result;
    }

    private static (string Key, string Value)? SplitDictionaryEntry(string entry)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < entry.Length; i++)
        {
            var c = entry[i];
            if (c == '"' && !escaped)
                inString = !inString;

            if (!inString)
            {
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ':' && depth == 0)
                    return (entry[..i].Trim(), entry[(i + 1)..].Trim());
            }

            escaped = c == '\\' && !escaped;
            if (c != '\\') escaped = false;
        }

        return null;
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"' && !escaped)
                inString = !inString;

            if (!inString)
            {
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == separator && depth == 0)
                {
                    result.Add(text[start..i].Trim());
                    start = i + 1;
                }
            }

            escaped = c == '\\' && !escaped;
            if (c != '\\') escaped = false;
        }

        if (start < text.Length)
            result.Add(text[start..].Trim());

        return result.Where(x => x.Length > 0).ToList();
    }

    private static string InferSharpThonValueType(string value)
    {
        value = value.Trim();

        if (value.StartsWith("\"") && value.EndsWith("\""))
            return "string";

        if (value == "true" || value == "false")
            return "bool";

        if (Regex.IsMatch(value, @"^-?\d+$"))
            return "int";

        if (Regex.IsMatch(value, @"^-?\d+\.\d+[fF]?$"))
            return value.EndsWith("f", StringComparison.OrdinalIgnoreCase) ? "float" : "double";

        return "object";
    }

    private static bool HasExplicitType(string code)
    {
        return Regex.IsMatch(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*:\s*[A-Za-z_][A-Za-z0-9_]*\s*=|^(?:public|private|protected)\s+[A-Za-z_][A-Za-z0-9_]*\s+[^=]+\s*="
        ) || Regex.IsMatch(
            code,
            @"^(?:int|string|str|bool|float|double|long|object|Any)\s+[A-Za-z_][A-Za-z0-9_]*\s*="
        );
    }

    private static string? GetAssignmentName(string code)
    {
        var match = Regex.Match(
            code,
            @"^(?:(?:public|private|protected)\s+)?(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*[A-Za-z_][A-Za-z0-9_]*\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*="
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsKnownVariable(
        Stack<Dictionary<string, VariableSymbol>> scopes,
        string name)
    {
        foreach (var scope in scopes)
        {
            if (scope.ContainsKey(name))
                return true;
        }

        return false;
    }

    private static void CloseVariableScopes(
        Stack<Dictionary<string, VariableSymbol>> scopes,
        string code)
    {
        var closingBraces = CountBraces(code, '}');

        for (int i = 0; i < closingBraces; i++)
        {
            // Keep the root scope alive.
            if (scopes.Count > 1)
                scopes.Pop();
        }
    }

    private static void OpenVariableScopes(
        Stack<Dictionary<string, VariableSymbol>> scopes,
        string sourceCode,
        string transpiledCode)
    {
        var openingBraces = CountBraces(sourceCode, '{');
        var closingBraces = CountBraces(sourceCode, '}');
        var netOpeningScopes = Math.Max(0, openingBraces - closingBraces);

        for (int i = 0; i < netOpeningScopes; i++)
            scopes.Push(new Dictionary<string, VariableSymbol>(StringComparer.Ordinal));

        if (netOpeningScopes == 0)
            return;

        // Function parameters are symbols in the newly opened function scope.
        RegisterFunctionParameters(sourceCode, scopes.Peek());

        // `for (i in ...)` introduces i in its new scope.
        var forMatch = Regex.Match(
            sourceCode,
            @"^for\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s+in\b"
        );

        if (forMatch.Success)
            scopes.Peek()[forMatch.Groups[1].Value] = new VariableSymbol { Name = forMatch.Groups[1].Value };

        // catch/except variable is also local to the new scope.
        var catchMatch = Regex.Match(
            sourceCode,
            @"^(?:catch|except)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s+as\s+([A-Za-z_][A-Za-z0-9_]*)"
        );

        if (catchMatch.Success)
            scopes.Peek()[catchMatch.Groups[1].Value] = new VariableSymbol { Name = catchMatch.Groups[1].Value };
    }

    private static void RegisterFunctionParameters(
        string code,
        Dictionary<string, VariableSymbol> scope)
    {
        var match = Regex.Match(
            code,
            @"\bdef\s+[A-Za-z_][A-Za-z0-9_]*\s*\(([^)]*)\)"
        );

        if (!match.Success)
            return;

        foreach (var parameter in match.Groups[1].Value.Split(','))
        {
            var text = parameter.Trim();
            if (text.Length == 0)
                continue;

            var nameMatch = Regex.Match(
                text,
                @"(?:^|\s)([A-Za-z_][A-Za-z0-9_]*)\s*(?::|$)"
            );

            if (nameMatch.Success)
                scope[nameMatch.Groups[1].Value] = new VariableSymbol { Name = nameMatch.Groups[1].Value };
            else
            {
                var plainName = Regex.Match(
                    text,
                    @"^[A-Za-z_][A-Za-z0-9_]*$"
                );

                if (plainName.Success)
                    scope[plainName.Value] = new VariableSymbol { Name = plainName.Value };
            }
        }
    }

    private static int CountBraces(string text, char brace)
    {
        int count = 0;
        bool inString = false;
        bool escaped = false;

        foreach (var c in text)
        {
            if (c == '"' && !escaped)
                inString = !inString;

            if (!inString && c == brace)
                count++;

            escaped = c == '\\' && !escaped;
            if (c != '\\')
                escaped = false;
        }

        return count;
    }

    // SOURCE PREPARATION

    private static string PrepareSource(string spCode)
    {
        spCode = Regex.Replace(
            spCode,
            @"Write\((.+)\)",
            match =>
            {
                var inner =
                    match.Groups[1].Value;

                int depth = 0;

                for (int i = 0;
                     i < inner.Length;
                     i++)
                {
                    if (inner[i] == '(')
                        depth++;

                    if (inner[i] == ')')
                        depth--;

                    if (depth < 0)
                    {
                        return
                            $"Console.WriteLine(" +
                            $"{inner[..i]});";
                    }
                }

                return
                    $"Console.WriteLine({inner});";
            }
        );

        spCode = Regex.Replace(
            spCode,
            @"f""([^""]*)""",
            m =>
                "$\"" +
                m.Groups[1].Value +
                "\""
        );

        return spCode;
    }

    // FUNCTION RETURN TYPE INFERENCE

    private static bool IsFunctionDeclaration(
        string code)
    {
        /*
         * Supports:
         *
         * def foo()
         * public def foo()
         * private def foo()
         * static def foo()
         *
         * and:
         *
         * def foo() -> int
         * public def foo() -> str
         */

        return Regex.IsMatch(
            code,
            @"^(?:(?:public|private|protected|static)\s+)*def\s+[A-Za-z_][A-Za-z0-9_]*\s*\("
        );
    }

    private static string InferFunctionReturnType(
        string[] sourceLines,
        int declarationLine)
    {
        var declaration =
            sourceLines[declarationLine];

        /*
         * Explicit return type always wins.
         *
         * Example:
         *
         * def foo() -> int
         *
         * The parser has already generated int.
         */
        if (Regex.IsMatch(
                declaration,
                @"->\s*[A-Za-z_][A-Za-z0-9_]*"
            ))
        {
            return "";
        }

        /*
         * Find the function body.
         */
        int braceDepth = 0;
        bool bodyStarted = false;

        for (int i = declarationLine;
             i < sourceLines.Length;
             i++)
        {
            var line =
                RemoveLineComment(
                    sourceLines[i]
                );

            foreach (char c in line)
            {
                if (c == '{')
                {
                    braceDepth++;
                    bodyStarted = true;
                }
                else if (c == '}')
                {
                    braceDepth--;
                }
            }

            if (i == declarationLine &&
                !bodyStarted)
            {
                continue;
            }

            /*
             * Look for a return with an expression.
             *
             * return "hello"
             * return x
             * return x + y
             */
            if (Regex.IsMatch(
                    line,
                    @"\breturn\s+.+"
                ))
            {
                return "object";
            }

            /*
             * Function body finished.
             */
            if (bodyStarted && braceDepth <= 0)
                break;
        }

        /*
         * No explicit return expression:
         *
         * def foo() {
         *     Write("hello")
         * }
         *
         * => void
         */
        return "void";
    }

    private static string RemoveLineComment(
        string line)
    {
        return SplitLineComment(line).Code;
    }

    private static (string Code, string Comment) SplitLineComment(string line)
    {
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"' && !escaped)
                inString = !inString;

            if (!inString)
            {
                if (c == '#')
                    return (line[..i], "//" + line[i..]);

                if (c == '/' &&
                    i + 1 < line.Length &&
                    line[i + 1] == '/')
                {
                    return (line[..i], line[i..]);
                }
            }

            escaped = c == '\\' && !escaped;

            if (c != '\\')
                escaped = false;
        }

        return (line, "");
    }

    private static string ReplaceInferredFunctionReturnType(
    string transpiledDeclaration,
    string inferredType)
{
    // Empty means the function already has an explicit return type.
    if (string.IsNullOrEmpty(inferredType))
        return transpiledDeclaration;

    /*
     * Parser currently generates:
     *
     * static object foo()
     *
     * for functions without an explicit return type.
     *
     * Replace only the "object" return type.
     */
    var match = Regex.Match(
        transpiledDeclaration,
        @"^(\s*(?:(?:public|private|protected|static)\s+)*)object(\s+[A-Za-z_][A-Za-z0-9_]*\s*\()"
    );

    if (!match.Success)
        return transpiledDeclaration;

    var prefix = match.Groups[1].Value;
    var functionPart = match.Groups[2].Value;

    var suffixStart = match.Index + match.Length;
    var suffix = transpiledDeclaration.Substring(suffixStart);

    return prefix
        + inferredType
        + functionPart
        + suffix;
}

    // FORMATTING

    private static (
        string Code,
        List<int> SourceLineNumbers
    ) FormatResults(
        List<string> results,
        List<int> sourceLineNumbers)
    {
        var finalLines = new List<string>();
        var finalSourceLines = new List<int>();

        for (int i = 0;
             i < results.Count;
             i++)
        {
            var line = results[i];

            var trimmed = line.Trim();

            var codePart =
                trimmed.Contains("//")
                    ? trimmed.Split("//")[0].Trim()
                    : trimmed;

            if (
                string.IsNullOrEmpty(codePart) ||
                codePart.EndsWith(';') ||
                codePart.EndsWith('{') ||
                codePart.EndsWith('}')
            )
            {
                finalLines.Add(line);
            }
            else
            {
                finalLines.Add(line + ";");
            }

            finalSourceLines.Add(
                sourceLineNumbers[i]
            );
        }

        int indent = 0;

        var formatted =
            new List<string>();

        for (int i = 0;
             i < finalLines.Count;
             i++)
        {
            var trimmed =
                finalLines[i].Trim();

            if (trimmed.StartsWith("}"))
                indent--;

            formatted.Add(
                new string(
                    ' ',
                    Math.Max(0, indent) * 4
                ) + trimmed
            );

            if (trimmed.EndsWith("{"))
                indent++;
        }

        return (
            string.Join(
                "\n",
                formatted
            ),
            finalSourceLines
        );
    }

    private static string FixFloatLiterals(string code)
    {
        // SharpThon supports both: 
        //   float x = 3.14
        //   x: float = 3.14
        // C# requires the f suffix for float literals.
        // Do not touch string literals or literals that already have a suffix.

        code = Regex.Replace(
            code,
            @"\bfloat\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(-?\d+(?:\.\d+)?)(?![fFdDmM])",
            m => $"float {m.Groups[1].Value} = {m.Groups[2].Value}f"
        );

        code = Regex.Replace(
            code,
            @"\b([A-Za-z_][A-Za-z0-9_]*)\s*:\s*float\s*=\s*(-?\d+(?:\.\d+)?)(?![fFdDmM])",
            m => $"{m.Groups[1].Value}: float = {m.Groups[2].Value}f"
        );

        return code;
    }
}