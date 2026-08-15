using Sprache;
using System.Text.RegularExpressions;

namespace Sharpton.Core;

public static class SharpThonParser
{
    private static string BuildListLiteral(string content)
    {
        // Empty list
        if (string.IsNullOrWhiteSpace(content))
            return "new List<object>()";

        var items = content
            .Split(',')
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToArray();

        if (items.Length == 0)
            return "new List<object>()";

        string elementType = InferListElementType(items);

        return $"new List<{elementType}> {{ {string.Join(", ", items)} }}";
    }

    private static string InferListElementType(string[] items)
    {
        bool allInt = items.All(
            x => int.TryParse(x, out _)
        );

        if (allInt)
            return "int";

        bool allDouble = items.All(
            x => double.TryParse(
                x,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out _
            )
        );

        if (allDouble)
            return "double";

        bool allBool = items.All(
            x =>
                x == "true" ||
                x == "false"
        );

        if (allBool)
            return "bool";

        bool allString = items.All(
            x =>
                x.Length >= 2 &&
                x.StartsWith("\"") &&
                x.EndsWith("\"")
        );

        if (allString)
            return "string";

        // Mixed / unknown expressions.
        return "object";
    }

    private static string GetVariableCSharpType(
        IOption<string> type)
    {
        if (!type.IsDefined)
            return "var";

        var value = type.Get();

        if (value.StartsWith("list["))
        {
            var elementType = value[5..^1];

            elementType = elementType switch
            {
                "str" => "string",
                "Any" => "object",
                _ => elementType
            };

            return $"List<{elementType}>";
        }

        return value switch
        {
            "str" => "string",
            "Any" => "object",
            _ => value
        };
    }

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

    // Constructor-shaped values are parsed separately from generic values.
    // Whether the name is actually a class is resolved by Transpiler using
    // the classes declared in the current source file.
    public static readonly Parser<string> ConstructorCall =
        from typeName in Identifier
        from openParen in Parse.Char('(').Token()
        from arguments in Parse.CharExcept(")\n\r").Many().Text()
        from closeParen in Parse.Char(')').Token()
        select typeName + "(" + arguments + ")";

    // List - literal: [1, 2, 3] or ["mamad", "sam", true, false]
    public static readonly Parser<string> ListLiteral =
        from open in Parse.Char('[').Token()
        from items in (
            Parse.CharExcept(",]\n\r")
                .AtLeastOnce()
                .Text()
                .Select(x => x.Trim())
        )
        .DelimitedBy(Parse.Char(',').Token())
        .Optional()
        from close in Parse.Char(']').Token()
        select BuildListExpression(
            items.GetOrElse(new List<string>())
        );

    private static string BuildDictionaryExpression(
        IEnumerable<(string Key, string Value)> entries)
    {
        var values = entries.ToList();

        if (values.Count == 0)
            return "new Dictionary<object, object>()";

        var initializer = string.Join(
            ", ",
            values.Select(x => $"[{x.Key}] = {x.Value}")
        );

        return $"new Dictionary<object, object> {{ {initializer} }}";
    }

    public static readonly Parser<string> DictionaryLiteral =
        from open in Parse.Char('{').Token()

        from entries in (
            from key in
                Parse.CharExcept(":,}\n\r")
                    .AtLeastOnce()
                    .Text()
                    .Select(x => x.Trim())

            from colon in
                Parse.Char(':').Token()

            from value in
                Parse.CharExcept(",}\n\r")
                    .AtLeastOnce()
                    .Text()
                    .Select(x => x.Trim())

            select (Key: key, Value: value)

        ).DelimitedBy(Parse.Char(',').Token()).Optional()

        from close in Parse.Char('}').Token()

        select BuildDictionaryExpression(
            entries.GetOrElse(
                new List<(string Key, string Value)>()
            )
        );

    private static string BuildListExpression(
        IEnumerable<string> items)
    {
        var values = items.ToList();

        if (values.Count == 0)
            return "new List<object>()";

        var elementType = InferListElementType(values);

        return
            $"new List<{elementType}> " +
            $"{{ {string.Join(", ", values)} }}";
    }

    private static string InferListElementType(
        List<string> values)
    {
        var types = values
            .Select(InferListValueType)
            .Distinct()
            .ToList();

        if (types.Count == 1)
            return types[0];

        return "object";
    }

    private static string InferListValueType(
        string value)
    {
        value = value.Trim();

        if (
            value.StartsWith("\"") &&
            value.EndsWith("\"")
        )
            return "string";

        if (
            value == "true" ||
            value == "false"
        )
            return "bool";

        if (Regex.IsMatch(
            value,
            @"^-?\d+$"
        ))
            return "int";

        if (Regex.IsMatch(
            value,
            @"^-?\d+\.\d+[fF]?$"
        ))
        {
            if (value.EndsWith("f", StringComparison.OrdinalIgnoreCase))
                return "float";

            return "double";
        }

        return "object";
    }

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

