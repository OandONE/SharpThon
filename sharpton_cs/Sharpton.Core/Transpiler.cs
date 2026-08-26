using System.Text.RegularExpressions;
using System.Text;
using Sprache;

namespace Sharpton.Core;

public sealed class SharpThonImportException : Exception
{
    public SharpThonImportException(
        string sourceFile,
        int lineNumber,
        string moduleFileName)
        : base($"Imported module '{moduleFileName}' not found.")
    {
        SourceFile = sourceFile;
        LineNumber = lineNumber;
    }

    public SharpThonImportException(
        string sourceFile,
        int lineNumber,
        string packageName,
        bool isPackage)
        : base($"Package '{packageName}' does not have an index.spy file")
    {
        SourceFile = sourceFile;
        LineNumber = lineNumber;
    }

    public string SourceFile { get; }
    public int LineNumber { get; }
}

public sealed class SharpThonCircularImportException : Exception
{
    public SharpThonCircularImportException(
        string sourceFile,
        int lineNumber,
        IReadOnlyList<string> cycle)
        : base("Circular import detected.")
    {
        SourceFile = sourceFile;
        LineNumber = lineNumber;
        Cycle = cycle;
    }

    public string SourceFile { get; }
    public int LineNumber { get; }
    public IReadOnlyList<string> Cycle { get; }
}

public class Transpiler
{
    private string? currentClass;
    private string? currentInterface;
    private int currentTypeBraceDepth;
    private int sourceBraceDepth;
    private string? sourceDirectory;

    // Prevents circular imports:
    // A -> B -> A
    private readonly HashSet<string> visitedModules = new();

    // Keeps track of modules currently being expanded.
    private readonly HashSet<string> modulesInProgress = new();

    // Ordered DFS path for the modules currently being expanded. A HashSet
    // can detect a back edge, while this list lets us report the full cycle.
    private readonly List<string> importPath = new();

    private readonly HashSet<string> _userDefinedFunctions = new(StringComparer.Ordinal);

    public string TranspileFile(string filepath)
    {
        sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(filepath))!;
        InitializeImportTracking(filepath);

        var spCode = File.ReadAllText(filepath);
        var (processedCode, moduleBodies) = ProcessImports(
            spCode,
            allowUsingStatements: true,
            sourceFile: filepath
        );

        var mainCode = Transpile(processedCode);

        if (moduleBodies.Count == 0)
            return mainCode;

        // Separate using directives from the main code
        var usingLines = new List<string>();
        var nonUsingLines = new List<string>();
        foreach (var line in mainCode.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ", StringComparison.Ordinal))
                usingLines.Add(line);
            else
                nonUsingLines.Add(line);
        }

        // Remove extra top-level main(); call
        var filteredNonUsingLines = nonUsingLines
            .Where(line => !Regex.IsMatch(line.Trim(), @"^main\(\);\s*$"))
            .ToList();

        // Change main method access modifier to public
        for (int i = 0; i < filteredNonUsingLines.Count; i++)
        {
            var line = filteredNonUsingLines[i];
            if (Regex.IsMatch(line.Trim(), @"^static\s+void\s+main\s*\("))
            {
                filteredNonUsingLines[i] = line.Replace(
                    "static void main(",
                    "public static void main("
                );
            }
        }

        var result = new StringBuilder();
        // 1. all USINGs
        result.AppendLine(string.Join("\n", usingLines));
        // 2. Call wrapper at top-level
        result.AppendLine("SharpThonProgram.main();");
        // 3. Class wrapper
        result.AppendLine("public static class SharpThonProgram");
        result.AppendLine("{");
        result.AppendLine(string.Join("\n", filteredNonUsingLines));
        result.AppendLine("}");
        // 4. modules
        if (moduleBodies.Count > 0)
        {
            result.AppendLine("\n// --- Imported Modules ---\n");
            result.AppendLine(string.Join("\n\n", moduleBodies));
        }

