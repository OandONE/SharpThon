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

var spCode = File.ReadAllText(filepath);
var transpiler = new Transpiler();
var csCode = transpiler.Transpile(spCode);

Console.WriteLine("=== C# Code ===");
Console.WriteLine(csCode);

// ذخیره و کامپایل
var tempFile = Path.GetTempFileName() + ".cs";
File.WriteAllText(tempFile, csCode);

try
{
    var process = System.Diagnostics.Process.Start("dotnet", $"script \"{tempFile}\"");
    if (process == null)
    {
        Console.WriteLine("dotnet-script not found. Install: dotnet tool install -g dotnet-script");
        return 1;
    }
    process.WaitForExit();
    return process.ExitCode;
}
finally
{
    File.Delete(tempFile);
}
