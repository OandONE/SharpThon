using Sprache;

namespace Sharpton.Core;

public static class SharpThonParser
{
    // ── base tokens ──
    public static readonly Parser<string> Identifier = Parse.Letter.AtLeastOnce().Text().Token();
    public static readonly Parser<string> Number = Parse.Digit.AtLeastOnce().Text().Token();
    public static readonly Parser<string> String = Parse.Char('"').Until(Parse.Char('"')).Text().Token();
    
    // ── types ─ـ
    public static readonly Parser<string> Type = Parse.String("int").Or(Parse.String("str")).Or(Parse.String("bool")).Or(Parse.String("float")).Or(Parse.String("object")).Text().Token();

    // ── vars ─ـ
    public static readonly Parser<string> VariableDecl =
        from name in Identifier
        from type in (from colon in Parse.Char(':').Token()
                      from t in Type
                      select t).Optional()
        from eq in Parse.Char('=').Token()
        from value in Parse.AnyChar.Until(Parse.Char(';').Optional()).Text()
        select type.IsDefined
            ? $"{type.Get()} {name} = {value.Trim()};"
            : $"var {name} = {value.Trim()};";

    // ── Write ─ـ
    public static readonly Parser<string> WriteCall =
        from write in Parse.String("Write")
        from openParen in Parse.Char('(').Token()
        from args in Parse.AnyChar.Until(Parse.Char(')')).Text()
        select $"Console.WriteLine({args});";

    // ── Line ─ـ
    public static readonly Parser<string> Line = VariableDecl.Or(WriteCall);

    // ── Program ─ـ
    public static readonly Parser<string> Program = Line.DelimitedBy(Parse.LineEnd).Select(lines => string.Join("\n", lines));

    // ── method helper for Transpile ─ـ
    public static string ParseCode(string spCode)
    {
        try
        {
            return Program.Parse(spCode);
        }
        catch (ParseException ex)
        {
            return $"// Parse error: {ex.Message}";
        }
    }
}
