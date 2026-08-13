using Xunit;

namespace SharpThon.Tests;

public sealed class CircularImportTests
{
    [Fact]
    public void CircularModules_DisplaySharpThonCircularImportError()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            File.Copy(
                Path.Combine(CliTestSupport.TestsDirectory, "CircularA.spy"),
                Path.Combine(temporaryDirectory, "CircularA.spy"));
            File.Copy(
                Path.Combine(CliTestSupport.TestsDirectory, "CircularB.spy"),
                Path.Combine(temporaryDirectory, "CircularB.spy"));

            // Imports are lowercase, so copy names must match the resolver's
            // case-sensitive module-file convention on Linux.
            File.Copy(
                Path.Combine(CliTestSupport.TestsDirectory, "CircularA.spy"),
                Path.Combine(temporaryDirectory, "circular_a.spy"));
            File.Copy(
                Path.Combine(CliTestSupport.TestsDirectory, "CircularB.spy"),
                Path.Combine(temporaryDirectory, "circular_b.spy"));

            var output = CliTestSupport.RunCli(
                Path.Combine(temporaryDirectory, "circular_a.spy"));

            Assert.Contains("SharpThon Circular Import Error", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}
