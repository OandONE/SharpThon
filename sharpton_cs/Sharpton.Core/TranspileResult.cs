namespace Sharpton.Core;

public class TranspileResult
{
    public string Code { get; }
    public List<int> SourceLineNumbers { get; }

    public TranspileResult(
        string code,
        List<int> sourceLineNumbers)
    {
        Code = code;
        SourceLineNumbers = sourceLineNumbers;
    }
}