using System.Text.RegularExpressions;
using Sprache;

namespace Sharpton.Core;

public class Transpiler
{
    public string Transpile(string spCode)
    {
        // ── 1. Delete Comments ──
        var lines = spCode.Split('\n');
        var cleaned = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("//") && !trimmed.StartsWith('#'))
                cleaned.Add(line);
        }
        spCode = string.Join('\n', cleaned);

        // ── 3. for (i in 5) { → foreach ─ـ
        spCode = Regex.Replace(spCode, @"for\s*\((\w+)\s+in\s+(\d+)\)\s*\{", 
            @"foreach (var $1 in Enumerable.Range(0, $2)) {");

        // ── 4. while (cond) { ─ـ
        spCode = Regex.Replace(spCode, @"while\s*\((.+)\)\s*\{", @"while ($1) {");

        // ── 5. ++ → += 1 ─ـ
        spCode = Regex.Replace(spCode, @"(\w+)\+\+", "$1 += 1");

        // ── 6. def → func C# ─ـ
        spCode = Regex.Replace(spCode, @"def\s+(\w+)\s*\(([^)]*)\)\s*(->\s*(\w+))?\s*\{", match =>
        {
            var name = match.Groups[1].Value;
            var args = match.Groups[2].Value.Trim();
            var returnType = match.Groups[4].Success ? match.Groups[4].Value : "void";
            if (returnType == "str") returnType = "string";
            if (returnType == "Any") returnType = "object";

            var converted = new List<string>();
            if (!string.IsNullOrEmpty(args))
            {
                foreach (var part in args.Split(','))
                {
                    var p = part.Trim();
                    if (p.Contains(':'))
                    {
                        var s = p.Split(':');
                        var t = s[1].Trim();
                        if (t == "str") t = "string";
                        if (t == "Any") t = "object";
                        converted.Add($"{t} {s[0].Trim()}");
                    }
                    else converted.Add($"object {p}");
                }
            }
            return $"static {returnType} {name}({string.Join(", ", converted)}) {{";
        });

        spCode = Regex.Replace(spCode, @"Write\(", "Console.WriteLine(");

        // ── ۷. Sprache Parse Line ─ـ
        var results = new List<string>();
        foreach (var line in spCode.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                results.Add("");
                continue;
            }

            try
            {
                var result = SharpThonParser.Line.Parse(trimmed);
                results.Add(result);
            }
            catch (Sprache.ParseException)
            {
                results.Add(trimmed);
            }
        }

        var finalLines = new List<string>();
        foreach (var line in results)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.EndsWith(';') || trimmed.EndsWith('{') || trimmed.EndsWith('}'))
                finalLines.Add(line);
            else
                finalLines.Add(line + ";");
        }
        return string.Join("\n", finalLines);
    }
}
