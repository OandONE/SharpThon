using Sharpton.Core;

if (args.Length < 1)
{
    Console.WriteLine("Usage: sharpton <file.spy>");
    return 1;
}

var filepath = args[0];

if (!File.Exists(filepath))
{
    Console.WriteLine($"File not found: {filepath}");
    return 1;
}

var version = File.ReadAllText("version.txt").Trim();
Console.WriteLine($"SharpThon {version}");

var transpiler = new Transpiler();

(string Code, List<int> SourceLineNumbers) transpileResult;

try
{
    transpileResult = transpiler.TranspileFileWithMapping(filepath);
}
catch (SharpThonCircularImportException exception)
{
    Console.WriteLine();
    Console.WriteLine("=== SharpThon Circular Import Error ===");
    Console.WriteLine();
    Console.WriteLine($"File: {exception.SourceFile}");
    Console.WriteLine($"Line: {exception.LineNumber}");
    Console.WriteLine();
    Console.WriteLine("Circular import detected:");
    Console.WriteLine(string.Join(" -> ", exception.Cycle));
    Console.WriteLine("===============================");
    Console.WriteLine();
    return 1;
}
catch (SharpThonImportException exception)
{
    Console.WriteLine();
    Console.WriteLine("=== SharpThon Import Error ===");
    Console.WriteLine();
    Console.WriteLine($"File: {exception.SourceFile}");
    Console.WriteLine($"Line: {exception.LineNumber}");
    Console.WriteLine();
    Console.WriteLine(exception.Message);
    Console.WriteLine();
    Console.WriteLine("===============================");
    Console.WriteLine();
    return 1;
}

var csCode = transpileResult.Code;
var sourceLineNumbers = transpileResult.SourceLineNumbers;

var sourceLines = File.ReadAllLines(filepath);

var outputDir = Path.Combine(
    Path.GetDirectoryName(filepath)!,
    "__sharpthon__"
);

Directory.CreateDirectory(outputDir);

var csFile = Path.Combine(
    outputDir,
    Path.GetFileNameWithoutExtension(filepath) + ".cs"
);

File.WriteAllText(csFile, csCode);

Console.WriteLine($"✅ Generated: {csFile}");
Console.WriteLine("=== C# Code ===");
Console.WriteLine(csCode);
Console.WriteLine("=== OutPut ===");

// Create a temporary .NET project
var tempDir = Path.Combine(
    Path.GetTempPath(),
    "sharpthon_" + Guid.NewGuid().ToString("N")
);

Directory.CreateDirectory(tempDir);

var tempProject = Path.Combine(tempDir, "SharpThonTemp.csproj");
var tempProgram = Path.Combine(tempDir, "Program.cs");

var projectFile = """
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
""";

File.WriteAllText(tempProject, projectFile);
File.WriteAllText(tempProgram, csCode);

try
{
    var process = System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{tempProject}\" --no-launch-profile",
            WorkingDirectory = tempDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }
    );

    if (process == null)
    {
        Console.WriteLine("Failed to start .NET.");
        return 1;
    }

    var output = process.StandardOutput.ReadToEnd();
    var errors = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (!string.IsNullOrWhiteSpace(output))
    {
        var errorLines = output.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries
        );

        foreach (var errorLine in errorLines)
        {
            Console.WriteLine(errorLine);

            var match = System.Text.RegularExpressions.Regex.Match(
                errorLine,
                @"Program\.cs\((\d+),(\d+)\): error (.+)"
            );

            if (!match.Success)
                continue;

            int csLine = int.Parse(match.Groups[1].Value);

            var generatedLines = csCode.Split('\n');

            string errorMessage = match.Groups[3].Value;

            // Remove project/file path from the C# error
            int bracketIndex = errorMessage.IndexOf(" [");
            if (bracketIndex >= 0)
                errorMessage = errorMessage[..bracketIndex];

            // Extract error code
            string errorCode = "";

            var codeMatch = System.Text.RegularExpressions.Regex.Match(
                errorMessage,
                @"^(CS\d+):\s*(.*)$"
            );

            if (codeMatch.Success)
            {
                errorCode = codeMatch.Groups[1].Value;
                errorMessage = codeMatch.Groups[2].Value;
            }

            if (csLine >= 1 && csLine <= sourceLineNumbers.Count)
            {
                int sharpThonLine = sourceLineNumbers[csLine - 1];

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine();
                Console.WriteLine("=== SharpThon Error ===");
                Console.WriteLine();
                Console.WriteLine($"File: {filepath}");
                Console.WriteLine($"Line: {sharpThonLine}");

                PrintSourceLink(
                    filepath,
                    sharpThonLine,
                    int.Parse(match.Groups[2].Value)
                );

                if (sharpThonLine >= 1 && sharpThonLine <= sourceLines.Length)
                {
                    Console.WriteLine();
                    Console.WriteLine($"  {sharpThonLine} | {sourceLines[sharpThonLine - 1]}");
                }

                Console.WriteLine();
                Console.WriteLine(errorMessage);

                if (!string.IsNullOrEmpty(errorCode))
                    Console.WriteLine($"C# Error: {errorCode}");

                Console.WriteLine();
                Console.WriteLine("=======================");
                Console.WriteLine();
                Console.ResetColor();
            }
        }
    }

    if (!string.IsNullOrWhiteSpace(errors))
    {
        PrintRuntimeError(
            errors,
            filepath,
            sourceLineNumbers,
            sourceLines
        );
    }

    return process.ExitCode;
}
finally
{
    try
    {
        Directory.Delete(tempDir, true);
    }
    catch
    {
        // Ignore cleanup errors.
    }
}

