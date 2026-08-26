using Sprache;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpton.Core;

public static class SharpThonParser
{

    private static string BuildLambda(
        IEnumerable<string> parameters,
        string body)
    {
        var paramList = parameters.ToList();

        if (paramList.Count == 1)
            return $"{paramList[0]} => {body}";

        return $"({string.Join(", ", paramList)}) => {body}";
    }

    private static bool IsLambdaExpression(string value)
    {
        var trimmed = value.Trim();
        return Regex.IsMatch(
            trimmed,
            @"^(?:[A-Za-z_]\w*(?:\s*,\s*[A-Za-z_]\w*)*|\([^)]*\))\s*=>"
        );
    }

    private static int CountLambdaParameters(string value)
    {
        var trimmed = value.Trim();
        var arrowIndex = trimmed.IndexOf("=>");
        if (arrowIndex < 0) return 1;

        var paramPart = trimmed.Substring(0, arrowIndex).Trim();

        if (paramPart.StartsWith("(") && paramPart.EndsWith(")"))
        {
            paramPart = paramPart.Substring(1, paramPart.Length - 2).Trim();
            if (string.IsNullOrEmpty(paramPart)) return 0;
            return paramPart.Split(',').Length;
        }

        if (paramPart.Contains(','))
            return paramPart.Split(',').Length;
        else
            return 1;
    }

