using System.Diagnostics;

namespace SharpThon.Tests;

internal static class CliTestSupport
{
    public static string SharptonDirectory { get; } = FindSharptonDirectory();

    public static string TestsDirectory =>
        Path.GetFullPath(Path.Combine(SharptonDirectory, "..", "tests"));

    public static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "sharpthon-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string RunCli(string sourceFile)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{Path.Combine(SharptonDirectory, "Sharpton.Cli", "Sharpton.Cli.csproj")}\" -- \"{sourceFile}\"",
            WorkingDirectory = SharptonDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process == null)
            throw new InvalidOperationException("Could not start the SharpThon CLI.");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return output + error;
    }

    public static void DeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // A failed test cleanup must not hide the assertion failure.
        }
    }

    private static string FindSharptonDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sharpton.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate sharpton_cs.");
    }
}
