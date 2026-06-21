using System.Text.RegularExpressions;
using Sprache;

namespace Sharpton.Core;

public class Transpiler
{
    public string Transpile(string spCode)
    {
        // ── Delete Comments ──
        var lines = spCode.Split('\n');
        var cleaned = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("//") && !trimmed.StartsWith('#'))
                cleaned.Add(line);
        }
        spCode = string.Join('\n', cleaned);

        spCode = Regex.Replace(spCode, @"Write\((.+)\)", match =>
        {
            var inner = match.Groups[1].Value;
            int depth = 0;
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '(') depth++;
                if (inner[i] == ')') depth--;
                if (depth < 0) return $"Console.WriteLine({inner.Substring(0, i)});";
            }
            return $"Console.WriteLine({inner});";
        });

        // ── Sprache Parse Line ─ـ
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
