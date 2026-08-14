namespace Sharpton.Core;

public class SharpThonImportException : Exception
{
    public string SourceFile { get; }
    public int LineNumber { get; }

    public SharpThonImportException(
        string message,
        string sourceFile,
        int lineNumber)
        : base(message)
    {
        SourceFile = sourceFile;
        LineNumber = lineNumber;
    }

    public SharpThonImportException(
        string message,
        string sourceFile,
        int lineNumber,
        Exception innerException)
        : base(message, innerException)
    {
        SourceFile = sourceFile;
        LineNumber = lineNumber;
    }
}

public class SharpThonCircularImportException
    : SharpThonImportException
{
    public string Cycle { get; }

    public SharpThonCircularImportException(
        string message,
        string sourceFile,
        int lineNumber,
        string cycle)
        : base(message, sourceFile, lineNumber)
    {
        Cycle = cycle;
    }
}