        return result.ToString();
    }

    public (string Code, List<int> SourceLineNumbers) TranspileFileWithMapping(string filepath)
    {
        sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(filepath))!;
        InitializeImportTracking(filepath);

        var spCode = File.ReadAllText(filepath);
        var (processedCode, moduleBodies) = ProcessImports(
            spCode,
            allowUsingStatements: true,
            sourceFile: filepath
        );

        var result = TranspileWithMapping(processedCode);
        var mainCode = result.Code;
        var sourceLineNumbers = result.SourceLineNumbers;

        if (moduleBodies.Count == 0)
            return (mainCode, sourceLineNumbers);

        var usingLines = new List<string>();
        var nonUsingLines = new List<string>();
        var usingSourceLines = new List<int>();
        var nonUsingSourceLines = new List<int>();
        var lines = mainCode.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("using ", StringComparison.Ordinal))
            {
                usingLines.Add(line);
                usingSourceLines.Add(sourceLineNumbers[i]);
            }
            else
            {
                nonUsingLines.Add(line);
                nonUsingSourceLines.Add(sourceLineNumbers[i]);
            }
        }

        // Remove extra main();
        var filteredPairs = nonUsingLines
            .Select((line, idx) => new { Line = line, Source = nonUsingSourceLines[idx] })
            .Where(pair => !Regex.IsMatch(pair.Line.Trim(), @"^main\(\);\s*$"))
            .ToList();

        // Change main method to public
        for (int i = 0; i < filteredPairs.Count; i++)
        {
            var pair = filteredPairs[i];
            if (Regex.IsMatch(pair.Line.Trim(), @"^static\s+void\s+main\s*\("))
            {
                pair = new
                {
                    Line = pair.Line.Replace("static void main(", "public static void main("),
                    Source = pair.Source
                };
                filteredPairs[i] = pair;
            }
        }

        var newCode = new List<string>();
        var newSourceLines = new List<int>();

        // USINGs
        newCode.AddRange(usingLines);
        newSourceLines.AddRange(usingSourceLines);

        // top-level call
        newCode.Add("SharpThonProgram.main();");
        newSourceLines.Add(1);

        // Class wrapper
        newCode.Add("public static class SharpThonProgram");
        newSourceLines.Add(1);
        newCode.Add("{");
        newSourceLines.Add(1);
        newCode.AddRange(filteredPairs.Select(p => p.Line));
        newSourceLines.AddRange(filteredPairs.Select(p => p.Source));
        newCode.Add("}");
        newSourceLines.Add(1);

        // modules
        if (moduleBodies.Count > 0)
        {
            newCode.Add("");
            newSourceLines.Add(1);
            newCode.Add("// --- Imported Modules ---");
            newSourceLines.Add(1);
            newCode.AddRange(moduleBodies);
            // For simplicity, set module lines to 1
            for (int i = 0; i < moduleBodies.Count; i++)
                newSourceLines.Add(1);
        }

        return (string.Join("\n", newCode), newSourceLines);
    }

    // IMPORTS

    private void InitializeImportTracking(string filepath)
    {
        visitedModules.Clear();
        modulesInProgress.Clear();
        importPath.Clear();
        currentClass = null;
        currentInterface = null;
        currentTypeBraceDepth = -1;
        sourceBraceDepth = 0;

        // The entry file must be part of the DFS path as well. Otherwise
        // a.spy -> b.spy -> a.spy would not be detected until too late.
        var rootPath = Path.GetFullPath(filepath);
        modulesInProgress.Add(rootPath);
        importPath.Add(rootPath);
    }

    private (string processedCode, List<string> moduleBodies)
        ProcessImports(
            string spCode,
            bool allowUsingStatements,
            string sourceFile)
    {
        var moduleBodies = new List<string>();
        var moduleMap = new Dictionary<string, string>();

        foreach (Match match in ModuleImportRegex.Matches(spCode))
        {
            // Separate module list with commas
            var moduleList = match.Groups[1].Value;
            var moduleNames = moduleList.Split(',')
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrEmpty(m));

            foreach (var moduleName in moduleNames)
            {
                var moduleParts = moduleName.Split('.');
                var packagePath = Path.GetFullPath(
                    Path.Combine(sourceDirectory!, Path.Combine(moduleParts))
                );

                string modulePath;

                if (Directory.Exists(packagePath))
                {
                    modulePath = Path.Combine(packagePath, "index.spy");
                    if (!File.Exists(modulePath))
                    {
                        var missingPackageLineNumber =
                            spCode[..match.Index].Count(c => c == '\n') + 1;
                        throw new SharpThonImportException(
                            sourceFile,
                            missingPackageLineNumber,
                            moduleName,
                            isPackage: true
                        );
                    }
                }
                else
                {
                    modulePath = Path.GetFullPath(
                        Path.Combine(
                            sourceDirectory!,
                            Path.Combine(moduleParts) + ".spy"
                        )
                    );

                    if (!File.Exists(modulePath))
                    {
                        var missingModuleLineNumber =
                            spCode[..match.Index].Count(c => c == '\n') + 1;
                        throw new SharpThonImportException(
                            sourceFile,
                            missingModuleLineNumber,
                            moduleName + ".spy"
                        );
                    }
                }

                var className = ToPascalCase(moduleName);
                moduleMap[moduleName] = className;

                var lineNumber =
                    spCode[..match.Index].Count(c => c == '\n') + 1;

                // check circular import
                if (modulesInProgress.Contains(modulePath))
                {
                    var cycleStart = importPath.IndexOf(modulePath);
                    var cycle = importPath
                        .Skip(cycleStart)
                        .Append(modulePath)
                        .Select(path => Path.GetFileName(path)!)
                        .ToList();

                    throw new SharpThonCircularImportException(
                        sourceFile,
                        lineNumber,
                        cycle
                    );
                }

                if (visitedModules.Contains(modulePath))
                    continue;

                visitedModules.Add(modulePath);
                modulesInProgress.Add(modulePath);
                importPath.Add(modulePath);

                try
                {
                    var body = TranspileModule(modulePath, className);
                    if (!string.IsNullOrWhiteSpace(body))
                        moduleBodies.Add(body);
                }
                finally
                {
                    importPath.RemoveAt(importPath.Count - 1);
                    modulesInProgress.Remove(modulePath);
                }
            }
        }

        // convert lines import to using static
        var lines = spCode.Split('\n');
        var newLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var match = ModuleImportRegex.Match(trimmed);

            if (match.Success)
            {
                var moduleList = match.Groups[1].Value;
                var moduleNames = moduleList.Split(',')
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrEmpty(m));

                bool allFound = true;
                var classNames = new List<string>();
                foreach (var moduleName in moduleNames)
                {
                    if (moduleMap.TryGetValue(moduleName, out var className))
                        classNames.Add(className);
                    else
                    {
                        allFound = false;
                        break;
                    }
                }

                if (allFound && allowUsingStatements)
                {
                    foreach (var className in classNames)
                        newLines.Add($"using static {className};");
                    continue;
                }
            }

            newLines.Add(line);
        }

        var processedCode = string.Join("\n", newLines);

        // Replace module name by class name in codes
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

    private string TranspileModule(string modulePath, string className)
    {
        var spCode = File.ReadAllText(modulePath);
        var (processedCode, nestedModules) = ProcessImports(
            spCode,
            allowUsingStatements: false,
            sourceFile: modulePath
        );

        var body = Transpile(processedCode);
        body = MakeModuleMembersPublic(body);

        // Remove ever line using from body
        var lines = body.Split('\n');
        var filteredLines = lines.Where(line =>
            !line.TrimStart().StartsWith("using ", StringComparison.Ordinal)
        ).ToList();
        body = string.Join("\n", filteredLines);

        var result = $"public static class {className}\n{{\n{body}\n}}";

        if (nestedModules.Count > 0)
        {
            result += "\n\n" + string.Join("\n\n", nestedModules);
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
                .Replace('.', '_')
                .Split('_')
                .Select(part =>
                    part.Length > 0
                        ? char.ToUpper(part[0]) + part[1..]
                        : "")
        );
    }

    private static readonly Regex ModuleImportRegex =
        new(
            @"^import\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*(?:\s*,\s*[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)*)$",
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

    private (string Code, List<int> SourceLineNumbers) TranspileCore(string spCode)
    {
        spCode = PrepareSource(spCode);
        spCode = FixFloatLiterals(spCode);

        _userDefinedFunctions.Clear();
        currentClass = null;
        currentInterface = null;
        currentTypeBraceDepth = -1;
        sourceBraceDepth = 0;

        // Class declarations may appear after their use, so collect them
        // before transpiling individual lines.
        var declaredClasses = GetDeclaredClasses(spCode);

        var results = new List<string>();
        var sourceLineNumbers = new List<int>();
        var sourceLines = spCode.Split('\n');

        // State for collecting class/interface blocks to move to the end
        bool inClassBlock = false;
        int classBlockDepth = 0;
        List<string>? currentClassBlockLines = null;
        List<int>? currentClassBlockSourceLines = null;
        var classBlocks = new List<(List<string> Lines, List<int> SourceLines)>();

        // Helper to add output to the correct list (top-level or class block)
        void AddOutput(string outputLine, int sourceLine)
        {
            if (inClassBlock)
            {
                currentClassBlockLines!.Add(outputLine);
                currentClassBlockSourceLines!.Add(sourceLine);
            }
            else
            {
                results.Add(outputLine);
                sourceLineNumbers.Add(sourceLine);
            }
        }

        // Helper to update class block depth and finalize block when closed
        void UpdateClassBlockDepth(string sourceLine)
        {
            if (!inClassBlock)
                return;

            classBlockDepth += CountBraces(sourceLine, '{') - CountBraces(sourceLine, '}');

            if (classBlockDepth <= 0)
            {
                // Finalize the block
                classBlocks.Add((currentClassBlockLines!, currentClassBlockSourceLines!));
                inClassBlock = false;
                currentClassBlockLines = null;
                currentClassBlockSourceLines = null;
            }
        }

        // SharpThon variable symbol table.
        // Each block gets its own scope.
        // Lookup walks from the innermost scope to outer scopes.
        var variableScopes = new Stack<HashSet<string>>();
        variableScopes.Push(
            new HashSet<string>(StringComparer.Ordinal)
        );

        for (
            int lineNumber = 0;
            lineNumber < sourceLines.Length;
            lineNumber++
        )
        {
            var line = sourceLines[lineNumber];

            var (code, comment) = SplitSharpThonComment(line);

            code = code.Trim();

            if (!inClassBlock && IsTypeDeclaration(code))
            {
                inClassBlock = true;
                currentClassBlockLines = new List<string>();
                currentClassBlockSourceLines = new List<int>();
                classBlockDepth = 0; // will be updated after processing this line
            }

            if (IsMultilineDictionaryStart(code))
            {
                code = CollectMultilineDictionary(
                    code,
                    sourceLines,
                    ref lineNumber
                );
            }
            else if (IsMultilineListStart(code))
            {
                code = CollectMultilineList(
                    code,
                    sourceLines,
                    ref lineNumber
                );
            }

            if (IsFunctionDeclaration(code))
            {
                var funcName = ExtractFunctionName(code);
                if (funcName != null)
                    _userDefinedFunctions.Add(funcName);
            }
            else
            {
                code = ApplyPythonStyleNameConversion(
                    code,
                    _userDefinedFunctions
                );
            }

            // Block-style async syntax
            //
            // go {
            //     command
            // }
            //
            // await go {
            //     command
            // }
            //
            // Old syntax is still handled by the normal parser:
            //
            // go command
            // await go command

            if (
                code == "go {" ||
                code == "await go {"
            )
            {
                bool isAwait =
                    code == "await go {";

                // For normal `go`, run in background.
                //
                // For `await go`, we intentionally block until the
                // Task completes because the current SharpThon
                // function model is synchronous.
                //
                // Later, when async def is supported, this can become:
                //
                // await Task.Run(() => {
                //
                string wrapper =
                    isAwait
                        ? "Task.Run(() => {"
                        : "Task.Run(() => {";

                AddOutput(wrapper, lineNumber + 1);
                UpdateClassBlockDepth(code); // opening brace of go block

                // The go block has its own variable scope.
                variableScopes.Push(
                    new HashSet<string>(
                        StringComparer.Ordinal
                    )
                );

                // We already consumed the opening `{`
                // from `go {`.
                int blockDepth = 1;

                lineNumber++;

                while (
                    lineNumber < sourceLines.Length
                )
                {
                    var blockLine = sourceLines[lineNumber];

                    var (blockCode, blockComment) =
                        SplitSharpThonComment(blockLine);

                    blockCode = blockCode.Trim();

                    // Empty line

                    if (string.IsNullOrEmpty(blockCode))
                    {
                        AddOutput("", lineNumber + 1);
                        UpdateClassBlockDepth(blockCode); // no braces, but still call

                        lineNumber++;
                        continue;
                    }

                    // Count braces BEFORE parsing the line.

                    int opens =
                        CountBraces(
                            blockCode,
                            '{'
                        );

                    UpdateCurrentType(blockCode);

                    if (IsFunctionDeclaration(blockCode))
                    {
                        var funcName = ExtractFunctionName(blockCode);
                        if (funcName != null)
                            _userDefinedFunctions.Add(funcName);
                    }
                    else
                    {
                        blockCode = ApplyPythonStyleNameConversion(
                            blockCode,
                            _userDefinedFunctions
                        );
                    }

                    int closes =
                        CountBraces(
                            blockCode,
                            '}'
                        );

                    // If we are at the outermost level and this line is
                    // the closing brace of the go block, stop here.
                    //
                    // Do NOT send this `}` to the SharpThon parser.

                    if (
                        blockDepth == 1 &&
                        blockCode == "}"
                    )
                    {
                        // Decrement class block depth for the closing brace
                        UpdateClassBlockDepth(blockCode);
                        break;
                    }

                    // Handle variable scopes before parsing.

                    CloseVariableScopes(
                        variableScopes,
                        blockCode
                    );

                    try
                    {
                        var innerResult =
                            SharpThonParser.Line.Parse(
                                blockCode
                            );

                        // Interface methods are declarations and must not have a body.
                        if (currentInterface != null && IsInterfaceMethodDeclaration(blockCode))
                        {
                            innerResult =
                                SharpThonParser.InterfaceMethodDecl.Parse(blockCode);
                        }

                        // Constructor handling

                        if (
                            currentClass != null &&
                            innerResult.StartsWith(
                                $"static void {currentClass}("
                            )
                        )
                        {
                            innerResult =
                                innerResult.Replace(
                                    $"static void {currentClass}(",
                                    $"public {currentClass}("
                                );
                        }

                        // Ensure class methods are public by default unless an explicit access modifier was provided.
                        if (
                            currentClass != null &&
                            currentInterface == null &&
                            IsFunctionDeclaration(blockCode) &&
                            !HasExplicitAccessModifier(blockCode)
                        )
                        {
                            var funcName = ExtractFunctionName(blockCode);
                            if (funcName != currentClass) // not a constructor
                            {
                                if (!Regex.IsMatch(innerResult.Trim(), @"^(public|private|protected|internal)\b"))
                                {
                                    innerResult = Regex.Replace(
                                        innerResult,
                                        @"^(?<indent>\s*)(?<prefix>(?:static\s+)?)",
                                        "${indent}public ${prefix}"
                                    );
                                }
                            }
                        }

                        // Function return type inference

                        if (
                            IsFunctionDeclaration(
                                blockCode
                            ) &&
                            currentInterface == null
                        )
                        {
                            var inferredType =
                                InferFunctionReturnType(
                                    sourceLines,
                                    lineNumber
                                );

                            innerResult =
                                ReplaceInferredFunctionReturnType(
                                    innerResult,
                                    inferredType
                                );
                        }

                        // Variable declaration / assignment handling

                        innerResult =
                            NormalizeVariableStatement(
                                blockCode,
                                innerResult,
                                variableScopes
                            );

                        // Object construction

                        innerResult =
                            AddImplicitConstructorKeyword(
                                blockCode,
                                innerResult,
                                declaredClasses
                            );

                        // Preserve inline comments

                        if (
                            !string.IsNullOrEmpty(
                                blockComment
                            )
                        )
                        {
                            innerResult +=
                                " " + blockComment;
                        }

                        AddOutput(innerResult, lineNumber + 1);
                        UpdateClassBlockDepth(blockCode);

                        // Open variable scopes after processing `{`.

                        OpenVariableScopes(
                            variableScopes,
                            blockCode,
                            innerResult
                        );
                    }
                    catch (Sprache.ParseException)
                    {
                        // Keep fallback behavior consistent
                        // with normal transpilation.
                        AddOutput(blockCode, lineNumber + 1);
                        UpdateClassBlockDepth(blockCode);

                        OpenVariableScopes(
                            variableScopes,
                            blockCode,
                            blockCode
                        );
                    }

                    // Update block depth AFTER processing the current line.

                    blockDepth += opens;
                    blockDepth -= closes;

                    lineNumber++;
                }

                // The closing `}` of the go block is currently sitting
                // at sourceLines[lineNumber].
                //
                // Consume it and emit the closing C# Task.Run syntax.

                if (
                    lineNumber < sourceLines.Length &&
                    sourceLines[lineNumber]
                        .Trim()
                        .StartsWith("}")
                )
                {
                    if (isAwait)
                    {
                        // Since current SharpThon functions are synchronous,
                        // wait for the Task to complete.
                        AddOutput(
                            "}).GetAwaiter().GetResult();",
                            lineNumber + 1
                        );
                    }
                    else
                    {
                        AddOutput("});", lineNumber + 1);
                    }
                }

                // Remove the scope belonging to the go block.
                if (variableScopes.Count > 1)
                {
                    variableScopes.Pop();
                }

                // `continue` is important because the closing `}`
                // of the go block must not go through the normal parser.
                continue;
            }

            // Normal empty line

            if (string.IsNullOrEmpty(code))
            {
                AddOutput("", lineNumber + 1);
                UpdateClassBlockDepth(code);

                continue;
            }

            // Close scopes before resolving the current line.
            //
            // This allows both:
            //
            // }
            //
            // and:
            //
            // } else {
            //
            // to behave correctly.

            CloseVariableScopes(
                variableScopes,
                code
            );

            // Class / interface declaration tracking
            UpdateCurrentType(code);

            try
            {
                var result =
                    SharpThonParser.Line.Parse(
                        code
                    );

                // Interface methods are declarations and must not have a body.
                if (currentInterface != null && IsInterfaceMethodDeclaration(code))
                {
                    result = SharpThonParser.InterfaceMethodDecl.Parse(code);
                }

                // Class methods are instance methods by default.
                // FunctionDecl emits `static` when no modifier is present
                // because top-level SharpThon functions are static. Inside a
                // class, remove that implicit static unless the source
                // explicitly requested `static def`.
                if (
                    currentClass != null &&
                    currentInterface == null &&
                    IsFunctionDeclaration(code) &&
                    !HasExplicitStaticModifier(code)
                )
                {
                    result = Regex.Replace(
                        result,
                        @"^static\s+",
                        ""
                    );
                }

                // Constructor handling

                if (
                    currentClass != null &&
                    result.StartsWith(
                        $"static void {currentClass}("
                    )
                )
                {
                    result =
                        result.Replace(
                            $"static void {currentClass}(",
                            $"public {currentClass}("
                        );
                }

                // Ensure class methods are public by default unless an explicit access modifier was provided.
                if (
                    currentClass != null &&
                    currentInterface == null &&
                    IsFunctionDeclaration(code) &&
                    !HasExplicitAccessModifier(code)
                )
                {
                    var funcName = ExtractFunctionName(code);
                    if (funcName != currentClass) // not a constructor
                    {
                        if (!Regex.IsMatch(result.Trim(), @"^(public|private|protected|internal)\b"))
                        {
                            result = Regex.Replace(
                                result,
                                @"^(?<indent>\s*)(?<prefix>(?:static\s+)?)",
                                "${indent}public ${prefix}"
                            );
                        }
                    }
                }

                // Function return type inference

                if (
                    IsFunctionDeclaration(code) &&
                    currentInterface == null
                )
                {
                    var inferredType =
                        InferFunctionReturnType(
                            sourceLines,
                            lineNumber
                        );

                    result =
                        ReplaceInferredFunctionReturnType(
                            result,
                            inferredType
                        );
                }

                // Variable declarations / assignments

                result =
                    NormalizeVariableStatement(
                        code,
                        result,
                        variableScopes
                    );

                // Object construction

                result =
                    AddImplicitConstructorKeyword(
                        code,
                        result,
                        declaredClasses
                    );

                // Preserve inline comments

                if (
                    !string.IsNullOrEmpty(comment)
                )
                {
                    result +=
                        " " + comment;
                }

                AddOutput(result, lineNumber + 1);
                UpdateClassBlockDepth(code);

                // Open a scope after processing the line containing `{`.

                OpenVariableScopes(
                    variableScopes,
                    code,
                    result
                );
            }
            catch (Sprache.ParseException)
            {
                // Keep the source line as fallback.
                AddOutput(code, lineNumber + 1);
                UpdateClassBlockDepth(code);

                // Keep scope tracking alive even when the parser
                // doesn't understand this particular line yet.
                OpenVariableScopes(
                    variableScopes,
                    code,
                    code
                );
            }

            UpdateSourceBraceDepth(code);
        }

        // Combine top-level results with collected class blocks
        var combinedResults = new List<string>(results);
        var combinedSourceLineNumbers = new List<int>(sourceLineNumbers);

        foreach (var block in classBlocks)
        {
            combinedResults.AddRange(block.Lines);
            combinedSourceLineNumbers.AddRange(block.SourceLines);
        }

        // Format final C# output using combined lists
        var formatted =
            FormatResults(
                combinedResults,
                combinedSourceLineNumbers
            );

        return (
            formatted.Code,
            formatted.SourceLineNumbers
        );
    }

    private static string NormalizeVariableStatement(
        string sourceCode,
        string transpiledCode,
        Stack<HashSet<string>> scopes)
    {
        var name = GetAssignmentName(sourceCode);

        // Assignments inside a property setter must target the backing field
        // (or another existing outer symbol), not introduce a local `var`.
        // C# supplies the setter's `value` parameter implicitly.
        if (IsPropertySetterAssignment(sourceCode) && name != null)
        {
            return transpiledCode.Replace(
                $"var {name} =",
                $"{name} =",
                StringComparison.Ordinal
            );
        }

        // Explicitly typed declarations always remain declarations:
        //   i: int = 0
        //   float value = 1.5
        if (HasExplicitType(sourceCode))
        {
            if (name != null)
                scopes.Peek().Add(name);

            return transpiledCode;
        }

        if (name == null)
            return transpiledCode;

        if (IsKnownVariable(scopes, name))
        {
            // Existing symbol => assignment, never `var` again.
            return transpiledCode.Replace(
                $"var {name} =",
                $"{name} =",
                StringComparison.Ordinal
            );
        }

        // New symbol => declaration in the current scope.
        scopes.Peek().Add(name);
        return transpiledCode;
    }

    private void UpdateCurrentType(string code)
    {
        var interfaceMatch = Regex.Match(
            code,
            @"^interface\s+([A-Za-z_][A-Za-z0-9_]*)\b"
        );

        if (interfaceMatch.Success)
        {
            currentInterface = interfaceMatch.Groups[1].Value;
            currentClass = null;
            currentTypeBraceDepth = sourceBraceDepth +
                CountBraces(code, '{') - CountBraces(code, '}');
            return;
        }

        var classMatch = Regex.Match(
            code,
            @"^class\s+([A-Za-z_][A-Za-z0-9_]*)\b"
        );

        if (classMatch.Success)
        {
            currentClass = classMatch.Groups[1].Value;
            currentInterface = null;
            currentTypeBraceDepth = sourceBraceDepth +
                CountBraces(code, '{') - CountBraces(code, '}');
        }
    }

    private void UpdateSourceBraceDepth(string code)
    {
        sourceBraceDepth += CountBraces(code, '{');
        sourceBraceDepth -= CountBraces(code, '}');

        if (currentTypeBraceDepth >= 0 &&
            sourceBraceDepth < currentTypeBraceDepth)
        {
            currentClass = null;
            currentInterface = null;
            currentTypeBraceDepth = -1;
        }
    }

    private static bool IsInterfaceMethodDeclaration(string code)
    {
        return Regex.IsMatch(
            code,
            @"^(?:(?:public|private|protected)\s+)?def\s+[A-Za-z_][A-Za-z0-9_]*\s*\("
        );
    }

    private static bool HasExplicitStaticModifier(string code)
    {
        return Regex.IsMatch(
            code,
            @"^(?:(?:public|private|protected)\s+)*static\s+def\b"
        );
    }

    private static HashSet<string> GetDeclaredClasses(string source)
    {
        return Regex.Matches(
                source,
                @"(?m)^\s*class\s+([A-Za-z_][A-Za-z0-9_]*)\b"
            )
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string AddImplicitConstructorKeyword(
        string sourceCode,
        string transpiledCode,
        HashSet<string> declaredClasses)
    {
        var match = Regex.Match(
            sourceCode,
            @"^(?:(?:public|private|protected)\s+)?[A-Za-z_][A-Za-z0-9_]*(?:\s*:\s*[A-Za-z_][A-Za-z0-9_]*)?\s*=\s*([A-Za-z_][A-Za-z0-9_]*)\s*\("
        );

        if (!match.Success || !declaredClasses.Contains(match.Groups[1].Value))
            return transpiledCode;

        var constructorCall = match.Groups[1].Value;

        return Regex.Replace(
            transpiledCode,
            $@"(=\s*)(?!new\s+){Regex.Escape(constructorCall)}\s*\(",
            $"$1new {constructorCall}(",
            RegexOptions.None,
            TimeSpan.FromSeconds(1)
        );
    }

    private static bool IsPropertySetterAssignment(string code)
    {
        return Regex.IsMatch(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*=\s*value\s*;?$"
        );
    }

    private static bool HasExplicitType(string code)
    {
        return Regex.IsMatch(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*:\s*[A-Za-z_][A-Za-z0-9_]*\s*="
        ) || Regex.IsMatch(
            code,
            @"^(?:public|private|protected|const|readonly)\s+(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*)\s*="
        ) || Regex.IsMatch(
            code,
            @"^(?:public|private|protected)\s+(?:const|readonly)\s+[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*[A-Za-z_][A-Za-z0-9_]*)?\s*="
        ) || Regex.IsMatch(
            code,
            @"^(?:int|string|str|bool|float|double|long|object|Any)\s+[A-Za-z_][A-Za-z0-9_]*\s*="
        );
    }

    private static string? GetAssignmentName(string code)
    {
        var match = Regex.Match(
            code,
            @"^(?:(?:public|private|protected)\s+(?:const|readonly)\s+|(?:public|private|protected|const|readonly)\s+)?(?:[A-Za-z_][A-Za-z0-9_]*\s*:\s*[A-Za-z_][A-Za-z0-9_]*\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*="
        );

        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool IsKnownVariable(
        Stack<HashSet<string>> scopes,
        string name)
    {
        foreach (var scope in scopes)
        {
            if (scope.Contains(name))
                return true;
        }

        return false;
    }

    private static void CloseVariableScopes(
        Stack<HashSet<string>> scopes,
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
        Stack<HashSet<string>> scopes,
        string sourceCode,
        string transpiledCode)
    {
        var openingBraces = CountBraces(sourceCode, '{');
        var closingBraces = CountBraces(sourceCode, '}');
        var netOpeningScopes = Math.Max(0, openingBraces - closingBraces);

        for (int i = 0; i < netOpeningScopes; i++)
            scopes.Push(new HashSet<string>(StringComparer.Ordinal));

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
            scopes.Peek().Add(forMatch.Groups[1].Value);

        // catch/except variable is also local to the new scope.
        var catchMatch = Regex.Match(
            sourceCode,
            @"^(?:catch|except)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s+as\s+([A-Za-z_][A-Za-z0-9_]*)"
        );

        if (catchMatch.Success)
            scopes.Peek().Add(catchMatch.Groups[1].Value);
    }

    private static void RegisterFunctionParameters(
        string code,
        HashSet<string> scope)
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
                scope.Add(nameMatch.Groups[1].Value);
            else
            {
                var plainName = Regex.Match(
                    text,
                    @"^[A-Za-z_][A-Za-z0-9_]*$"
                );

                if (plainName.Success)
                    scope.Add(plainName.Value);
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

    private static string? ExtractFunctionName(string code)
    {
        var match = Regex.Match(
            code,
            @"\bdef\s+([A-Za-z_][A-Za-z0-9_]*)"
        );

        return match.Success
            ? match.Groups[1].Value
            : null;
    }

    private static string ApplyPythonStyleNameConversion(
        string code,
        HashSet<string> userDefinedFunctions)
    {
        var regex = new Regex(
            @"(\.[ \t]*)([A-Za-z_][A-Za-z0-9_]*)([ \t]*\()"
        );

        return regex.Replace(code, match =>
        {
            if (IsInsideString(code, match.Index))
                return match.Value;

            var method = match.Groups[2].Value;

            if (!method.Contains('_') ||
                userDefinedFunctions.Contains(method))
            {
                return match.Value;
            }

            return
                match.Groups[1].Value +
                ToPascalCase(method) +
                match.Groups[3].Value;
        });
    }

    private static bool IsInsideString(string text, int index)
    {
        bool inString = false;
        bool escaped = false;

        for (int i = 0; i < index; i++)
        {
            char c = text[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
                inString = !inString;
        }

        return inString;
    }

    // SOURCE PREPARATION

    private static string PrepareSource(string spCode)
    {
        // A property with a direct return is a get-only expression-bodied
        // property. Convert it before line-by-line parsing.
        //
        // Name -> str {             public string Name => name;
        //     return name       =>
        // }
        spCode = Regex.Replace(
            spCode,
            @"(?m)^(\s*)(?:(public|private|protected)\s+)?(?:(static)\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*->\s*(int|str|bool|float|double|long|object|Any)\s*\{\s*\r?\n\s*return\s+([^\r\n]+?)\s*\r?\n\s*\}",
            match =>
            {
                var type = match.Groups[5].Value switch
                {
                    "str" => "string",
                    "Any" => "object",
                    var value => value
                };

                var access = match.Groups[2].Success
                    ? match.Groups[2].Value
                    : "public";
                var staticModifier = match.Groups[3].Success
                    ? "static "
                    : "";

                return $"{match.Groups[1].Value}{access} {staticModifier}" +
                    $"{type} {match.Groups[4].Value} => " +
                    match.Groups[6].Value.Trim();
            }
        );

        // The parser emits the opening brace for block declarations itself.
        // Normalize C-style blocks whose `{` is written on the next line so
        // that the standalone brace is not emitted a second time.
        //
        //   def add(x, y)       def add(x, y) {
        //   {                =>
        spCode = Regex.Replace(
            spCode,
            @"(?m)^(\s*(?:(?:public|private|protected|static)\s+)*def\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^\r\n]*\)(?:\s*->\s*[A-Za-z_][A-Za-z0-9_]*)?)\s*\r?\n\s*\{\s*$",
            "$1 {"
        );

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

        spCode = CollapseMultilineDictionaries(spCode);

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
        var index = line.IndexOf("//");

        if (index >= 0)
            return line[..index];

        return line;
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

    private static (string Code, string Comment) SplitSharpThonComment(
        string line)
    {
        bool inString = false;
        char stringQuote = '\0';
        bool escaped = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (inString)
            {
                if (c == stringQuote)
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"' || c == '\'')
            {
                inString = true;
                stringQuote = c;
                continue;
            }

            // Python/SharpThon style comment
            if (c == '#')
            {
                return (
                    line[..i],
                    "//" + line[i..]
                );
            }

            // C# style comment
            if (c == '/' &&
                i + 1 < line.Length &&
                line[i + 1] == '/')
            {
                return (
                    line[..i],
                    line[i..]
                );
            }
        }

        return (line, "");
    }

    private static string CollapseMultilineDictionaries(string source)
    {
        var lines = source.Split('\n');
        var result = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Dictionary assignment:
            // data = {
            // data: dict[str, int] = {
            bool startsDictionary =
                Regex.IsMatch(
                    line,
                    @"^\s*[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*dict\s*\[[^\]]+\])?\s*=\s*\{\s*$"
                );

            if (!startsDictionary)
            {
                result.Add(line);
                continue;
            }

            var combined = line.TrimEnd();
            int depth = 1;

            while (depth > 0 && i + 1 < lines.Length)
            {
                i++;

                var nextLine = lines[i].Trim();

                if (nextLine.Length == 0)
                    continue;

                combined += " " + nextLine;

                depth += CountBraces(nextLine, '{');
                depth -= CountBraces(nextLine, '}');
            }

            result.Add(combined);
        }

        return string.Join("\n", result);
    }

    private static bool IsMultilineListStart(string code)
    {
        return Regex.IsMatch(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*[^=]+)?\s*=\s*\[\s*$"
        );
    }

    private static string CollectMultilineList(
        string firstLine,
        string[] sourceLines,
        ref int lineNumber)
    {
        var lines = new List<string>
        {
            firstLine.Trim()
        };

        var bracketDepth = CountBraces(firstLine, '[')
                         - CountBraces(firstLine, ']');

        while (
            bracketDepth > 0 &&
            lineNumber + 1 < sourceLines.Length
        )
        {
            lineNumber++;

            var nextLine = sourceLines[lineNumber];
            lines.Add(nextLine.Trim());

            bracketDepth += CountBraces(nextLine, '[');
            bracketDepth -= CountBraces(nextLine, ']');
        }

        return string.Join(" ", lines);
    }

    private static bool IsMultilineDictionaryStart(string code)
    {
        return Regex.IsMatch(
            code,
            @"^[A-Za-z_][A-Za-z0-9_]*\s*(?::\s*[^=]+)?\s*=\s*\{\s*$"
        );
    }
    
    private static string CollectMultilineDictionary(
        string firstLine,
        string[] sourceLines,
        ref int lineNumber)
    {
        var lines = new List<string>
        {
            firstLine.Trim()
        };

        var depth = CountBraces(firstLine, '{')
                - CountBraces(firstLine, '}');

        while (
            depth > 0 &&
            lineNumber + 1 < sourceLines.Length
        )
        {
            lineNumber++;

            var nextLine = sourceLines[lineNumber];

            lines.Add(nextLine.Trim());

            depth += CountBraces(nextLine, '{');
            depth -= CountBraces(nextLine, '}');
        }

        return string.Join(" ", lines);
    }

    private static bool HasExplicitAccessModifier(string code)
    {
        return Regex.IsMatch(
            code,
            @"^(?:(?:public|private|protected)\s+)(?:static\s+)?def\b"
        );
    }

    private static bool IsTypeDeclaration(string code)
    {
        return Regex.IsMatch(
            code,
            @"^(?:(?:public|private|protected)\s+)?(class|interface)\s+[A-Za-z_][A-Za-z0-9_]*"
        );
    }
}