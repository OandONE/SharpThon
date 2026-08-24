using Xunit;
using Sharpton.Core;

namespace SharpThon.Tests;

public sealed class PythonLikeNamesTests
{
    [Fact]
    public void ConvertSnakeCaseMethodToPascalCase_DirectTranspile()
    {
        // Arrange
        var source = """
            using System
            console.write_line("Hello")
            """;

        // Act
        var transpiled = new Transpiler().Transpile(source);

        // Assert
        Assert.Contains("Console.WriteLine(\"Hello\")", transpiled);
    }

    [Fact]
    public void UserDefinedFunction_NotConverted_DirectTranspile()
    {
        // Arrange
        var source = """
            def my_function() {
                return 42
            }

            my_function()
            """;

        // Act
        var transpiled = new Transpiler().Transpile(source);

        // Assert
        Assert.Contains("my_function()", transpiled);
        Assert.DoesNotContain("MyFunction()", transpiled);
    }

    [Fact]
    public void MethodInsideString_NotConverted_DirectTranspile()
    {
        // Arrange
        var source = """
            text = "Call console.write_line()"
            """;

        // Act
        var transpiled = new Transpiler().Transpile(source);

        // Assert
        Assert.Contains("\"Call console.write_line()\"", transpiled);
        Assert.DoesNotContain("Console.WriteLine", transpiled);
    }

    [Fact]
    public void MethodInsideComment_NotConverted_DirectTranspile()
    {
        // Arrange
        var source = """
            # This is console.write_line()
            x = 1
            """;

        // Act
        var transpiled = new Transpiler().Transpile(source);

        // Assert
        Assert.Contains("// This is console.write_line()", transpiled);
        Assert.DoesNotContain("Console.WriteLine", transpiled);
    }

    [Fact]
    public void GoBlock_MethodConverted_DirectTranspile()
    {
        // Arrange
        var source = """
            go {
                do_something()
            }
            """;

        // Act
        var transpiled = new Transpiler().Transpile(source);

        // Assert
        Assert.Contains("DoSomething()", transpiled);
    }

    [Fact]
    public void RunCli_ConsoleWriteLine_ProducesOutput()
    {
        // Arrange
        var temporaryDirectory = CliTestSupport.CreateTemporaryDirectory();

        try
        {
            var sourceFile = Path.Combine(temporaryDirectory, "PythonLikeNames.spy");
            File.WriteAllText(
                sourceFile,
                File.ReadAllText(Path.Combine(CliTestSupport.TestsDirectory, "PythonLikeNames.spy")));

            // Act
            var output = CliTestSupport.RunCli(sourceFile);

            // Assert
            Assert.Contains("Result is 42", output);
            Assert.Contains("Hello SharpThon", output);
            Assert.Contains("Keep this: console.write_line()", output);
            Assert.Contains("Sum is 7", output);
        }
        finally
        {
            CliTestSupport.DeleteDirectory(temporaryDirectory);
        }
    }
}