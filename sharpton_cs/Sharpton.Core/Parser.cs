using Sprache;
using System.Text.RegularExpressions;

namespace Sharpton.Core;

public static class SharpThonParser
{
    private static string BuildDictionaryExpression(IEnumerable<string> entries)
    {
        var pairs = entries
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Select(ParseDictionaryEntry)
            .ToList();

        if (pairs.Count == 0)
            return "new Dictionary<object, object>()";

        var keyTypes = pairs.Select(x => InferDictionaryValueType(x.Key)).Distinct().ToList();
        var valueTypes = pairs.Select(x => InferDictionaryValueType(x.Value)).Distinct().ToList();

        var keyType = keyTypes.Count == 1 ? keyTypes[0] : "object";
        var valueType = valueTypes.Count == 1 ? valueTypes[0] : "object";

        return $"new Dictionary<{keyType}, {valueType}> {{ {string.Join(", ", pairs.Select(x => $"[{x.Key}] = {x.Value}"))} }}";
    }

    private static (string Key, string Value) ParseDictionaryEntry(string entry)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = 0; i < entry.Length; i++)
        {
            var c = entry[i];
            if (c == '"' && !escaped)
                inString = !inString;

            if (!inString)
            {
                if (c == '(' || c == '[' || c == '{') depth++;
                else if (c == ')' || c == ']' || c == '}') depth--;
                else if (c == ':' && depth == 0)
                    return (entry[..i].Trim(), entry[(i + 1)..].Trim());
            }

            escaped = c == '\\' && !escaped;
            if (c != '\\') escaped = false;
        }

        throw new FormatException($"Invalid dictionary entry: {entry}");
    }

    private static string InferDictionaryValueType(string value)
    {
        value = value.Trim();

        if (value.StartsWith("\"") && value.EndsWith("\""))
            return "string";

        if (value == "true" || value == "false")
            return "bool";

        if (Regex.IsMatch(value, @"^-?\d+$"))
            return "int";

        if (Regex.IsMatch(value, @"^-?\d+\.\d+[fF]?$"))
            return value.EndsWith("f", StringComparison.OrdinalIgnoreCase) ? "float" : "double";

        return "object";
    }

    // Dictionary literal: {"ali": 16, "mamad": 20}
    public static readonly Parser<string> DictionaryLiteral =
        from open in Parse.Char('{').Token()
        from entries in (
            Parse.CharExcept("},\n\r")
                .AtLeastOnce()
                .Text()
                .Select(x => x.Trim())
        ).DelimitedBy(Parse.Char(',').Token()).Optional()
        from close in Parse.Char('}').Token()
        select BuildDictionaryExpression(entries.GetOrElse(new List<string>()));

    // Base Tokens
    public static readonly Parser<string> Identifier =
        from first in Parse.Letter.Or(Parse.Char('_'))
        from rest in Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
        select first + new string(rest.ToArray());
        
    public static readonly Parser<string> String = 
        Parse.Char('"').Until(Parse.Char('"')).Text().Token();

    public static readonly Parser<string> Number =
        Parse.Digit.AtLeastOnce().Text().Token();
    
    public static readonly Parser<string> FieldDecl =
        from modifier in
        (
            Parse.String("public")
                .Or(Parse.String("private"))
                .Or(Parse.String("protected"))
                .Text()
                .Token()
        )
        from name in Identifier
        from colon in Parse.Char(':').Token()
        from type in Parse.String("int")
            .Or(Parse.String("str"))
            .Or(Parse.String("bool"))
            .Or(Parse.String("float"))
            .Or(Parse.String("long"))
            .Or(Parse.String("double"))
            .Or(Parse.String("object"))
            .Or(Parse.String("Any"))
            .Text()
            .Token()
        select
            $"{modifier} " +
            $"{(type == "str"
                ? "string"
                : type == "Any"
                    ? "object"
                    : type)} " +
            $"{name};";

    // Variables: x = 10 Or x: int = 10
    public static readonly Parser<string> VariableDecl =
        from modifier in
        (
            Parse.String("public")
                .Or(Parse.String("private"))
                .Or(Parse.String("protected"))
                .Text()
                .Token()
        ).Optional()

        from name in Identifier
        from type in (
            from colon in Parse.Char(':').Token()
            from t in Parse.String("int")
                .Or(Parse.String("str"))
                .Or(Parse.String("bool"))
                .Or(Parse.String("float"))
                .Or(Parse.String("long"))
                .Or(Parse.String("double"))
                .Or(Parse.String("object"))
                .Or(Parse.String("Any"))
                .Text()
                .Token()
            select t
        ).Optional()
        from eq in Parse.Char('=').Token()
        from value in
            DictionaryLiteral
                .Or(Parse.CharExcept(";\n\r").AtLeastOnce().Text())
        from semicolon in Parse.Char(';').Optional()

        select
            $"{(modifier.IsDefined ? modifier.Get() + " " : "")}" +
            $"{(type.IsDefined
                ? (type.Get() == "str"
                    ? "string"
                    : type.Get() == "Any"
                        ? "object"
                        : type.Get())
                : "var")} " +
            $"{name} = {value.Trim()};";

    // If
    public static readonly Parser<string> IfStatement = 
        from ifKw in Parse.String("if").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"if ({condition}) {{";

    // Elif
    public static readonly Parser<string> ElifStatement = 
        from elifKw in Parse.String("elif").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"else if ({condition}) {{";

    // Else
    public static readonly Parser<string> ElseStatement = 
        from elseKw in Parse.String("else").Token()
        select $"else {{";

    // Close Brace
    public static readonly Parser<string> CloseBrace = 
        from brace in Parse.Char('}').Token()
        select "}";

    // For Loop
    public static readonly Parser<string> ForLoop = 
        from forKw in Parse.String("for").Token()
        from openP in Parse.Char('(').Token()
        from varName in Identifier
        from inKw in Parse.String("in").Token()
        from range in Number
        from closeP in Parse.Char(')').Token()
        select $"foreach (var {varName} in Enumerable.Range(0, {range})) {{";

    // While Loop
    public static readonly Parser<string> WhileLoop = 
        from whileKw in Parse.String("while").Token()
        from openP in Parse.Char('(').Token()
        from condition in Parse.CharExcept(")").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"while ({condition}) {{";

    // Function
    public static readonly Parser<string> FunctionDecl =
        from modifier in
            Parse.String("public")
                .Or(Parse.String("private"))
                .Or(Parse.String("static"))
                .Text()
                .Token()
                .Optional()

        from defKw in Parse.String("def").Token()
        from name in Identifier
        from openP in Parse.Char('(').Token()

        from args in (
            from param in Identifier

            from type in (
                from colon in Parse.Char(':').Token()

                from t in Parse.String("int")
                    .Or(Parse.String("str"))
                    .Or(Parse.String("bool"))
                    .Or(Parse.String("float"))
                    .Or(Parse.String("double"))
                    .Or(Parse.String("long"))
                    .Or(Parse.String("object"))
                    .Or(Parse.String("Any"))
                    .Text()
                    .Token()

                select t
            ).Optional()

            select type.IsDefined
                ? $"{(
                    type.Get() == "str"
                        ? "string"
                        : type.Get() == "Any"
                            ? "object"
                            : type.Get()
                )} {param}"
                : $"object {param}"

        ).DelimitedBy(Parse.Char(',').Token()).Optional()

        from closeP in Parse.Char(')').Token()

        from returnType in (
            from arrow in Parse.String("->").Token()

            from t in Parse.String("int")
                .Or(Parse.String("str"))
                .Or(Parse.String("bool"))
                .Or(Parse.String("float"))
                .Or(Parse.String("double"))
                .Or(Parse.String("long"))
                .Or(Parse.String("object"))
                .Or(Parse.String("void"))
                .Or(Parse.String("None"))
                .Or(Parse.String("Any"))
                .Text()
                .Token()

            select t
        ).Optional()

        select $"{(
            modifier.IsDefined
                ? modifier.Get() + " "
                : "static "
        )}{(
            returnType.IsDefined
                ? (
                    returnType.Get() == "str"
                        ? "string"
                        : returnType.Get() == "Any"
                            ? "object"
                            : returnType.Get() == "None"
                                ? "void"
                                : returnType.Get()
                )
                : "object"
        )} {name}({string.Join(", ", args.GetOrElse(new List<string>()))}) {{";
    // Write
    public static readonly Parser<string> WriteCall = 
        from write in Parse.String("Write").Token()
        from open in Parse.Char('(').Token()
        from args in (
            from arg in Parse.CharExcept(",)\n\r").AtLeastOnce().Text()
            select arg.Trim()
        ).DelimitedBy(Parse.Char(',').Token())
        from close in Parse.Char(')').Token()
        select $"Console.WriteLine({string.Join(", ", args)});";
    
    public static readonly Parser<string> Increment = 
        from name in Identifier
        from plus in Parse.String("++").Token()
        select $"{name} += 1;";
    
    // Try
    public static readonly Parser<string> TryStatement = 
        from tryKw in Parse.String("try").Token()
        select "try {";

    // Catch (with "except" alias)
    public static readonly Parser<string> CatchStatement =
        from catchKw in Parse.String("catch")
            .Or(Parse.String("except"))
            .Token()

        from args in (
            from openP in Parse.Char('(').Token()
            from exceptionType in Identifier
            from asKw in Parse.String("as").Token().Optional()
            from varName in Identifier.Optional()
            from closeP in Parse.Char(')').Token()
            select varName.IsDefined
                ? $"catch ({exceptionType} {varName.Get()}) {{"
                : $"catch ({exceptionType}) {{"
        ).Optional()

        select args.IsDefined
            ? args.Get()
            : "catch {";
    
    public static readonly Parser<string> CloseAndCatch = 
        from close in Parse.Char('}').Token()
        from catchKw in Parse.String("catch").Or(Parse.String("except")).Token()
        from args in (
            from openP in Parse.Char('(').Token()
            from exceptionType in Identifier
            from asKw in Parse.String("as").Token().Optional()
            from varName in Identifier.Optional()
            from closeP in Parse.Char(')').Token()
            select varName.IsDefined 
                ? $"catch ({exceptionType} {varName.Get()}) {{" 
                : $"catch ({exceptionType}) {{"
        ).Optional()
        select args.IsDefined ? $"}} {args.Get()}" : "} catch {";

    // Finally
    public static readonly Parser<string> FinallyStatement =
        from finallyKw in Parse.String("finally").Token()
        select "finally {";
    
    public static readonly Parser<string> CloseAndFinally = 
        from close in Parse.Char('}').Token()
        from finallyKw in Parse.String("finally").Token()
        select "} finally {";

    // import to using
    public static readonly Parser<string> ImportStatement =
        from importKw in Parse.String("import").Token()
        from ns in Parse.Regex(@"[A-Za-z_][A-Za-z0-9_.]*").Token()
        select $"using {ns};";

    // Return
    public static readonly Parser<string> ReturnStatement =
        from ret in Parse.String("return").Token()
        from value in Parse.CharExcept("\n\r").Many().Text()
        select $"return {value.Trim()};";

    // Class
    public static readonly Parser<string> ClassDecl =
    from cls in Parse.String("class").Token()
    from name in Identifier
    select $"class {name} {{";

    // Go - fire and forget
    public static readonly Parser<string> GoStatement =
        from goKw in Parse.String("go").Token()
        from call in Parse.CharExcept("\n\r").AtLeastOnce().Text()
        select $"Task.Run(() => {call.Trim().TrimEnd(';')});";

    // Await Go - wait for completion
    public static readonly Parser<string> AwaitGoStatement =
        from awaitKw in Parse.String("await").Token()
        from goKw in Parse.String("go").Token()
        from call in Parse.CharExcept("\n\r").AtLeastOnce().Text()
        select $"await Task.Run(() => {call.Trim().TrimEnd(';')});";


    // Line
    public static readonly Parser<string> Line =
        ImportStatement
            .Or(ImportStatement)
            .Or(AwaitGoStatement)
            .Or(GoStatement)
            .Or(ReturnStatement)
            .Or(WriteCall)
            .Or(VariableDecl)
            .Or(FunctionDecl)
            .Or(IfStatement)
            .Or(ElifStatement)
            .Or(ElseStatement)
            .Or(TryStatement)
            .Or(CloseAndFinally)
            .Or(CloseAndCatch)
            .Or(CatchStatement)
            .Or(FinallyStatement)
            .Or(ForLoop)
            .Or(WhileLoop)
            .Or(Increment)
            .Or(CloseBrace);

    // Program
    public static readonly Parser<string> Program = 
        Line.DelimitedBy(Parse.LineEnd).Select(lines => string.Join("\n", lines));
}
