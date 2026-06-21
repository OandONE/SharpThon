using System.Text.RegularExpressions;

namespace Sharpton.Core;

public class Transpiler
{
    public string Transpile(string spCode)
    {
        var cs = spCode;

        // ── 1. commants: # → // ──
        var lines = cs.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith('#'))
            {
                lines[i] = line.Replace("#", "//");
            }
        }
        cs = string.Join('\n', lines);

        // ── 2 semicolon ──
        lines = cs.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && 
                !line.EndsWith(';') && 
                !line.EndsWith('{') && 
                !line.EndsWith('}') &&
                !line.StartsWith("//") &&
                !line.StartsWith("def ") &&
                !line.StartsWith("class ") &&
                !line.StartsWith("try") &&
                !line.StartsWith("catch") &&
                !line.StartsWith("else") &&
                !line.StartsWith("elif"))
            {
                lines[i] = lines[i].TrimEnd() + ";";
            }
        }
        cs = string.Join('\n', lines);

        // counter: int = 0 → int counter = 0
        cs = Regex.Replace(cs, @"(\w+)\s*:\s*(\w+)\s*=", "$2 $1 =");

        // other (no type) → var
        cs = Regex.Replace(cs, @"^(\w+)\s*=", @"var $1 =", RegexOptions.Multiline);

        // ── 4. for (i in 3) → foreach با Range ─ـ
        cs = Regex.Replace(cs, @"for\s*\((\w+)\s+in\s+(\d+)\)\s*\{", 
            @"foreach (var $1 in Enumerable.Range(0, $2)) {");

        // ── 5. Write → Console.WriteLine ─ـ
        cs = Regex.Replace(cs, @"Write\(", "Console.WriteLine(");

        // ── 6. elif → else if ─ـ
        cs = Regex.Replace(cs, @"\belif\b", "else if");

        // ── 7. def → func C# ─ـ
        cs = Regex.Replace(cs, @"(public\s+|private\s+|static\s+)*def\s+(\w+)\s*\(([^)]*)\)\s*(->\s*(\w+))?\s*\{", match =>
        {
            var modifier = match.Groups[1].Value.Trim();
            var name = match.Groups[2].Value;
            var args = match.Groups[3].Value.Trim();
            var returnType = "void";

            if (match.Groups.Count >= 6 && match.Groups[5].Success)
                returnType = match.Groups[5].Value;
                if (returnType == "str") {
                    returnType = "string";
                }
                else if (returnType == "Any"){
                    returnType = "object";
                }
            else
            {
                var bodyStart = match.Index + match.Length;
                var bodyEnd = cs.IndexOf('}', bodyStart);
                if (bodyEnd > bodyStart)
                {
                    var body = cs.Substring(bodyStart, bodyEnd - bodyStart);
                    if (Regex.IsMatch(body, @"\breturn\b"))
                        returnType = "object";
                }
            }

            if (string.IsNullOrEmpty(modifier))
                modifier = "static";

            if (!string.IsNullOrEmpty(args))
            {
                var parts = args.Split(',');
                var converted = new List<string>();
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.Contains(':'))
                    {
                        var split = trimmed.Split(':');
                        var NameType = split[1].Trim();
                        var NameVar = split[0].Trim();
                        if (NameType == "str") {
                            NameType = "string";
                        }
                        else if (NameType == "Any"){
                            NameType = "object";
                        }
                        converted.Add($"{NameType} {NameVar}");
                    }
                    else
                    {
                        converted.Add($"object {trimmed}");
                    }
                }
                args = string.Join(", ", converted);
            }

            return $"{modifier} {returnType} {name}({args}) {{";
        });

        return cs;
    }
}
