<p align="center">
  <h1 align="center">🐍⚡ SharpThon</h1>
  <img src="logo.svg">
  <p align="center">
  <strong>A Python-like language that transpiles to C# — up to 16x faster than Python.</strong>
  <br>
  <sub>Benchmark based on a simple 10M loop test. Performance varies by workload.</sub>
</p>

<p align="center">
  <a href="https://github.com/OandONE/SharpThon/blob/main/LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="License: MIT"></a>
  <a href="#"><img src="https://img.shields.io/badge/Stage-Alpha-orange" alt="Alpha"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-8.0-blueviolet" alt=".NET 8.0"></a>
  <a href="#"><img src="https://img.shields.io/badge/Parser-Sprache-green" alt="Sprache"></a>
  <a href="#"><img src="https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-blue" alt="Cross Platform"></a>
  <a href="#"><img src="https://img.shields.io/badge/Architecture-x64%20%7C%20ARM64-success" alt="Architecture"></a>
  <a href="#"><img src="https://img.shields.io/badge/Speed-Up%20to%2016x%20faster%20than%20Python-red" alt="Speed"></a>
</p>

---

## ⚡ Performance

**SharpThon is 16x faster than Python.**

| Language           | 10M Loop Iterations |
| ------------------ | ------------------: |
| Python             |            3,275 ms |
| **SharpThon (C#)** |          **196 ms** |

Same syntax, 16x the speed. The power of .NET, the simplicity of Python.

---

## 🤔 Why SharpThon?

Python offers simplicity. C# offers performance and the .NET ecosystem. SharpThon aims to combine both.

Write Python-like code with C# syntax. Transpile to **clean C#** and run anywhere .NET runs.

```spy
name = "OandONE"
age: int = 16

if (age == 16) {
    Write("Sweet sixteen!")
}
```

---

## ✨ Features

* ✅ Python-like syntax — familiar, readable
* ✅ C# target — transpiles to C# that can directly participate in the .NET ecosystem
* ✅ Sprache Parser — clean C#, minimal Regex, no external tools
* ✅ Optional type hints — `x = 10` or `x: int = 10`
* ✅ Constants & Readonly — `const MAX = 3`, `readonly _host: str = "localhost"`
* ✅ Numeric types — `int`, `long`, `float`, `double`
* ✅ `str` → `string`, `Any` → `object` — seamless C# mapping
* ✅ Braces `{}` — no more indentation errors
* ✅ `Write()` — similar to `print()` or `Console.WriteLine()`
* ✅ `if/elif/else` — `elif` becomes C# `else if`
* ✅ `for (i in n)` — becomes `foreach` + `Enumerable.Range`
* ✅ `while`, `++/--`
* ✅ Functions — modifiers, type hints, parameters, and return types
* ✅ Classes — fields, constructors, methods, access modifiers, and `this`
* ✅ Comments — supports `#` and preserves inline `//` comments
* ✅ Module imports — import other `.spy` files as modules
* ✅ `using` — import .NET namespaces
* ✅ .NET namespace imports
* ✅ C#/.NET interoperability
* ✅ String interpolation — `f"Hello {name}"` → `$"Hello {name}"`
* ✅ `try/catch/finally` — supports exception types and aliases
* ✅ Async tasks — `go` and `await go` for simple asynchronous execution
* ✅ C-style `for` loops — `for (i = 0; i < 10; i++)`
* ✅ Python-style error messages — compile and runtime errors mapped to `.spy` lines
* ✅ Clear import errors — missing files and circular imports are reported clearly
* ✅ Properties — `get`/`set` accessors with modifiers
* ✅ Object instantiation — no `new` keyword needed
* ✅ Package imports — `index.spy` as entry point for folders
* ✅ Async tasks — `go {...}` and `await go {...}` with block support (sync + async)
* ✅ CI/CD — GitHub Actions for build and test
* ✅ Inheritance — class `:` parent syntax
* ✅ Lists — array literal syntax `[1, 2, 3]`
* ✅ Dictionaries — Python-style `{"key": value}` syntax
* ✅ Error context display — shows the exact source line with each error
* ✅ Full Generics — `List<T>`, `Dictionary<K, V>`, nested generics
* ✅ Python-like C# API names — `Console.write_line()` automatically maps to `Console.WriteLine()`

---

## 🎯 Vision

### Future Goals

* Full C# / .NET interoperability
* Interfaces
* Generics
* Advanced C# type support
* External .NET library support
* NuGet package support
* Self-hosting compiler

SharpThon aims to become a lightweight .NET development language
that can directly interoperate with the existing C# and .NET ecosystem.

SharpThon does not need to understand or implement frameworks such as
ASP.NET Core or Entity Framework Core directly. Instead, it aims to
generate C# that can consume any compatible .NET library through normal
using directives, types, methods, generics, and APIs.

```spy
using SomeDotNetLibrary

class UserService {
    public def GetUser(id: int) {
        return UserRepository.Find(id)
    }
}
```

---

## 🔮 Future Ideas

### py2spy

A long-term goal of SharpThon is **py2spy** — a tool that converts a subset of Python code into SharpThon syntax, helping developers migrate Python projects to the .NET ecosystem while keeping a familiar syntax.

Python:

```py
for i in range(5):
    print(i)
```

↓

SharpThon:

```spy
for (i in 5) {
    Write(i)
}
```

---

## 📦 Installation

```bash
git clone https://github.com/OandONE/SharpThon.git
cd SharpThon/sharpton_cs
dotnet build
```

SharpThon targets **.NET 8** and runs on platforms supported by .NET, including Windows, Linux, and macOS.

---

## 🚀 Quick Start

### 1. Create a `.spy` file

```spy
// hello.spy
name = "World"
Write("Hello " + name + "!")
```

### 2. Run with SharpThon

```bash
dotnet run --project Sharpton.Cli -- hello.spy
```

### 3. Output

```text
Hello World!
```

---

## 📋 Syntax Reference

### Variables

```spy
name = "Ali"                    // type inferred
age: int = 16                   // explicit type
pi = 3.14                       // double
distance: double = 12.5         // double
population: long = 8000000000   // long
is_dev = true                   // bool
text: str = "Hello"             // str → string in C#
data: Any = 42                  // Any → object in C#
```

### Python-like C# API Names

SharpThon automatically converts snake_case method names to PascalCase when calling C# methods. This lets you use Python-style naming with .NET libraries.

```spy
Console.write_line("Hello from SharpThon!")
Console.write_line_with_color("Red")
```

Transpiles to:

```csharp
Console.WriteLine("Hello from SharpThon!");
Console.WriteLineWithColor("Red");
```

### Constants & Readonly

SharpThon supports C#-style `const` and `readonly` modifiers.

```spy
const MAX_RETRY = 3
const APP_NAME = "SharpThon"
const PI: double = 3.14159

class Config {
    readonly _host: str = "localhost"
    public readonly Port: int = 8080
    public const API_URL = "https://api.example.com"
}
```

Output C#:

```csharp
const int MAX_RETRY = 3;
const string APP_NAME = "SharpThon";
const double PI = 3.14159;

class Config {
    readonly string _host = "localhost";
    public readonly int Port = 8080;
    public const string API_URL = "https://api.example.com";
}
```

- `const` works for local variables and class fields. The type is inferred from the value if not explicitly written.
- `readonly` is for class fields only (it cannot be used inside functions).
- You can combine `public`, `private`, `protected` with `const` or `readonly`.

### Conditions

```spy
if (age > 18) {
    Write("Adult")
}
elif (age == 16) {
    Write("Sweet sixteen!")
}
else {
    Write("Young")
}
```

### Loops

```spy
for (i in 5) {
    Write("Count: " + i)
}

counter = 0
while (counter < 3) {
    Write("While: " + counter)
    counter++
}
```

### Functions

```spy
public def add(a: int, b: int) -> int {
    return a + b
}

Write("5 + 3 = " + add(5, 3))

static def greet(name: str) {
    Write("Hello " + name + "!")
}

greet("Developer")

public def Area(radius: double) -> double {
    return 3.14159 * radius * radius
}
```

### Modules

Import other `.spy` files to use their functions and classes. The transpiler automatically compiles the imported file and adds it to the project.

**main.spy:**

```spy
import math_utils

result = math_utils.add(10, 20)
Write(f"Result: {result}")
```

**math_utils.spy:**

```spy
def add(x: int, y: int) -> int {
    return x + y
}
```

### Package Entry File (`index.spy`)

When importing a folder as a package, SharpThon automatically looks for an `index.spy` file inside that folder and runs it as the entry point.

**Structure:**

```
my_package/
├── index.spy       ← entry point
├── other_file.spy
└── ...
```

**Usage:**

```spy
import my_package
```

SharpThon will automatically load `my_package/index.spy`.

If `index.spy` does not exist, an error is shown:

```
=== SharpThon Import Error ===

Package 'my_package' does not contain an index.spy file.
```

This is similar to `index.js` in Node.js or `__init__.py` in Python, but simpler and more explicit.

### Using .NET Namespaces

Use `using` for standard .NET libraries, just like in C#.

```spy
using System
using System.Linq
```

↓

```csharp
using System;
using System.Linq;
```

### Classes

```spy
class User {
    public name: str
    private age: int

    def User(name: str, age: int) {
        this.name = name
        this.age = age
    }

    public def Greet() {
        Write(f"Hello {name}")
    }

    public def IsAdult() -> bool {
        return age >= 18
    }
}
```

### Properties

SharpThon supports properties with C#-style accessors. Use `get` and `set` blocks to control access to private fields.

```spy
class UserSettings {
    private name: str = "Ali"
    private age: int = 0
    private _password: str = ""

    // Read-only property
    Name -> str {
        return name
    }

    // Public property with getter and setter
    public Age -> int {
        get {
            return age
        }
        set (value) {
            age = value
        }
    }

    // Write-only property
    protected Password -> str {
        set (value) {
            _password = value
        }
    }

    // Static read-only property
    static Config -> str {
        return "default"
    }
}
```

Usage:

```spy
settings = UserSettings()
settings.Age = 42
Write(settings.Name)
Write(UserSettings.Config)
```

Properties support:

- ✅ `get` / `set` accessors
- ✅ `public`, `private`, `protected` modifiers
- ✅ `static` properties
- ✅ Read-only (`get` only) and write-only (`set` only) properties

### Inheritance

SharpThon supports class inheritance. Use `:` to inherit from a parent class.

```spy
class Animal {
    public name: str

    def Animal(name: str) {
        this.name = name
    }

    public def Speak() {
        Write("...")
    }
}

class Dog : Animal {
    def Dog(name: str) {
        // Call parent constructor
        Animal(name)
    }

    public def Speak() {
        Write(name + " says woof!")
    }
}

dog = Dog("Rex")
dog.Speak()
```

The transpiler converts this to C# class inheritance using `:` syntax.

### Object Instantiation

Objects are created without the `new` keyword. This keeps the syntax closer to Python.

```spy
user = User("Ali", 16)
settings = UserSettings()
```

↓

```csharp
var user = new User("Ali", 16);
var settings = new UserSettings();
```

The `new` keyword is added automatically by the transpiler.

### String Interpolation

Use Python-style f-strings for string interpolation.

```spy
name = "Ali"
message = f"Hello {name}"

Write(message)
```

↓

```csharp
var name = "Ali";
var message = $"Hello {name}";

Console.WriteLine(message);
```

### Exception Handling

```spy
try {
    x = 0
    y = 1 / x
}
catch (Exception as ex) {
    Write(ex.Message)
}
finally {
    Write("Done")
}
```

### Async Tasks

Use `go` to run an operation in the background without waiting for it to complete.

```spy
go do_something()
```

This transpiles to:

```csharp
Task.Run(() => do_something());
```

Use `await go` when the task should be awaited:

```spy
await go do_something()
```

This transpiles to:

```csharp
await Task.Run(() => do_something());
```

**Block support:**

```spy
go {
    Write("Hello")
    Write("World")
}

await go {
    data = fetch_data()
    process(data)
}
```

### Error Handling (Python-style Traceback)

SharpThon provides clean, readable error messages similar to Python tracebacks. Both compile-time and runtime errors are mapped back to the original `.spy` file.

When an error occurs, SharpThon displays:

- 📁 **Your `.spy` source file** (not the generated `.cs` file)
- 📍 **Line number** where the error happened
- 💬 **A clear error description**
- 🏷️ **The C# error type** (for advanced users)
- 📝 **The actual source code line** where the error occurred

**Compile-time error example:**

```
=== SharpThon Error ===

File: ../test_error.spy
Line: 6

The name 'unknownVariable' does not exist in the current context
C# Error: CS0103

=======================
```

**Runtime error example (division by zero):**

```spy
// divide.spy
def main() {
    x = 10
    y = 0
    z = x / y   // division by zero
    Write(z)
}
main()
```

Output:

```
=== SharpThon Runtime Error ===

File: ../divide.spy
Line: 4

System.DivideByZeroException: Attempted to divide by zero.

===============================
```

This makes debugging much easier, especially for developers coming from Python who are used to simple, direct error messages.

### Error Context Display

SharpThon now shows the actual source code line along with the error, not just the line number.

**Example:**

```
File: ../_test_error.spy
Line: 8

  8 |         "name": "Ali"

} expected
C# Error: CS1513
```

This makes debugging much easier — you can see exactly which line caused the error without opening the file.

### C-style For Loops

```spy
for (i = 0; i < 5; i++) {
    Write(i)
}
```

↓

```csharp
for (int i = 0; i < 5; i++) {
    Console.WriteLine(i);
}
```

### Lists

```spy
numbers = [1, 2, 3]
names = ["Ali", "Reza"]
mixed = [1, "two", true]
```

### Dictionaries

```spy
scores = {"Ali": 90, "Reza": 85}
config = {"host": "localhost", "port": 8080}
```

Access values:

```spy
Write(scores["Ali"])
config["port"] = 9090
```

### Generics

SharpThon supports full generics with C#-style type parameters:

```spy
// Lists
numbers: List<int> = [1, 2, 3]
names: List<string> = ["Ali", "Mamad"]

// Dictionaries
ages: Dictionary<string, int> = {
    "ali": 16,
    "mamad": 20
}

// Nested generics
matrix: List<List<int>> = [
    [1, 2],
    [3, 4]
]

// Complex types
nested: Dictionary<string, List<int>> = {
    "numbers": [10, 20, 30]
}
```

Generic types map directly to C#:
- `List<T>` → `List<T>`
- `Dictionary<K, V>` → `Dictionary<K, V>`
- Nested generics work as expected

### CI/CD

Every push to GitHub automatically runs build and tests via GitHub Actions.

The workflow file is located at `.github/workflows/dotnet.yml`.

## 🔄 How It Works

```text
SharpThon (.spy)
       ↓
 Sprache Parser
       ↓
 C# (.cs)
       ↓
 .NET Build
       ↓
 Run
```

The transpiler is written in C# using Sprache — a clean parser combinator library.

Minimal Regex, no external tools, no Java — just pure C# and .NET.

A Python prototype is also available in `python_transpiler/`.

---

## 📁 Project Structure

```text
SharpThon/
├── python_transpiler/      # Python prototype (MVP)
│   ├── transpiler.py
│   ├── indent_converter.py
│   └── runner.py
├── sharpton_cs/            # C# transpiler (current)
│   ├── Sharpton.Core/      # Core library
│   │   ├── Transpiler.cs
│   │   ├── Parser.cs       # Sprache parser
│   │   └── Sharpton.Core.csproj
│   ├── Sharpton.Cli/       # CLI tool
│   │   ├── Program.cs
│   │   └── Sharpton.Cli.csproj
│   └── Sharpton.sln
├── test.spy                # Demo file
└── README.md
```

---

## 🗺️ Roadmap

| Feature                                         | Status     |
| ----------------------------------------------- | ---------- |
| Python MVP                                      | ✅ Complete |
| C# Transpiler (Regex)                           | ✅ Complete |
| Sprache Parser                                  | ✅ Complete |
| Functions with modifiers                        | ✅ Complete |
| `str` → `string`, `Any` type                    | ✅ Complete |
| `long` and `double` types                       | ✅ Complete |
| Imports (`import x.y`)                          | ✅ Complete |
| Classes (fields, constructors, methods, `this`) | ✅ Complete |
| String interpolation (f-strings)                | ✅ Complete |
| `try/catch/finally`                             | ✅ Complete |
| Async tasks (`go` / `await go`)                 | ✅ Complete |
| C-style `for` loops                             | ✅ Complete |
| Unit tests                                      | ✅ Complete |
| Clear import errors (missing/circular)          | ✅ Complete |
| Properties                                      | ✅ Complete |
| Async tasks (`go {...}` / `await go {...}`)     | ✅ Complete |
| CI/CD                                           | ✅ Complete |
| Inheritance                                     | ✅ Complete |
| Dictionaries & List Support                     | ✅ Complete |
| Generics                                        | ✅ Complete |
| Constants & Readonly                            | ✅ Complete |
| Python-like names for C# APIs                   | ✅          |
| VS Code Extension (LSP)                        | 🚧 In Progress |
| Interfaces                                      | ❌          |
| LINQ / Lambda expressions                       | ❌          |
| Official documentation (website, PDF)           | ❌          |
| Python UX libraries via `using` (e.g., os)      | ❌          |
| Full `async` / `await` support                  | ❌          |
| Self-hosting                                    | ❌          |
| NuGet package                                   | ❌          |

---

## 📄 License

MIT © OandONE

---

## 🙏 Acknowledgments

Inspired by Python (simplicity) and C# (power).

Built by a developer who wanted Python with braces — and got 16x the speed.
