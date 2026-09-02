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
    public static string realpath(string path) => System.IO.Path.GetFullPath(path);
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
        public string realpath(string value) => System.IO.Path.GetFullPath(value);
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

    public static string dumps(dynamic value, int indent = -1)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = indent >= 0
        };
        return System.Text.Json.JsonSerializer.Serialize(Normalize(value), options);
    }

    public static void dump(dynamic value, string path, int indent = -1) =>
        System.IO.File.WriteAllText(path, dumps(value, indent));

    private static object? Normalize(object? value)
    {
        if (value is null ||
            value is string ||
            value is bool ||
            value is byte || value is short || value is int || value is long ||
            value is float || value is double || value is decimal)
            return value;

        if (value is System.Collections.IDictionary dictionary)
        {
            var result = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (System.Collections.DictionaryEntry entry in dictionary)
                result[entry.Key?.ToString() ?? "null"] = Normalize(entry.Value);
            return result;
        }

        if (value is System.Collections.IEnumerable enumerable)
        {
            var result = new System.Collections.Generic.List<object?>();
            foreach (var item in enumerable)
                result.Add(Normalize(item));
            return result;
        }

        return value;
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
    private static System.Random _random = new();

    public static void seed() => _random = new System.Random();
    public static void seed(int seedValue) => _random = new System.Random(seedValue);
    public static double random() => _random.NextDouble();
    public static double uniform(double a, double b) => a + ((b - a) * _random.NextDouble());

    public static int randint(int a, int b)
    {
        if (a > b) throw new System.ArgumentException("empty range for randint()");
        return checked((int)_random.NextInt64(a, (long)b + 1));
    }

    public static int randrange(int stop) => randrange(0, stop, 1);
    public static int randrange(int start, int stop) => randrange(start, stop, 1);

    public static int randrange(int start, int stop, int step)
    {
        if (step == 0) throw new System.ArgumentException("zero step for randrange()");
        long count = step > 0
            ? (stop <= start ? 0 : ((long)stop - start + step - 1) / step)
            : (stop >= start ? 0 : ((long)start - stop + (-step) - 1) / (-step));
        if (count <= 0) throw new System.ArgumentException("empty range for randrange()");
        var index = _random.NextInt64(count);
        return checked((int)(start + (index * step)));
    }

    public static T choice<T>(System.Collections.Generic.IList<T> sequence)
    {
        if (sequence.Count == 0) throw new System.IndexOutOfRangeException("Cannot choose from an empty sequence");
        return sequence[_random.Next(sequence.Count)];
    }

    public static System.Collections.Generic.List<T> choices<T>(System.Collections.Generic.IList<T> population, int k = 1)
    {
        if (population.Count == 0) throw new System.IndexOutOfRangeException("Cannot choose from an empty sequence");
        if (k < 0) throw new System.ArgumentOutOfRangeException(nameof(k));
        var result = new System.Collections.Generic.List<T>(k);
        for (var i = 0; i < k; i++) result.Add(choice(population));
        return result;
    }

    public static System.Collections.Generic.List<T> sample<T>(System.Collections.Generic.IList<T> population, int k)
    {
        if (k < 0 || k > population.Count) throw new System.ArgumentException("Sample larger than population");
        var copy = population.ToList();
        shuffle(copy);
        return copy.Take(k).ToList();
    }

    public static void shuffle<T>(System.Collections.Generic.IList<T> sequence)
    {
        for (var i = sequence.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (sequence[i], sequence[j]) = (sequence[j], sequence[i]);
        }
    }

    public static int getrandbits(int k)
    {
        if (k < 0 || k > 30) throw new System.ArgumentOutOfRangeException(nameof(k));
        if (k == 0) return 0;
        return _random.Next(0, 1 << k);
    }

    public static byte[] randbytes(int n)
    {
        if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n));
        var result = new byte[n];
        _random.NextBytes(result);
        return result;
    }

    public static double triangular(double low = 0.0, double high = 1.0, double mode = double.NaN)
    {
        if (high < low) (low, high) = (high, low);
        if (high == low) return low;

        var m = double.IsNaN(mode) ? (low + high) / 2.0 : mode;
        if (m < low || m > high)
            throw new System.ArgumentOutOfRangeException(nameof(mode), "mode must be between low and high");

        var c = (m - low) / (high - low);
        var u = _random.NextDouble();
        return u <= c
            ? low + System.Math.Sqrt(u * (high - low) * (m - low))
            : high - System.Math.Sqrt((1 - u) * (high - low) * (high - m));
    }

    public static double gauss(double mu = 0.0, double sigma = 1.0)
    {
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return mu + sigma * System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2);
    }

    public static double normalvariate(double mu = 0.0, double sigma = 1.0) => gauss(mu, sigma);
    public static double lognormvariate(double mu, double sigma) => System.Math.Exp(normalvariate(mu, sigma));
    public static double expovariate(double lambd = 1.0) => -System.Math.Log(1.0 - _random.NextDouble()) / lambd;
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

    public static long gcd(long a, long b) => Gcd(a, b);
    public static long lcm(long a, long b) => a == 0 || b == 0 ? 0 : System.Math.Abs((a / Gcd(a, b)) * b);
    public static double hypot(double x, double y) => System.Math.Sqrt((x * x) + (y * y));
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

    public static double[] modf(double value) =>
        new[] { value - System.Math.Truncate(value), System.Math.Truncate(value) };

    private static long Gcd(long a, long b)
    {
        a = System.Math.Abs(a);
        b = System.Math.Abs(b);
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
                'Z' => value.ToString("zzz", System.Globalization.CultureInfo.InvariantCulture),
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
    public int tm_isdst => 0;
}
""";
}