static void PrintRuntimeError(
    string standardError,
    string sourceFile,
    IReadOnlyList<int> sourceLineNumbers,
    IReadOnlyList<string> sourceLines)
{
    var exceptionMatch =
        System.Text.RegularExpressions.Regex.Match(
            standardError,
            @"Unhandled exception\.\s+(?<type>[\w.]+):\s*(?<message>[^\r\n]*)"
        );

    if (!exceptionMatch.Success)
    {
        Console.Error.WriteLine(standardError.TrimEnd());
        return;
    }

    var generatedLineMatch =
        System.Text.RegularExpressions.Regex.Match(
            standardError,
            @"Program\.cs:line\s+(?<line>\d+)"
        );

    int? sharpThonLine = null;

    if (generatedLineMatch.Success &&
        int.TryParse(
            generatedLineMatch.Groups["line"].Value,
            out var generatedLine) &&
        generatedLine >= 1 &&
        generatedLine <= sourceLineNumbers.Count)
    {
        sharpThonLine = sourceLineNumbers[generatedLine - 1];
    }

    var exceptionType = exceptionMatch.Groups["type"].Value;
    var exceptionMessage = exceptionMatch.Groups["message"].Value.Trim();

    const string orange = "\x1b[1;38;2;255;165;0m";
    const string reset = "\x1b[0m";

    Console.WriteLine();
    Console.WriteLine($"{orange}=== SharpThon Runtime Error ==={reset}");
    Console.WriteLine();

    Console.WriteLine($"{orange}File: {sourceFile}{reset}");

    if (sharpThonLine.HasValue)
    {
        Console.WriteLine(
            $"{orange}Line: {sharpThonLine.Value}{reset}"
        );

        var absolutePath = Path.GetFullPath(sourceFile);

        var vscodeUri =
            $"vscode://file{absolutePath}:{sharpThonLine.Value}:1";

        var link =
            $"\x1b]8;;{vscodeUri}\x1b\\" +
            $"🔗 Open source line {sharpThonLine.Value}" +
            $"\x1b]8;;\x1b\\";

        Console.WriteLine(link);

        if (
            sharpThonLine.Value >= 1 &&
            sharpThonLine.Value <= sourceLines.Count
        )
        {
            Console.WriteLine();
            Console.WriteLine(
                $"{orange}  {sharpThonLine.Value} | " +
                $"{sourceLines[sharpThonLine.Value - 1]}{reset}"
            );
        }
    }
    else
    {
        Console.WriteLine($"{orange}Line: Unknown{reset}");
    }

    Console.WriteLine();

    Console.WriteLine(
        $"{orange}" +
        (string.IsNullOrEmpty(exceptionMessage)
            ? exceptionType
            : $"{exceptionType}: {exceptionMessage}") +
        $"{reset}"
    );

    Console.WriteLine();
    Console.WriteLine($"{orange}==============================={reset}");
    Console.WriteLine();
}

static void PrintSourceLink(
    string sourceFile,
    int line,
    int column = 1)
{
    var absolutePath = Path.GetFullPath(sourceFile);

    var uri =
        $"vscode://file/{Uri.EscapeDataString(absolutePath)}:{line}:{column}";

    Console.WriteLine(
        $"\u001b]8;;{uri}\u0007" +
        $"🔗 Open source line {line}" +
        $"\u001b]8;;\u0007"
    );
}
