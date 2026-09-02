using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sharpton.Core;

/// <summary>
/// Built-in compatibility libraries exposed through SharpThon's `require` keyword.
/// The transpiler delegates library discovery and code generation to this registry.
/// </summary>
public static class SharpThonLibraries
{
    private static readonly Dictionary<string, string> ClassNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["os"] = "SharpThonOs",
            ["json"] = "SharpThonJson",
            ["re"] = "SharpThonRe",
            ["sys"] = "SharpThonSys",
            ["random"] = "SharpThonRandom",
            ["math"] = "SharpThonMath",
            ["time"] = "SharpThonTime"
        };

    public static readonly string[] Order =
    {
        "os", "json", "re", "sys", "random", "math", "time"
    };

    public static bool IsSupported(string name) => ClassNames.ContainsKey(name);

    public static string NormalizeName(string name) => name.ToLowerInvariant();

    public static string GetClassName(string name) => ClassNames[NormalizeName(name)];

    public static string GetBody(string name)
    {
        return NormalizeName(name) switch
        {
            "os" => OsBody,
            "json" => JsonBody,
            "re" => ReBody,
            "sys" => SysBody,
            "random" => RandomBody,
            "math" => MathBody,
            "time" => TimeFormattingBody + TimeBody,
            _ => ""
        };
    }

    private const string OsBody = """
public static class SharpThonOs
{
    public static string name => System.OperatingSystem.IsWindows() ? "nt" : "posix";
    public static string sep => System.IO.Path.DirectorySeparatorChar.ToString();
    public static string altsep => System.OperatingSystem.IsWindows() ? "/" : "";
    public static string pathsep => System.IO.Path.PathSeparator.ToString();
    public static string linesep => System.Environment.NewLine;
    public static string curdir => ".";
    public static string pardir => "..";
    public static string devnull => System.OperatingSystem.IsWindows() ? "NUL" : "/dev/null";
    public static int getpid() => System.Environment.ProcessId;

    public static int getppid()
    {
        if (System.OperatingSystem.IsWindows())
            return -1;

        try
        {
            var stat = System.IO.File.ReadAllText("/proc/self/stat");
            var closeParen = stat.LastIndexOf(')');
            if (closeParen < 0) return -1;

            var fields = stat[(closeParen + 1)..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return fields.Length > 1 && int.TryParse(fields[1], out var ppid) ? ppid : -1;
        }
        catch
        {
            return -1;
        }
    }
    public static int cpu_count() => System.Environment.ProcessorCount;

    public static System.Collections.Generic.Dictionary<string, string> environ =>
        System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(
                x => x.Key?.ToString() ?? "",
                x => x.Value?.ToString() ?? "",
                System.StringComparer.Ordinal);

    public static string getcwd() => System.IO.Directory.GetCurrentDirectory();
    public static void chdir(string path) => System.IO.Directory.SetCurrentDirectory(path);

    public static string[] listdir(string path = ".") =>
        System.IO.Directory.GetFileSystemEntries(path)
            .Select(x => System.IO.Path.GetFileName(x) ?? "")
            .Where(x => x.Length > 0)
            .ToArray();

    public static void mkdir(string path) => System.IO.Directory.CreateDirectory(path);
    public static void makedirs(string path) => System.IO.Directory.CreateDirectory(path);
    public static void remove(string path) => System.IO.File.Delete(path);
    public static void unlink(string path) => remove(path);
    public static void rmdir(string path) => System.IO.Directory.Delete(path);
    public static void rename(string src, string dst) => System.IO.File.Move(src, dst, true);
    public static void replace(string src, string dst) => System.IO.File.Move(src, dst, true);

    public static string? getenv(string key, string? defaultValue = null) =>
        System.Environment.GetEnvironmentVariable(key) ?? defaultValue;

    public static void setenv(string key, string value) =>
        System.Environment.SetEnvironmentVariable(key, value);

    public static void unsetenv(string key) =>
        System.Environment.SetEnvironmentVariable(key, null);

    public static long getsize(string path) => new System.IO.FileInfo(path).Length;
    public static double getmtime(string path) =>
        (System.IO.File.GetLastWriteTimeUtc(path) - System.DateTime.UnixEpoch).TotalSeconds;
    public static double getctime(string path) =>
        (System.IO.File.GetCreationTimeUtc(path) - System.DateTime.UnixEpoch).TotalSeconds;

    public static SharpThonOsStat stat(string path) => new(path);

    public static string abspath(string path) => System.IO.Path.GetFullPath(path);
    public static string realpath(string path) => PathApi.RealPath(path);
    public static string normpath(string path) => PathApi.NormalizePath(path);
    public static string normcase(string path) =>
        System.OperatingSystem.IsWindows() ? path.ToLowerInvariant() : path;
    public static string relpath(string path, string start = ".") => System.IO.Path.GetRelativePath(start, path);
    public static string expanduser(string path)
    {
        var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        if (path == "~") return home;
        if (path.StartsWith("~/", System.StringComparison.Ordinal) || path.StartsWith("~\\", System.StringComparison.Ordinal))
            return System.IO.Path.Combine(home, path[2..]);
        return path;
    }

    public static string expandvars(string path) =>
        System.Text.RegularExpressions.Regex.Replace(
            path,
            @"\$(\w+)|%([^%]+)%",
            m =>
            {
                var key = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                return System.Environment.GetEnvironmentVariable(key) ?? m.Value;
            });

    public static string join(params string[] parts) => JoinPaths(parts);

    public static readonly PathApi path = new();

    private static string JoinPaths(string[] parts)
    {
        if (parts.Length == 0)
            throw new System.ArgumentException("join() requires at least one path");

        var result = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            result = System.IO.Path.IsPathRooted(parts[i])
                ? parts[i]
                : System.IO.Path.Combine(result, parts[i]);
        }
        return result;
    }

    public static System.Collections.Generic.List<SharpThonOsWalkEntry> walk(string top)
    {
        var result = new System.Collections.Generic.List<SharpThonOsWalkEntry>();
        foreach (var directory in System.IO.Directory.EnumerateDirectories(top, "*", System.IO.SearchOption.AllDirectories).Prepend(top))
        {
            result.Add(new SharpThonOsWalkEntry(
                directory,
                System.IO.Directory.GetDirectories(directory)
                    .Select(x => System.IO.Path.GetFileName(x) ?? "")
                    .Where(x => x.Length > 0)
                    .ToArray(),
                System.IO.Directory.GetFiles(directory)
                    .Select(x => System.IO.Path.GetFileName(x) ?? "")
                    .Where(x => x.Length > 0)
                    .ToArray()));
        }
        return result;
    }

    public sealed class PathApi
    {
        public bool exists(string value) => System.IO.File.Exists(value) || System.IO.Directory.Exists(value);
        public bool lexists(string value) => exists(value);
        public bool isfile(string value) => System.IO.File.Exists(value);
        public bool isdir(string value) => System.IO.Directory.Exists(value);
        public bool isabs(string value) => System.IO.Path.IsPathRooted(value);
        public bool islink(string value) =>
            System.IO.File.Exists(value) &&
            (System.IO.File.GetAttributes(value) & System.IO.FileAttributes.ReparsePoint) != 0;
        public string join(params string[] parts) => SharpThonOs.JoinPaths(parts);
        public string dirname(string value) => System.IO.Path.GetDirectoryName(value) ?? "";
        public string basename(string value) => System.IO.Path.GetFileName(value) ?? "";
        public string abspath(string value) => System.IO.Path.GetFullPath(value);
        public string realpath(string value) => RealPath(value);
        public string normpath(string value) => NormalizePath(value);
        public string normcase(string value) => System.OperatingSystem.IsWindows() ? value.ToLowerInvariant() : value;
        public string relpath(string value, string start = ".") => System.IO.Path.GetRelativePath(start, value);
        public string[] split(string value)
        {
            return new[] { dirname(value), basename(value) };
        }
        public string[] splitext(string value)
        {
            var extension = System.IO.Path.GetExtension(value);
            return new[] { value[..(value.Length - extension.Length)], extension };
        }

        public static string RealPath(string value)
        {
            var full = System.IO.Path.GetFullPath(value);

            if (!System.OperatingSystem.IsWindows())
            {
                try
                {
                    var ptr = realpath_native(full, System.IntPtr.Zero);
                    if (ptr != System.IntPtr.Zero)
                    {
                        var resolved = System.Runtime.InteropServices.Marshal.PtrToStringAnsi(ptr);
                        free_native(ptr);
                        if (!string.IsNullOrEmpty(resolved))
                            return resolved;
                    }
                }
                catch { }
            }

            try
            {
                if (System.IO.File.Exists(full))
                    return new System.IO.FileInfo(full).ResolveLinkTarget(true)?.FullName ?? full;
                if (System.IO.Directory.Exists(full))
                    return new System.IO.DirectoryInfo(full).ResolveLinkTarget(true)?.FullName ?? full;
            }
            catch { }
            return full;
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "realpath", CharSet = System.Runtime.InteropServices.CharSet.Ansi)]
        private static extern System.IntPtr realpath_native(string path, System.IntPtr resolvedPath);

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "free")]
        private static extern void free_native(System.IntPtr ptr);

        public static string NormalizePath(string value)
        {
            if (string.IsNullOrEmpty(value)) return ".";

            var input = value.Replace('\\', '/');
            var drive = input.Length >= 2 && input[1] == ':';
            var rooted = input.StartsWith("/", StringComparison.Ordinal) || drive;
            var prefix = drive ? input[..2] : (rooted ? "/" : "");
            var remainder = drive ? input[2..] : input;
            var stack = new List<string>();

            foreach (var part in remainder.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part == ".") continue;
                if (part == "..")
                {
                    if (stack.Count > 0 && stack[^1] != "..")
                        stack.RemoveAt(stack.Count - 1);
                    else if (!rooted)
                        stack.Add("..");
                    continue;
                }
                stack.Add(part);
            }

            var separator = System.IO.Path.DirectorySeparatorChar;
            var result = prefix + string.Join(separator, stack);

            if (drive)
            {
                if (stack.Count == 0) return prefix + separator;
                result = prefix + separator + string.Join(separator, stack);
                return result;
            }

            if (result.Length == 0)
                return rooted ? separator.ToString() : ".";

            return result;
        }
    }
}

public sealed class SharpThonOsWalkEntry
{
    public SharpThonOsWalkEntry(string root, string[] dirs, string[] files)
    {
        this.root = root;
        this.dirs = dirs;
        this.files = files;
    }

    public string root { get; }
    public string[] dirs { get; }
    public string[] files { get; }
}

public sealed class SharpThonOsStat
{
    private readonly System.IO.FileInfo info;

    public SharpThonOsStat(string path)
    {
        info = new System.IO.FileInfo(path);
    }

    public long st_size => info.Length;
    public double st_mtime => (info.LastWriteTimeUtc - System.DateTime.UnixEpoch).TotalSeconds;
    public double st_ctime => (info.CreationTimeUtc - System.DateTime.UnixEpoch).TotalSeconds;
    public long st_mode => (long)info.Attributes;
}
""";

    private const string JsonBody = """
public static class SharpThonJson
{
    public static dynamic loads(string text)
    {
        using var document = System.Text.Json.JsonDocument.Parse(text);
        return (dynamic)ConvertElement(document.RootElement)!;
    }

    public static dynamic load(string path) => loads(System.IO.File.ReadAllText(path));

    // Python json.dumps defaults to separators=(', ', ': ') and ensure_ascii=True.
    public static string dumps(dynamic value, int indent = -1) => SerializePythonJson(value, indent, 0);

    public static void dump(dynamic value, string path, int indent = -1) =>
        System.IO.File.WriteAllText(path, dumps(value, indent));

    private static string SerializePythonJson(object? value, int indent, int depth)
    {
        if (value is null) return "null";
        if (value is string text) return Quote(text);
        if (value is bool b) return b ? "true" : "false";

        if (value is byte || value is short || value is int || value is long ||
            value is System.Numerics.BigInteger)
            return value.ToString() ?? "0";

        if (value is float f)
            return FormatFloat(f);
        if (value is double d)
            return FormatFloat(d);
        if (value is decimal dec)
            return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (value is System.Collections.IDictionary dictionary)
        {
            var entries = new List<string>();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
            {
                var key = entry.Key?.ToString() ?? "null";
                entries.Add(Quote(key) + ": " + SerializePythonJson(entry.Value, indent, depth + 1));
            }
            return FormatContainer("{", "}", entries, indent, depth);
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
                items.Add(SerializePythonJson(item, indent, depth + 1));
            return FormatContainer("[", "]", items, indent, depth);
        }

        return Quote(value.ToString() ?? "");
    }

    private static string FormatContainer(string open, string close, List<string> items, int indent, int depth)
    {
        if (items.Count == 0) return open + close;
        if (indent < 0) return open + string.Join(", ", items) + close;

        var pad = new string(' ', (depth + 1) * indent);
        var closePad = new string(' ', depth * indent);
        return open + "\n" + pad + string.Join(",\n" + pad, items) + "\n" + closePad + close;
    }

    private static string FormatFloat(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        var text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (!text.Contains('.') && !text.Contains('E') && !text.Contains('e'))
            text += ".0";
        return text;
    }

    private static string Quote(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length + 2);
        builder.Append('"');
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < 0x20 || ch > 0x7f)
                        builder.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    else
                        builder.Append(ch);
                    break;
            }
        }
        builder.Append('"');
        return builder.ToString();
    }

    private static object? ConvertElement(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
            {
                var result = new System.Collections.Generic.Dictionary<string, object?>();
                foreach (var property in element.EnumerateObject())
                    result[property.Name] = ConvertElement(property.Value);
                return result;
            }
            case System.Text.Json.JsonValueKind.Array:
            {
                var result = new System.Collections.Generic.List<object?>();
                foreach (var item in element.EnumerateArray())
                    result.Add(ConvertElement(item));
                return result;
            }
            case System.Text.Json.JsonValueKind.String:
                return element.GetString() ?? "";
            case System.Text.Json.JsonValueKind.Number:
                if (element.TryGetInt32(out var i)) return i;
                if (element.TryGetInt64(out var l)) return l;
                return element.GetDouble();
            case System.Text.Json.JsonValueKind.True:
                return true;
            case System.Text.Json.JsonValueKind.False:
                return false;
            case System.Text.Json.JsonValueKind.Null:
                return null;
            default:
                return element.ToString();
        }
    }
}
""";

    private const string ReBody = """
public static class SharpThonRe
{
    // Python re.ASCII: .NET ECMAScript gives ASCII-oriented character classes.
    public const int A = (int)System.Text.RegularExpressions.RegexOptions.ECMAScript;
    public const int I = (int)System.Text.RegularExpressions.RegexOptions.IgnoreCase;
    public const int M = (int)System.Text.RegularExpressions.RegexOptions.Multiline;
    public const int S = (int)System.Text.RegularExpressions.RegexOptions.Singleline;
    public const int X = (int)System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace;

    public static SharpThonRegexMatch? match(string pattern, string text, int flags = 0) =>
        Wrap(new System.Text.RegularExpressions.Regex("\\A(?:" + pattern + ")", Options(flags)).Match(text));

    public static SharpThonRegexMatch? search(string pattern, string text, int flags = 0) =>
        Wrap(new System.Text.RegularExpressions.Regex(pattern, Options(flags)).Match(text));

    public static SharpThonRegexMatch? fullmatch(string pattern, string text, int flags = 0) =>
        Wrap(new System.Text.RegularExpressions.Regex("\\A(?:" + pattern + ")\\z", Options(flags)).Match(text));

    public static SharpThonCompiledRegex compile(string pattern, int flags = 0) =>
        new(pattern, flags);

    public static System.Collections.Generic.List<string> findall(string pattern, string text, int flags = 0) =>
        new System.Text.RegularExpressions.Regex(pattern, Options(flags))
            .Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .ToList();

    public static System.Collections.Generic.List<SharpThonRegexMatch> finditer(string pattern, string text, int flags = 0) =>
        new System.Text.RegularExpressions.Regex(pattern, Options(flags))
            .Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => new SharpThonRegexMatch(m))
            .ToList();

    public static string sub(string pattern, string replacement, string text, int count = 0, int flags = 0)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern, Options(flags));
        var convertedReplacement = replacement.Replace("\\g<", "${");
        return count > 0
            ? regex.Replace(text, convertedReplacement, count)
            : regex.Replace(text, convertedReplacement);
    }

    public static string[] split(string pattern, string text, int maxsplit = 0, int flags = 0)
    {
        var regex = new System.Text.RegularExpressions.Regex(pattern, Options(flags));
        return maxsplit > 0 ? regex.Split(text, maxsplit) : regex.Split(text);
    }

    public static bool ismatch(string pattern, string text, int flags = 0) =>
        match(pattern, text, flags) != null;

    public static string escape(string text) => System.Text.RegularExpressions.Regex.Escape(text);

    private static SharpThonRegexMatch? Wrap(System.Text.RegularExpressions.Match match) =>
        match.Success ? new SharpThonRegexMatch(match) : null;

    private static System.Text.RegularExpressions.RegexOptions Options(int flags) =>
        (System.Text.RegularExpressions.RegexOptions)flags;
}

public sealed class SharpThonCompiledRegex
{
    private readonly System.Text.RegularExpressions.Regex regex;
    private readonly System.Text.RegularExpressions.Regex matchRegex;

    public SharpThonCompiledRegex(string pattern, int flags = 0)
    {
        var options = (System.Text.RegularExpressions.RegexOptions)flags;
        regex = new System.Text.RegularExpressions.Regex(pattern, options);
        matchRegex = new System.Text.RegularExpressions.Regex("\\A(?:" + pattern + ")", options);
    }

    public SharpThonRegexMatch? match(string text)
    {
        var result = matchRegex.Match(text);
        return result.Success ? new SharpThonRegexMatch(result) : null;
    }

    public SharpThonRegexMatch? search(string text)
    {
        var result = regex.Match(text);
        return result.Success ? new SharpThonRegexMatch(result) : null;
    }

    public System.Collections.Generic.List<string> findall(string text) =>
        regex.Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => m.Value)
            .ToList();

    public System.Collections.Generic.List<SharpThonRegexMatch> finditer(string text) =>
        regex.Matches(text)
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => new SharpThonRegexMatch(m))
            .ToList();

    public string sub(string replacement, string text, int count = 0) =>
        count > 0 ? regex.Replace(text, replacement, count) : regex.Replace(text, replacement);

    public string[] split(string text, int maxsplit = 0) =>
        maxsplit > 0 ? regex.Split(text, maxsplit) : regex.Split(text);
}

public sealed class SharpThonRegexMatch
{
    private readonly System.Text.RegularExpressions.Match match;

    public SharpThonRegexMatch(System.Text.RegularExpressions.Match match)
    {
        this.match = match;
    }

    public bool success => match.Success;
    public string group(int index = 0) => match.Groups[index].Value;
    public int start(int index = 0) => match.Groups[index].Index;
    public int end(int index = 0) => match.Groups[index].Index + match.Groups[index].Length;
    public int[] span(int index = 0) => new[] { start(index), end(index) };
    public string[] groups => match.Groups.Cast<System.Text.RegularExpressions.Group>().Skip(1).Select(g => g.Value).ToArray();
    public System.Collections.Generic.Dictionary<string, string> groupdict =>
        match.Groups.Cast<System.Text.RegularExpressions.Group>()
            .Where(g => !int.TryParse(g.Name, out _))
            .ToDictionary(g => g.Name, g => g.Value);
}
""";

    private const string SysBody = """
public static class SharpThonSys
{
    public static string[] argv => System.Environment.GetCommandLineArgs();
    public static string version => System.Environment.Version.ToString();
    public static string platform => System.OperatingSystem.IsWindows() ? "win32" : "linux";
    public static string executable => System.Environment.ProcessPath ?? "";
    public static string prefix => System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
    public static string base_prefix => prefix;
    public static string exec_prefix => prefix;
    public static string implementation => "dotnet";
    public static string byteorder => System.BitConverter.IsLittleEndian ? "little" : "big";
    public static long maxsize => long.MaxValue;
    public static string filesystemencoding => System.Text.Encoding.UTF8.WebName;
    public static string newline => System.Environment.NewLine;
    public static int max_threads => System.Environment.ProcessorCount;
    public static System.Collections.Generic.List<string> path =>
        new() { System.Environment.CurrentDirectory };

    public static string? getenv(string name, string? defaultValue = null) =>
        System.Environment.GetEnvironmentVariable(name) ?? defaultValue;

    public static void exit(int code = 0) => System.Environment.Exit(code);
    public static void abort() => System.Environment.FailFast("sys.abort()");
    public static void setprofile(object? callback) { }
    public static void settrace(object? callback) { }
    public static long getsizeof(object? value) => value switch
    {
        null => 0,
        string text => text.Length * 2L,
        Array array => array.Length * 8L,
        _ => 8L
    };
}
""";

    private const string RandomBody = """
public static class SharpThonRandom
{
    // CPython's random module uses MT19937. Keeping the same core generator,
    // seeding and _randbelow algorithm makes deterministic integer seeds match Python.
    private const int N = 624;
    private const int M = 397;
    private const uint MATRIX_A = 0x9908B0DFU;
    private const uint UPPER_MASK = 0x80000000U;
    private const uint LOWER_MASK = 0x7FFFFFFFU;

    private static readonly uint[] _state = new uint[N];
    private static int _index = N;
    private static double? _gaussNext;

    public static void seed() => SeedFromEntropy();
    public static void seed(int seedValue) => SeedInteger(new System.Numerics.BigInteger(seedValue));
    public static void seed(long seedValue) => SeedInteger(new System.Numerics.BigInteger(seedValue));
    public static void seed(string seedValue) => SeedPythonBytes(System.Text.Encoding.UTF8.GetBytes(seedValue));
    public static void seed(byte[] seedValue) => SeedPythonBytes(seedValue);

    public static double random()
    {
        var a = GenRandUInt32() >> 5;
        var b = GenRandUInt32() >> 6;
        return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
    }

    public static double uniform(double a, double b) => a + (b - a) * random();

    public static int randint(int a, int b)
    {
        if (b < a) throw new System.ArgumentException($"empty range in randint({a}, {b})");
        return checked(a + (int)RandBelow((long)b - a + 1));
    }

    public static int randrange(int stop)
    {
        if (stop <= 0) throw new System.ArgumentException($"empty range for randrange({stop})");
        return checked((int)RandBelow(stop));
    }

    public static int randrange(int start, int stop) => randrange(start, stop, 1);

    public static int randrange(int start, int stop, int step)
    {
        if (step == 0) throw new System.ArgumentException("zero step for randrange()");
        long width = (long)stop - start;
        long n = step > 0
            ? (width > 0 ? (width + step - 1) / step : 0)
            : (width < 0 ? (width + step + 1) / step : 0);
        if (n <= 0) throw new System.ArgumentException($"empty range in randrange({start}, {stop}, {step})");
        return checked(start + (int)(step * RandBelow(n)));
    }

    public static T choice<T>(System.Collections.Generic.IList<T> sequence)
    {
        if (sequence.Count == 0) throw new System.IndexOutOfRangeException("Cannot choose from an empty sequence");
        return sequence[(int)RandBelow(sequence.Count)];
    }

    public static System.Collections.Generic.List<T> choices<T>(System.Collections.Generic.IList<T> population, int k = 1)
    {
        if (population.Count == 0) throw new System.IndexOutOfRangeException("Cannot choose from an empty sequence");
        if (k < 0) throw new System.ArgumentOutOfRangeException(nameof(k));
        var result = new System.Collections.Generic.List<T>(k);
        var n = population.Count;
        for (var i = 0; i < k; i++)
            result.Add(population[(int)System.Math.Floor(random() * n)]);
        return result;
    }

    public static System.Collections.Generic.List<T> sample<T>(System.Collections.Generic.IList<T> population, int k)
    {
        var n = population.Count;
        if (k < 0 || k > n) throw new System.ArgumentException("Sample larger than population or is negative");

        var result = new System.Collections.Generic.List<T>(k);
        if (n <= 21 || k <= 5)
        {
            var pool = population.ToList();
            for (var i = 0; i < k; i++)
            {
                var j = (int)RandBelow(n - i);
                result.Add(pool[j]);
                pool[j] = pool[n - i - 1];
            }
            return result;
        }

        var selected = new System.Collections.Generic.HashSet<int>();
        for (var i = 0; i < k; i++)
        {
            var j = (int)RandBelow(n);
            while (!selected.Add(j)) j = (int)RandBelow(n);
            result.Add(population[j]);
        }
        return result;
    }

    public static void shuffle<T>(System.Collections.Generic.IList<T> sequence)
    {
        for (var i = sequence.Count - 1; i > 0; i--)
        {
            var j = (int)RandBelow(i + 1);
            (sequence[i], sequence[j]) = (sequence[j], sequence[i]);
        }
    }

    public static System.Numerics.BigInteger getrandbits(int k)
    {
        if (k < 0) throw new System.ArgumentOutOfRangeException(nameof(k));
        if (k == 0) return System.Numerics.BigInteger.Zero;

        var words = (k + 31) / 32;
        var result = System.Numerics.BigInteger.Zero;
        for (var i = 0; i < words; i++)
        {
            var bits = System.Math.Min(32, k - i * 32);
            var word = GenRandUInt32();
            if (bits < 32) word >>= 32 - bits;
            result |= new System.Numerics.BigInteger(word) << (i * 32);
        }
        return result;
    }

    public static byte[] randbytes(int n)
    {
        if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n));
        var value = getrandbits(checked(n * 8));
        var result = new byte[n];
        for (var i = 0; i < n; i++)
            result[i] = (byte)((value >> (8 * i)) & 0xFF);
        return result;
    }

    public static double triangular(double low = 0.0, double high = 1.0, double mode = double.NaN)
    {
        var u = random();
        if (high == low) return low;
        var c = double.IsNaN(mode) ? 0.5 : (mode - low) / (high - low);
        if (u > c)
        {
            u = 1.0 - u;
            c = 1.0 - c;
            (low, high) = (high, low);
        }
        return low + (high - low) * System.Math.Sqrt(u * c);
    }

    public static double gauss(double mu = 0.0, double sigma = 1.0)
    {
        if (_gaussNext.HasValue)
        {
            var cached = _gaussNext.Value;
            _gaussNext = null;
            return mu + cached * sigma;
        }

        var x2pi = random() * (2.0 * System.Math.PI);
        var g2rad = System.Math.Sqrt(-2.0 * System.Math.Log(1.0 - random()));
        var z = System.Math.Cos(x2pi) * g2rad;
        _gaussNext = System.Math.Sin(x2pi) * g2rad;
        return mu + z * sigma;
    }

    public static double normalvariate(double mu = 0.0, double sigma = 1.0)
    {
        var NV_MAGICCONST = 4.0 * System.Math.Exp(-0.5) / System.Math.Sqrt(2.0);
        while (true)
        {
            var u1 = random();
            var u2 = 1.0 - random();
            var z = NV_MAGICCONST * (u1 - 0.5) / u2;
            var zz = z * z / 4.0;
            if (zz <= -System.Math.Log(u2))
                return mu + z * sigma;
        }
    }

    public static double lognormvariate(double mu = 0.0, double sigma = 1.0) => System.Math.Exp(normalvariate(mu, sigma));
    public static double expovariate(double lambd = 1.0) => -System.Math.Log(1.0 - random()) / lambd;

    private static void SeedPythonBytes(byte[] data)
    {
        var digest = System.Security.Cryptography.SHA512.HashData(data);
        var combined = new byte[data.Length + digest.Length];
        System.Buffer.BlockCopy(data, 0, combined, 0, data.Length);
        System.Buffer.BlockCopy(digest, 0, combined, data.Length, digest.Length);
        SeedInteger(new System.Numerics.BigInteger(combined, isUnsigned: true, isBigEndian: false));
    }

    private static void SeedInteger(System.Numerics.BigInteger value)
    {
        _gaussNext = null;
        if (value < 0) value = System.Numerics.BigInteger.Negate(value);

        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        if (bytes.Length == 0) bytes = new byte[] { 0 };
        var words = (bytes.Length + 3) / 4;
        var key = new uint[words];
        for (var i = 0; i < bytes.Length; i++)
            key[i / 4] |= (uint)bytes[i] << ((i % 4) * 8);
        InitByArray(key);
    }

    private static void SeedFromEntropy()
    {
        var bytes = new byte[N * 4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var key = new uint[N];
        for (var i = 0; i < N; i++)
            key[i] = BitConverter.ToUInt32(bytes, i * 4);
        InitByArray(key);
    }

    private static void InitGenRand(uint seed)
    {
        _state[0] = seed;
        for (var i = 1; i < N; i++)
            _state[i] = unchecked(1812433253U * (_state[i - 1] ^ (_state[i - 1] >> 30)) + (uint)i);
        _index = N;
    }

    private static void InitByArray(uint[] key)
    {
        InitGenRand(19650218U);
        var i = 1;
        var j = 0;
        var k = System.Math.Max(N, key.Length);
        for (var count = 0; count < k; count++)
        {
            _state[i] = unchecked((_state[i] ^ ((_state[i - 1] ^ (_state[i - 1] >> 30)) * 1664525U)) + key[j] + (uint)j);
            i++;
            j++;
            if (i >= N) { _state[0] = _state[N - 1]; i = 1; }
            if (j >= key.Length) j = 0;
        }

        for (k = N - 1; k > 0; k--)
        {
            _state[i] = unchecked((_state[i] ^ ((_state[i - 1] ^ (_state[i - 1] >> 30)) * 1566083941U)) - (uint)i);
            i++;
            if (i >= N) { _state[0] = _state[N - 1]; i = 1; }
        }

        _state[0] = 0x80000000U;
        _index = N;
        _gaussNext = null;
    }

    private static uint GenRandUInt32()
    {
        if (_index >= N)
        {
            for (var kk = 0; kk < N - M; kk++)
            {
                var y = (_state[kk] & UPPER_MASK) | (_state[kk + 1] & LOWER_MASK);
                _state[kk] = _state[kk + M] ^ (y >> 1) ^ ((y & 1U) != 0 ? MATRIX_A : 0U);
            }
            for (var kk = N - M; kk < N - 1; kk++)
            {
                var y = (_state[kk] & UPPER_MASK) | (_state[kk + 1] & LOWER_MASK);
                _state[kk] = _state[kk + (M - N)] ^ (y >> 1) ^ ((y & 1U) != 0 ? MATRIX_A : 0U);
            }
            var lastY = (_state[N - 1] & UPPER_MASK) | (_state[0] & LOWER_MASK);
            _state[N - 1] = _state[M - 1] ^ (lastY >> 1) ^ ((lastY & 1U) != 0 ? MATRIX_A : 0U);
            _index = 0;
        }

        var y2 = _state[_index++];
        y2 ^= y2 >> 11;
        y2 ^= (y2 << 7) & 0x9D2C5680U;
        y2 ^= (y2 << 15) & 0xEFC60000U;
        y2 ^= y2 >> 18;
        return y2;
    }

    private static long RandBelow(long n)
    {
        if (n <= 0) throw new System.ArgumentException("Upper bound must be greater than zero");
        var k = 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)(n - 1));
        while (true)
        {
            var r = (long)getrandbits(k);
            if (r < n) return r;
        }
    }
}
""";

    private const string MathBody = """
public static class SharpThonMath
{
    public const double pi = System.Math.PI;
    public const double e = System.Math.E;
    public const double tau = 2.0 * System.Math.PI;
    public const double inf = double.PositiveInfinity;
    public const double nan = double.NaN;

    public static double acos(double value) => System.Math.Acos(value);
    public static double acosh(double value) => System.Math.Acosh(value);
    public static double asin(double value) => System.Math.Asin(value);
    public static double cos(double value) => System.Math.Cos(value);
    public static double asinh(double value) => System.Math.Asinh(value);
    public static double atan(double value) => System.Math.Atan(value);
    public static double atan2(double y, double x) => System.Math.Atan2(y, x);
    public static double atanh(double value) => System.Math.Atanh(value);
    public static double cbrt(double value) => System.Math.Cbrt(value);
    public static double ceil(double value) => System.Math.Ceiling(value);
    public static System.Numerics.BigInteger comb(int n, int k)
    {
        if (n < 0 || k < 0 || k > n) throw new System.ArgumentException("Invalid values for comb()");
        k = System.Math.Min(k, n - k);
        System.Numerics.BigInteger result = 1;
        for (var i = 1; i <= k; i++) result = result * (n - k + i) / i;
        return result;
    }
    public static double copysign(double x, double y) => System.Math.CopySign(x, y);
    public static double degrees(double value) => value * (180.0 / pi);
    public static double erf(double value) => Erf(value);
    public static double erfc(double value) => 1.0 - Erf(value);
    public static double exp(double value) => System.Math.Exp(value);
    public static double exp2(double value) => System.Math.Pow(2.0, value);
    public static double fabs(double value) => System.Math.Abs(value);

    public static System.Numerics.BigInteger factorial(int value)
    {
        if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(value));
        System.Numerics.BigInteger result = 1;
        for (var i = 2; i <= value; i++) result *= i;
        return result;
    }

    public static double floor(double value) => System.Math.Floor(value);
    public static double fmod(double x, double y) => x % y;

    public static bool isclose(double a, double b, double rel_tol = 1e-09, double abs_tol = 0.0) =>
        System.Math.Abs(a - b) <= System.Math.Max(rel_tol * System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)), abs_tol);

    public static double dist(double[] p, double[] q)
    {
        if (p.Length != q.Length) throw new System.ArgumentException("Dimension mismatch");
        var sum = 0.0;
        for (var i = 0; i < p.Length; i++)
            sum += (p[i] - q[i]) * (p[i] - q[i]);
        return System.Math.Sqrt(sum);
    }

    public static double fsum(double[] values) => values.Sum();
    public static double prod(double[] values, double start = 1.0)
    {
        var result = start;
        foreach (var value in values) result *= value;
        return result;
    }

    public static System.Numerics.BigInteger gcd(System.Numerics.BigInteger a, System.Numerics.BigInteger b) => Gcd(a, b);
    public static System.Numerics.BigInteger lcm(System.Numerics.BigInteger a, System.Numerics.BigInteger b)
    {
        if (a.IsZero || b.IsZero) return System.Numerics.BigInteger.Zero;
        return System.Numerics.BigInteger.Abs((a / Gcd(a, b)) * b);
    }
    public static double hypot(double x, double y)
    {
        x = System.Math.Abs(x);
        y = System.Math.Abs(y);
        if (double.IsInfinity(x) || double.IsInfinity(y)) return double.PositiveInfinity;
        if (x < y) (x, y) = (y, x);
        if (x == 0.0) return 0.0;
        var r = y / x;
        return x * System.Math.Sqrt(1.0 + r * r);
    }
    public static bool isfinite(double value) => double.IsFinite(value);
    public static bool isinf(double value) => double.IsInfinity(value);
    public static bool isnan(double value) => double.IsNaN(value);
    public static long isqrt(long value)
    {
        if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(value));
        if (value < 2) return value;

        var root = (long)System.Math.Sqrt(value);
        while (root > value / root) root--;
        while (root < long.MaxValue && (root + 1) <= value / (root + 1)) root++;
        return root;
    }
    public static double ldexp(double value, int exponent) => value * System.Math.Pow(2.0, exponent);
    public static double log(double value, double new_base = 0.0) => new_base == 0.0 ? System.Math.Log(value) : System.Math.Log(value, new_base);
    public static double log10(double value) => System.Math.Log10(value);
    public static double log1p(double value) => System.Math.Log(1.0 + value);
    public static double log2(double value) => System.Math.Log2(value);
    public static double pow(double x, double y) => System.Math.Pow(x, y);
    public static double radians(double value) => value * (pi / 180.0);
    public static double remainder(double x, double y) => System.Math.IEEERemainder(x, y);
    public static double sin(double value) => System.Math.Sin(value);
    public static double sinh(double value) => System.Math.Sinh(value);
    public static double sqrt(double value) => System.Math.Sqrt(value);
    public static double tan(double value) => System.Math.Tan(value);
    public static double tanh(double value) => System.Math.Tanh(value);
    public static double trunc(double value) => System.Math.Truncate(value);

    public static double[] frexp(double value)
    {
        if (value == 0.0 || double.IsNaN(value) || double.IsInfinity(value))
            return new[] { value, 0.0 };

        var exponent = System.Math.ILogB(System.Math.Abs(value)) + 1;
        var mantissa = System.Math.ScaleB(value, -exponent);
        return new[] { mantissa, (double)exponent };
    }

    public static double[] modf(double value)
    {
        if (double.IsNaN(value)) return new[] { double.NaN, double.NaN };
        if (double.IsInfinity(value)) return new[] { System.Math.CopySign(0.0, value), value };
        var integer = System.Math.Truncate(value);
        return new[] { value - integer, integer };
    }

    private static System.Numerics.BigInteger Gcd(System.Numerics.BigInteger a, System.Numerics.BigInteger b)
    {
        a = System.Numerics.BigInteger.Abs(a);
        b = System.Numerics.BigInteger.Abs(b);
        while (b != 0) (a, b) = (b, a % b);
        return a;
    }

    private static double Erf(double x)
    {
        var sign = x < 0 ? -1.0 : 1.0;
        x = System.Math.Abs(x);
        var t = 1.0 / (1.0 + 0.3275911 * x);
        var y = 1.0 - (((((1.061405429 * t - 1.453152027) * t + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t) * System.Math.Exp(-x * x);
        return sign * y;
    }
}
""";

    private const string TimeFormattingBody = """
public static class SharpThonTimeFormatting
{
    public static string Format(string format, System.DateTimeOffset value)
    {
        var result = new System.Text.StringBuilder();

        for (var i = 0; i < format.Length; i++)
        {
            if (format[i] != '%' || i + 1 >= format.Length)
            {
                result.Append(format[i]);
                continue;
            }

            var code = format[++i];
            result.Append(code switch
            {
                '%' => "%",
                'Y' => value.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
                'y' => value.ToString("yy", System.Globalization.CultureInfo.InvariantCulture),
                'm' => value.ToString("MM", System.Globalization.CultureInfo.InvariantCulture),
                'd' => value.ToString("dd", System.Globalization.CultureInfo.InvariantCulture),
                'e' => value.Day.ToString().PadLeft(2, ' '),
                'H' => value.ToString("HH", System.Globalization.CultureInfo.InvariantCulture),
                'I' => value.ToString("hh", System.Globalization.CultureInfo.InvariantCulture),
                'M' => value.ToString("mm", System.Globalization.CultureInfo.InvariantCulture),
                'S' => value.ToString("ss", System.Globalization.CultureInfo.InvariantCulture),
                'f' => value.ToString("ffffff", System.Globalization.CultureInfo.InvariantCulture),
                'j' => value.DayOfYear.ToString("000", System.Globalization.CultureInfo.InvariantCulture),
                'w' => (((int)value.DayOfWeek)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                'u' => (((int)value.DayOfWeek + 6) % 7 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                'a' => value.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture),
                'A' => value.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture),
                'b' => value.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture),
                'B' => value.ToString("MMMM", System.Globalization.CultureInfo.InvariantCulture),
                'p' => value.ToString("tt", System.Globalization.CultureInfo.InvariantCulture),
                'z' => value.ToString("zzz", System.Globalization.CultureInfo.InvariantCulture).Replace(":", ""),
                'Z' => System.TimeZoneInfo.Local.IsDaylightSavingTime(value)
                    ? System.TimeZoneInfo.Local.DaylightName
                    : System.TimeZoneInfo.Local.StandardName,
                'x' => value.ToString("MM/dd/yy", System.Globalization.CultureInfo.InvariantCulture),
                'X' => value.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                'c' => value.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture),
                _ => "%" + code
            });
        }

        return result.ToString();
    }
}
""";

    private const string TimeBody = """
public static class SharpThonTime
{
    public static double time() =>
        (System.DateTimeOffset.UtcNow - System.DateTimeOffset.UnixEpoch).TotalSeconds;

    public static long time_ns()
    {
        var ticks = System.DateTime.UtcNow.Ticks - System.DateTime.UnixEpoch.Ticks;
        var seconds = ticks / System.TimeSpan.TicksPerSecond;
        var remainder = ticks % System.TimeSpan.TicksPerSecond;
        return checked(seconds * 1_000_000_000L + (remainder * 100L));
    }

    public static double monotonic() =>
        System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;

    public static long monotonic_ns()
    {
        var ticks = System.Diagnostics.Stopwatch.GetTimestamp();
        var frequency = System.Diagnostics.Stopwatch.Frequency;
        var seconds = ticks / frequency;
        var remainder = ticks % frequency;
        return checked(seconds * 1_000_000_000L + (remainder * 1_000_000_000L) / frequency);
    }

    public static double perf_counter() => monotonic();
    public static long perf_counter_ns() => monotonic_ns();
    public static double process_time() => System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.TotalSeconds;
    public static long process_time_ns() => System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime.Ticks * 100L;

    public static string ctime(double seconds = -1)
    {
        var value = seconds < 0
            ? System.DateTimeOffset.Now
            : System.DateTimeOffset.UnixEpoch.AddSeconds(seconds).ToLocalTime();
        return value.ToString("ddd MMM dd HH:mm:ss yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static SharpThonTimeStruct localtime(double seconds = -1) =>
        new(seconds < 0 ? System.DateTimeOffset.Now : System.DateTimeOffset.UnixEpoch.AddSeconds(seconds).ToLocalTime());

    public static SharpThonTimeStruct gmtime(double seconds = -1) =>
        new(seconds < 0 ? System.DateTimeOffset.UtcNow : System.DateTimeOffset.UnixEpoch.AddSeconds(seconds));

    public static void sleep(double seconds)
    {
        if (seconds < 0) throw new System.ArgumentOutOfRangeException(nameof(seconds));
        System.Threading.Thread.Sleep(System.TimeSpan.FromSeconds(seconds));
    }

    public static string strftime(string format, SharpThonTimeStruct value) =>
        SharpThonTimeFormatting.Format(format, value.value);

    public static void sleep_ms(long milliseconds)
    {
        if (milliseconds < 0) throw new System.ArgumentOutOfRangeException(nameof(milliseconds));
        System.Threading.Thread.Sleep(System.TimeSpan.FromMilliseconds(milliseconds));
    }

    public static double mktime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
    {
        var local = new System.DateTime(year, month, day, hour, minute, second, System.DateTimeKind.Local);
        return new System.DateTimeOffset(local).ToUnixTimeSeconds();
    }
}

public sealed class SharpThonTimeStruct
{
    internal readonly System.DateTimeOffset value;

    public SharpThonTimeStruct(System.DateTimeOffset value)
    {
        this.value = value;
    }

    public int tm_year => value.Year;
    public int tm_mon => value.Month;
    public int tm_mday => value.Day;
    public int tm_hour => value.Hour;
    public int tm_min => value.Minute;
    public int tm_sec => value.Second;
    public int tm_wday => ((int)value.DayOfWeek + 6) % 7;
    public int tm_yday => value.DayOfYear;
    public int tm_isdst => System.TimeZoneInfo.Local.IsDaylightSavingTime(value) ? 1 : 0;
}
""";
}
