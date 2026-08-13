using Xunit;

namespace SharpThon.Tests;

public sealed class PackageImportsTests
{
    [Fact]
    public void PackageImport_UsesPackageIndex()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var packageDirectory = Path.Combine(temporaryDirectory, "my_package");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(
                Path.Combine(packageDirectory, "index.spy"),
                "def greet() {\n    Write(\"Package import works\")\n}\n");
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "main.spy"),
                "import my_package\ngreet()\n");

            var output = CliTestSupport.RunCli(
                Path.Combine(temporaryDirectory, "main.spy"));

            Assert.Contains("Package import works", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void PackageWithoutIndex_ShowsClearImportError()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            Directory.CreateDirectory(
                Path.Combine(temporaryDirectory, "my_package"));
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "main.spy"),
                "import my_package\n");

            var output = CliTestSupport.RunCli(
                Path.Combine(temporaryDirectory, "main.spy"));

            Assert.Contains(
                "Package 'my_package' does not have an index.spy file",
                output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void DottedImport_UsesModuleFileInsidePackageDirectory()
    {
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var packageDirectory = Path.Combine(temporaryDirectory, "my_package");
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(
                Path.Combine(packageDirectory, "foo.spy"),
                "def greet() {\n    Write(\"Nested module import works\")\n}\n");
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "main.spy"),
                "import my_package.foo\ngreet()\n");

            var output = CliTestSupport.RunCli(
                Path.Combine(temporaryDirectory, "main.spy"));

            Assert.Contains("Nested module import works", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}