            from t in
                (
                    from listKw in Parse.String("list").Token()
                    from openBracket in Parse.Char('[').Token()

                    from elementType in
                        Parse.String("int")
                            .Or(Parse.String("str"))
                            .Or(Parse.String("bool"))
                            .Or(Parse.String("float"))
                            .Or(Parse.String("long"))
                            .Or(Parse.String("double"))
                            .Or(Parse.String("object"))
                            .Or(Parse.String("Any"))
                            .Text()
                            .Token()

                    from closeBracket in Parse.Char(']').Token()

                    select $"list[{elementType}]"
                )
                .Or(
                    Parse.String("int")
                        .Or(Parse.String("str"))
                        .Or(Parse.String("bool"))
                        .Or(Parse.String("float"))
                        .Or(Parse.String("long"))
                        .Or(Parse.String("double"))
                        .Or(Parse.String("object"))
                        .Or(Parse.String("Any"))
                        .Text()
                        .Token()
                )

            select t
        ).Optional()

        from eq in Parse.Char('=').Token()

        from value in
            DictionaryLiteral
                .Or(ListLiteral)
                .Or(ConstructorCall)
                .Or(Parse.CharExcept(";\n\r").AtLeastOnce().Text())

        from semicolon in Parse.Char(';').Optional()

        select
            $"{(modifier.IsDefined
                ? modifier.Get() + " "
                : "")}" +
            $"{GetVariableCSharpType(type)} " +
            $"{name} = {value.Trim()};";

    // Properties: [public|private|protected] [static] Name -> Type {
    public static readonly Parser<string> PropertyDecl =
        from access in Parse.String("public")
            .Or(Parse.String("private"))
            .Or(Parse.String("protected"))
            .Text()
            .Token()
            .Optional()
        from staticKw in Parse.String("static").Token().Optional()
        from name in Identifier
        from arrow in Parse.String("->").Token()
        from type in Parse.String("int")
            .Or(Parse.String("str"))
            .Or(Parse.String("bool"))
            .Or(Parse.String("float"))
            .Or(Parse.String("double"))
            .Or(Parse.String("long"))
            .Or(Parse.String("object"))
            .Or(Parse.String("Any"))
            .Text()
            .Token()
        select $"{(access.IsDefined ? access.Get() : "public")} " +
            $"{(staticKw.IsDefined ? "static " : "")}" +
            $"{(type == "str" ? "string" : type == "Any" ? "object" : type)} " +
            $"{name} {{";

    // Property accessors
    public static readonly Parser<string> GetAccessor =
        from getKw in Parse.String("get").Token()
        from openBrace in Parse.Char('{').Token()
        select "get {";

    public static readonly Parser<string> SetAccessor =
        from setKw in Parse.String("set").Token()
        from openParen in Parse.Char('(').Token()
        from value in Parse.String("value").Token()
        from closeParen in Parse.Char(')').Token()
        from openBrace in Parse.Char('{').Token()
        select "set {";

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

    // C-style For Loop: for (i = 0; i < 10; i++)
    public static readonly Parser<string> CStyleForLoop =
        from forKw in Parse.String("for").Token()
        from openP in Parse.Char('(').Token()
        from varName in Identifier
        from equalsSign in Parse.Char('=').Token()
        from initialValue in Parse.CharExcept(";\n\r").AtLeastOnce().Text()
        from firstSemicolon in Parse.Char(';').Token()
        from condition in Parse.CharExcept(";\n\r").AtLeastOnce().Text()
        from secondSemicolon in Parse.Char(';').Token()
        from increment in Parse.CharExcept(")\n\r").AtLeastOnce().Text()
        from closeP in Parse.Char(')').Token()
        select $"for (int {varName} = {initialValue.Trim()}; {condition.Trim()}; {increment.Trim()}) {{";

    // Range-based For Loop
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
                // Untyped parameters must support SharpThon operators at
                // runtime (for example x + y and x * y). Using object makes
                // those expressions invalid C#, while dynamic preserves the
                // language's untyped behavior.
                : $"dynamic {param}"

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

    // Class And Support Interfaces
    public static readonly Parser<string> ClassDecl =
        from cls in Parse.String("class").Token()
        from name in Identifier
        from inheritance in (
            from colon in Parse.Char(':').Token()
            from baseClass in Identifier
            select baseClass
        ).Optional()
        select inheritance.IsDefined
            ? $"class {name} : {inheritance.Get()} {{"
            : $"class {name} {{";

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
            .Or(PropertyDecl)
            .Or(GetAccessor)
            .Or(SetAccessor)
            .Or(ClassDecl)
            .Or(DictionaryLiteral)
            .Or(ListLiteral)
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
            .Or(CStyleForLoop)
            .Or(ForLoop)
            .Or(WhileLoop)
            .Or(Increment)
            .Or(CloseBrace);

    // Program
    public static readonly Parser<string> Program = 
        Line.DelimitedBy(Parse.LineEnd).Select(lines => string.Join("\n", lines));
}
