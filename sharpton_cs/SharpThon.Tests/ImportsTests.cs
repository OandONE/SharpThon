using Xunit;

namespace SharpThon.Tests;

public sealed class ImportsTests
{
    [Fact]
    public void TestImports_UsesImportedModule()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var mainFile = Path.Combine(temporaryDirectory, "TestImports.spy");
            File.Copy(
                Path.Combine(CliTestSupport.TestsDirectory, "TestImportModule.spy"),
                Path.Combine(temporaryDirectory, "TestImportModule.spy"));
            File.WriteAllText(
                mainFile,
                File.ReadAllText(Path.Combine(CliTestSupport.TestsDirectory, "TestImports.spy")) + "\nrun()\n");

            var output = CliTestSupport.RunCli(mainFile);

            Assert.Contains("Import works", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}
