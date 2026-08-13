using Xunit;

namespace SharpThon.Tests;

public sealed class VariablesTests
{
    [Fact]
    public void TestVariables_Produces30()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var sourceFile = Path.Combine(temporaryDirectory, "TestVariables.spy");
            File.WriteAllText(
                sourceFile,
                File.ReadAllText(Path.Combine(CliTestSupport.TestsDirectory, "TestVariables.spy")) + "\nrun()\n");

            var output = CliTestSupport.RunCli(sourceFile);

            Assert.Contains("30", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}
