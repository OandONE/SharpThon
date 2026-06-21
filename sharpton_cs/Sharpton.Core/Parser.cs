using Sprache;

namespace Sharpton.Core;

public static class SharpThonParser
{
    // ── Base Tokens ──
    public static readonly Parser<string> Identifier = 
        Parse.Letter.AtLeastOnce().Text().Token();
    
    public static readonly Parser<string> String = 
        Parse.Char('"').Until(Parse.Char('"')).Text().Token();
    
    public static readonly Parser<string> Number = 
        Parse.Digit.AtLeastOnce().Text().Token();

    // ── Variables: x = 10 Or x: int = 10 ─ـ
    public static readonly Parser<string> VariableDecl = 
        from name in Identifier
        from type in (
            from colon in Parse.Char(':').Token()
            from t in Parse.String("int").Or(Parse.String("str")).Or(Parse.String("bool")).Or(Parse.String("float")).Or(Parse.String("object")).Or(Parse.String("Any")).Text().Token()
            select t
        ).Optional()
        from eq in Parse.Char('=').Token()
        from value in Parse.CharExcept(";\n\r").AtLeastOnce().Text()
        from semicolon in Parse.Char(';').Optional()
        select type.IsDefined
            ? $"{(type.Get() == "str" ? "string" : type.Get() == "Any" ? "object" : type.Get())} {name} = {value.Trim()};"
            : $"var {name} = {value.Trim()};";

    // ── Write ─ـ
    public static readonly Parser<string> WriteCall = 
        from write in Parse.String("Write")
        from open in Parse.Char('(').Token()
        from args in Parse.CharExcept(")").AtLeastOnce().Text()
        from close in Parse.Char(')').Token()
        select $"Console.WriteLine({args});";

    // ── If ─ـ
    public static readonly Parser<string> IfStatement = 
        from ifKw in Parse.String("if").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"if ({condition}) {{";

    // ── Elif ─ـ
    public static readonly Parser<string> ElifStatement = 
        from elifKw in Parse.String("elif").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"else if ({condition}) {{";

    // ── Else ─ـ
    public static readonly Parser<string> ElseStatement = 
        from elseKw in Parse.String("else").Token()
        select $"else {{";

    // ── Close Brace ─ـ
    public static readonly Parser<string> CloseBrace = 
        from brace in Parse.Char('}').Token()
        select "}";

    // ── For Loop ─ـ
    public static readonly Parser<string> ForLoop = 
        from forKw in Parse.String("for").Token()
        from openP in Parse.Char('(').Token()
        from varName in Identifier
        from inKw in Parse.String("in").Token()
        from range in Number
        from closeP in Parse.Char(')').Token()
        select $"foreach (var {varName} in Enumerable.Range(0, {range})) {{";

    // ── While Loop ─ـ
    public static readonly Parser<string> WhileLoop = 
        from whileKw in Parse.String("while").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"while ({condition}) {{";

    // ── Line ─ـ
    public static readonly Parser<string> Line = 
    VariableDecl.Or(IfStatement).Or(ElifStatement).Or(ElseStatement).Or(CloseBrace);

    // ── Program ─ـ
    public static readonly Parser<string> Program = 
        Line.DelimitedBy(Parse.LineEnd).Select(lines => string.Join("\n", lines));
}