    private static string BuildFuncType(int paramCount)
    {
        if (paramCount <= 0) return "Func<dynamic>";
        var types = string.Join(", ", Enumerable.Repeat("dynamic", paramCount));
        return $"Func<{types}, dynamic>";
    }

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
        IOption<string> type,
        IEnumerable<string> modifiers,
        string value)
    {
        if (type.IsDefined)
            return type.Get();

        if (modifiers.Any(m => m == "const" || m == "readonly"))
            return InferVariableTypeFromValue(value);

        if (IsLambdaExpression(value))
        {
            int paramCount = CountLambdaParameters(value);
            return BuildFuncType(paramCount);
        }

        return "var";
    }

    private static string InferVariableTypeFromValue(string value)
    {
        value = value.Trim();

        if (value.Length >= 2 &&
            value.StartsWith("\"") &&
            value.EndsWith("\""))
        {
            return "string";
        }

        if (value == "true" || value == "false")
            return "bool";

        if (Regex.IsMatch(value, @"^-?\d+$"))
            return "int";

        if (Regex.IsMatch(value, @"^-?\d+\.\d+[fF]?$"))
        {
            return value.EndsWith(
                "f",
                StringComparison.OrdinalIgnoreCase
            )
                ? "float"
                : "double";
        }

        return "object";
    }

    private static readonly Parser<string> AccessModifier =
        Parse.String("public")
            .Or(Parse.String("private"))
            .Or(Parse.String("protected"))
            .Text()
            .Token();

    private static readonly Parser<string> ConstModifier =
        Parse.String("const").Text().Token();

    private static readonly Parser<string> ReadonlyModifier =
        Parse.String("readonly").Text().Token();

    private static readonly Parser<IEnumerable<string>> VariableModifiers =
        from access in AccessModifier.Optional()
        from constKw in ConstModifier.Optional()
        from readonlyKw in ReadonlyModifier.Optional()
        select new[]
        {
            access.GetOrElse(""),
            constKw.IsDefined ? "const" : "",
            readonlyKw.IsDefined ? "readonly" : ""
        }.Where(x => !string.IsNullOrEmpty(x));

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
        from type in TypeName
        select $"{modifier} {type} {name};";
    private static string MapTypeName(string type)
    {
        return type switch
        {
            "str" => "string",
            "Any" => "object",
            _ => type
        };
    }

    public static readonly Parser<string> TypeName =
        Parse.Ref(() =>
            from name in Identifier
            from genericArgs in (
                from open in Parse.Char('<').Token()
                from args in TypeName.DelimitedBy(Parse.Char(',').Token())
                from close in Parse.Char('>').Token()
                select args.ToList()
            ).Optional()
            select genericArgs.IsDefined
                ? $"{MapTypeName(name)}<{string.Join(", ", genericArgs.Get())}>"
                : MapTypeName(name)
        );

    // Constructor-shaped values are parsed separately from generic values.
    // Whether the name is actually a class is resolved by Transpiler using
    // the classes declared in the current source file.
    public static readonly Parser<string> ConstructorCall =
        from typeName in Identifier
        from openParen in Parse.Char('(').Token()
        from arguments in Parse.CharExcept(")\n\r").Many().Text()
        from closeParen in Parse.Char(')').Token()
        select typeName + "(" + arguments + ")";

    // Recursive collection value parser. Allows nested lists and dictionaries.
    public static readonly Parser<string> ValueLiteral =
        Parse.Ref(() =>
            DictionaryLiteral
                .Or(ListLiteral)
                .Or(ConstructorCall)
                .Or(
                    Parse.CharExcept(",}]\n\r")
                        .AtLeastOnce()
                        .Text()
                        .Select(x => x.Trim())
                )
        );

    // List literal:
    // [1, 2, 3]
    // [[1, 2], [3, 4]]
    // [ {"a": 1}, {"b": 2} ]
    public static readonly Parser<string> ListLiteral =
        Parse.Ref(() =>
            from open in Parse.Char('[').Token()
            from items in
                ValueLiteral
                    .DelimitedBy(Parse.Char(',').Token())
                    .Optional()
            from close in Parse.Char(']').Token()
            select BuildListExpression(
                items.GetOrElse(new List<string>())
            )
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
            from colon in Parse.Char(':').Token()
            from value in ValueLiteral
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
        var values = items
            .Select(x => x.Trim())
            .ToList();

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
        if (values.Count == 0)
            return "object";

        if (values.All(IsListExpression))
        {
            var nestedTypes = values
                .Select(GetListElementTypeFromExpression)
                .Distinct()
                .ToList();

            if (nestedTypes.Count == 1)
                return $"List<{nestedTypes[0]}>";

            return "List<object>";
        }

        if (values.All(IsDictionaryExpression))
            return "Dictionary<object, object>";

        var types = values
            .Select(InferListValueType)
            .Distinct()
            .ToList();

        if (types.Count == 1)
            return types[0];

        return "object";
    }

    private static bool IsListExpression(string value)
    {
        value = value.Trim();
        return value.StartsWith("new List<", StringComparison.Ordinal) &&
               value.Contains('>');
    }

    private static bool IsDictionaryExpression(string value)
    {
        value = value.Trim();
        return value.StartsWith("new Dictionary<", StringComparison.Ordinal) &&
               value.Contains('>');
    }

    private static string GetListElementTypeFromExpression(
        string value)
    {
        value = value.Trim();

        const string prefix = "new List<";

        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return "object";

        var start = prefix.Length;
        var end = value.LastIndexOf('>');

        if (end < 0)
            return "object";

        return value.Substring(start, end - start);
    }

    private static string InferListValueType(
        string value)
    {
        value = value.Trim();

        if (IsListExpression(value))
            return GetListElementTypeFromExpression(value);

        if (IsDictionaryExpression(value))
            return "Dictionary<object, object>";

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

        if (Regex.IsMatch(value, @"^-?\d+$"))
            return "int";

        if (Regex.IsMatch(value, @"^-?\d+\.\d+[fF]?$"))
        {
            if (value.EndsWith("f", StringComparison.OrdinalIgnoreCase))
                return "float";

            return "double";
        }

        return "object";
    }

    private static string ApplyDeclaredTypeToDictionary(
        IOption<string> type,
        string value)
    {
        if (!type.IsDefined)
            return value;

        var declaredType = type.Get();

        if (!declaredType.StartsWith(
                "Dictionary<",
                StringComparison.Ordinal))
            return value;

        if (!value.StartsWith(
                "new Dictionary<object, object>",
                StringComparison.Ordinal))
            return value;

        var genericArguments = ExtractGenericArguments(declaredType);

        if (genericArguments.Count != 2)
            return value;

        return ConvertDictionaryExpression(
            value,
            genericArguments[0],
            genericArguments[1]
        );
    }

    private static string ConvertDictionaryExpression(
        string value,
        string expectedKeyType,
        string expectedValueType)
    {
        value = value.Trim();

        var openBrace = value.IndexOf('{');
        var closeBrace = value.LastIndexOf('}');

        if (openBrace < 0 || closeBrace < openBrace)
            return value;

        var body = value.Substring(
            openBrace + 1,
            closeBrace - openBrace - 1
        ).Trim();

        if (body.Length == 0)
            return
                $"new Dictionary<{expectedKeyType}, {expectedValueType}>()";

        var entries = SplitTopLevelCommaSeparated(body);
        var convertedEntries = new List<string>();

        foreach (var entry in entries)
        {
            var separator = entry.IndexOf("] = ", StringComparison.Ordinal);

            if (separator < 0)
            {
                convertedEntries.Add(entry.Trim());
                continue;
            }

            var key = entry.Substring(0, separator + 1).Trim();
            var itemValue = entry.Substring(separator + 4).Trim();

            var rawKey = key.StartsWith("[") && key.EndsWith("]")
                ? key.Substring(1, key.Length - 2).Trim()
                : key;

            var convertedKey = ConvertCollectionValue(
                rawKey,
                expectedKeyType
            );

            var convertedValue = ConvertCollectionValue(
                itemValue,
                expectedValueType
            );

            convertedEntries.Add(
                $"[{convertedKey}] = {convertedValue}"
            );
        }

        return
            $"new Dictionary<{expectedKeyType}, {expectedValueType}> " +
            $"{{ {string.Join(", ", convertedEntries)} }}";
    }

    private static string ApplyDeclaredTypeToCollection(
        IOption<string> type,
        string value)
    {
        if (!type.IsDefined)
            return value;

        var declaredType = type.Get();

        if (declaredType.StartsWith("Dictionary<", StringComparison.Ordinal))
            return ApplyDeclaredTypeToDictionary(type, value);

        if (!declaredType.StartsWith("List<", StringComparison.Ordinal))
            return value;

        if (!value.StartsWith("new List<", StringComparison.Ordinal))
            return value;

        var elementType = ExtractGenericArgument(declaredType);

        if (string.IsNullOrEmpty(elementType))
            return value;

        return ConvertListExpression(value, elementType);
    }

    private static string ConvertListExpression(
        string value,
        string expectedElementType)
    {
        value = value.Trim();

        var openBrace = value.IndexOf('{');
        var closeBrace = value.LastIndexOf('}');

        if (openBrace < 0 || closeBrace < openBrace)
            return value;

        var body = value.Substring(
            openBrace + 1,
            closeBrace - openBrace - 1
        ).Trim();

        var items = SplitTopLevelCommaSeparated(body);

        var convertedItems = items
            .Select(item => ConvertCollectionValue(
                item,
                expectedElementType
            ))
            .ToList();

        return
            $"new List<{expectedElementType}> " +
            $"{{ {string.Join(", ", convertedItems)} }}";
    }

    private static string ConvertCollectionValue(
        string value,
        string expectedType)
    {
        value = value.Trim();

        if (expectedType.StartsWith("List<", StringComparison.Ordinal) &&
            value.StartsWith("new List<", StringComparison.Ordinal))
        {
            var nestedElementType = ExtractGenericArgument(expectedType);

            if (!string.IsNullOrEmpty(nestedElementType))
                return ConvertListExpression(value, nestedElementType);
        }

        if (expectedType.StartsWith(
                "Dictionary<",
                StringComparison.Ordinal) &&
            value.StartsWith(
                "new Dictionary<object, object>",
                StringComparison.Ordinal))
        {
            var genericArguments = ExtractGenericArguments(expectedType);

            if (genericArguments.Count == 2)
            {
                return ConvertDictionaryExpression(
                    value,
                    genericArguments[0],
                    genericArguments[1]
                );
            }
        }

        if (expectedType == "float" &&
            Regex.IsMatch(value, @"^-?\d+\.\d+$"))
        {
            return value + "f";
        }

        return value;
    }

    private static List<string> ExtractGenericArguments(string type)
    {
        var open = type.IndexOf('<');
        var close = type.LastIndexOf('>');

        if (open < 0 || close <= open)
            return new List<string>();

        var body = type.Substring(
            open + 1,
            close - open - 1
        );

        return SplitTopLevelCommaSeparated(body);
    }

    private static string ExtractGenericArgument(string type)
    {
        var open = type.IndexOf('<');
        var close = type.LastIndexOf('>');

        if (open < 0 || close <= open)
            return "";

        return type.Substring(
            open + 1,
            close - open - 1
        );
    }

    private static List<string> SplitTopLevelCommaSeparated(
        string value)
    {
        var result = new List<string>();
        var current = new StringBuilder();

        var angleDepth = 0;
        var braceDepth = 0;
        var bracketDepth = 0;
        var inString = false;
        var escaped = false;

        foreach (var ch in value)
        {
            if (ch == '"' && !escaped)
                inString = !inString;

            if (!inString)
            {
                if (ch == '<') angleDepth++;
                else if (ch == '>') angleDepth--;
                else if (ch == '{') braceDepth++;
                else if (ch == '}') braceDepth--;
                else if (ch == '[') bracketDepth++;
                else if (ch == ']') bracketDepth--;
            }

            if (
                ch == ',' &&
                !inString &&
                angleDepth == 0 &&
                braceDepth == 0 &&
                bracketDepth == 0
            )
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                escaped = false;
                continue;
            }

            current.Append(ch);

            if (ch == '\\' && !escaped)
                escaped = true;
            else
                escaped = false;
        }

        if (current.Length > 0)
            result.Add(current.ToString().Trim());

        return result;
    }

    // Variables: x = 10 Or x: int = 10
    public static readonly Parser<string> VariableDecl =
        from modifiers in VariableModifiers

        from name in Identifier

        from type in (
            from colon in Parse.Char(':').Token()
            from t in TypeName
            select t
        ).Optional()

        from eq in Parse.Char('=').Token()

        from value in
            DictionaryLiteral
                .Or(ListLiteral)
                .Or(ConstructorCall)
                .Or(LambdaExpression)
                .Or(Parse.CharExcept(";\n\r").AtLeastOnce().Text())

        from semicolon in Parse.Char(';').Optional()

        let rawValue = value.Trim()
        let csharpType = GetVariableCSharpType(
            type,
            modifiers,
            rawValue
        )
        let finalValue = ApplyDeclaredTypeToCollection(
            type,
            rawValue
        )
        let modifierString = modifiers.Any()
            ? string.Join(" ", modifiers) + " "
            : ""

        select
            $"{modifierString}" +
            $"{csharpType} " +
            $"{name} = {finalValue};";

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
        from type in TypeName
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

    // Lambda
    public static readonly Parser<string> LambdaExpression =
        from parameters in Identifier.DelimitedBy(Parse.Char(',').Token())
        from arrow in Parse.String("=>").Token()
        from body in Parse.CharExcept(";\n\r").AtLeastOnce().Text()
        select BuildLambda(parameters, body.Trim());

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

                from t in TypeName

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

            from t in TypeName

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

    // Class / interface declarations
    //
    // Supports: class Dog
    //           class Dog : Animal, IAnimal
    //           interface IAnimal
    //           interface IAnimal : IBaseAnimal, IDisposable
    public static readonly Parser<string> ClassDecl =
        from cls in Parse.String("class").Token()
        from name in Identifier
        from inheritance in (
            from colon in Parse.Char(':').Token()
            from bases in Identifier.DelimitedBy(Parse.Char(',').Token())
            select bases.ToList()
        ).Optional()
        select inheritance.IsDefined
            ? $"class {name} : {string.Join(", ", inheritance.Get())} {{"
            : $"class {name} {{";

    public static readonly Parser<string> InterfaceDecl =
        from interfaceKw in Parse.String("interface").Token()
        from name in Identifier
        from inheritance in (
            from colon in Parse.Char(':').Token()
            from bases in Identifier.DelimitedBy(Parse.Char(',').Token())
            select bases.ToList()
        ).Optional()
        select inheritance.IsDefined
            ? $"interface {name} : {string.Join(", ", inheritance.Get())} {{"
            : $"interface {name} {{";

    // Interface members are declarations, not implementations.
    // Example: def speak(name: str) -> str
    // becomes:  string speak(string name);
    public static readonly Parser<string> InterfaceMethodDecl =
        from access in
            Parse.String("public")
                .Or(Parse.String("private"))
                .Or(Parse.String("protected"))
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
                from t in TypeName
                select t
            ).Optional()
            select type.IsDefined
                ? $"{MapTypeName(type.Get())} {param}"
                : $"dynamic {param}"
        ).DelimitedBy(Parse.Char(',').Token()).Optional()
        from closeP in Parse.Char(')').Token()
        from returnType in (
            from arrow in Parse.String("->").Token()
            from t in TypeName
            select t
        ).Optional()
        select $"{(access.IsDefined ? access.Get() + " " : "")}" +
               $"{(returnType.IsDefined
                    ? (returnType.Get() == "None" ? "void" : MapTypeName(returnType.Get()))
                    : "void")} " +
               $"{name}({string.Join(", ", args.GetOrElse(new List<string>()))});";

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
            .Or(AwaitGoStatement)
            .Or(GoStatement)
            .Or(ReturnStatement)
            .Or(WriteCall)
            .Or(PropertyDecl)
            .Or(GetAccessor)
            .Or(SetAccessor)
            .Or(InterfaceDecl)
            .Or(ClassDecl)
            // InterfaceMethodDecl is intentionally not part of the generic
            // Line parser. The Transpiler selects it only while inside an
            // interface; otherwise `def` must use FunctionDecl.
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
