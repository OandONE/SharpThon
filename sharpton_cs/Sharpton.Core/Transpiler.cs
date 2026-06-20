using System.Text.RegularExpressions;

namespace Sharpton.Core;

public class Transpiler
{
public string Transpile(string spCode)
{
    var cs = spCode;

    // ── ۱. کامنت‌ها: # → // ──
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

    // ── ۲. ; به خطوطی که ندارن ──
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

    // بقیه (بدون type) → var
    cs = Regex.Replace(cs, @"^(\w+)\s*=", @"var $1 =", RegexOptions.Multiline);

    // ── ۴. for (i in 3) → foreach با Range ─ـ
    cs = Regex.Replace(cs, @"for\s*\((\w+)\s+in\s+(\d+)\)\s*\{", 
        @"foreach (var $1 in Enumerable.Range(0, $2)) {");

    // ── ۵. Write → Console.WriteLine ─ـ
    cs = Regex.Replace(cs, @"Write\(", "Console.WriteLine(");

    // ── ۶. elif → else if ─ـ
    cs = Regex.Replace(cs, @"\belif\b", "else if");

    // ── ۷. def → تابع C# (موقت: حذف def) ─ـ
    cs = Regex.Replace(cs, @"def\s+(\w+)\s*\((.*?)\)\s*\{", match =>
    {
        var name = match.Groups[1].Value;
        var args = match.Groups[2].Value;
        return $"static int {name}({args}) {{";
    });

    return cs;
}
}
