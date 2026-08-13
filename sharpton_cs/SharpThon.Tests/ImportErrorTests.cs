using Xunit;

namespace SharpThon.Tests;

public sealed class ImportErrorTests
{
    [Fact]
    public void MissingModule_ShowsSharpThonImportError()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var sourceFile = Path.Combine(temporaryDirectory, "MissingImport.spy");
            File.WriteAllText(sourceFile, "import testnist\n");

            var output = CliTestSupport.RunCli(sourceFile);

            Assert.Contains("SharpThon Import Error", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}
