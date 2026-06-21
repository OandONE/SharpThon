<p align="center">
  <h1 align="center">🐍⚡ SharpThon</h1>
  <p align="center"><strong>A Python-like language that transpiles to C# — braces, semicolons, and strong typing.</strong></p>
</p>

<p align="center">
  <a href="https://github.com/OandONE/SharpThon/blob/main/LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow" alt="License: MIT"></a>
  <a href="#"><img src="https://img.shields.io/badge/Stage-Alpha-orange" alt="Alpha"></a>
  <a href="#"><img src="https://img.shields.io/badge/.NET-8.0-blueviolet" alt=".NET 8.0"></a>
</p>

---

## 🤔 Why SharpThon?

Python is simple. C# is fast. **SharpThon** gives you both — with the future of ASP.NET and EF Core built-in.

Write Python-like code with C# syntax. Transpile to **clean C#** and run anywhere .NET runs.

```spy
name = "OandONE"
age: int = 16

if (age == 16) {
    Write("Sweet sixteen!")
}
```

---

✨ Features

· ✅ Python-like syntax — familiar, readable
· ✅ C# target — transpiles to clean, idiomatic C#
· ✅ Optional type hints — x = 10 or x: int = 10
· ✅ str → string, Any → object — seamless C# mapping
· ✅ Braces {} — no more indentation errors
· ✅ Write() — same as print() or Console.WriteLine()
· ✅ if/elif/else — C# gets else if
· ✅ for (i in n) — becomes foreach + Enumerable.Range
· ✅ while, try/catch, ++/--
· ✅ Functions — with modifiers (public, static), type hints, and return types
· ✅ Comments: # → //
· 🚧 ANTLR Parser (in progress — replacing Regex)
· 🚧 Classes (in progress)
· 🚧 Imports (in progress)

---

🎯 Vision

SharpThon will become a complete .NET development language:

```spy
// ASP.NET Core
class HomeController {
    public def Index() -> IActionResult {
        return View("Index", 50)
    }
}

// EF Core
class User : DbContext {
    public int Id { get; set; }
    public string Name { get; set; }
}
```

---

📦 Installation

```bash
git clone https://github.com/OandONE/SharpThon.git
cd SharpThon/sharpton_cs
dotnet build
```

---

🚀 Quick Start

1. Create a .spy file

```spy
// hello.spy
name = "World"
Write("Hello " + name + "!")
```

2. Run with SharpThon

```bash
dotnet run --project Sharpton.Cli -- hello.spy
```

3. Output

```
Hello World!
```

---

📋 Syntax Reference

Variables

```spy
name = "Ali"          // type inferred
age: int = 16         // explicit type
pi = 3.14             // double
is_dev = true         // bool
text: str = "Hello"   // str → string in C#
data: Any = 42        // Any → object in C#
```

Conditions

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

Loops

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

Functions

```spy
public def add(a: int, b: int) -> int {
    return a + b
}
Write("5 + 3 = " + add(5, 3))

static def greet(name: str) {
    Write("Hello " + name + "!")
}
greet("Developer")
```

---

🔄 How It Works

```
SharpThon (.spy) → Transpiler → C# (.cs) → .NET Build → Run
```

The transpiler is written in C#. ANTLR parser is replacing Regex for production-ready parsing.
A Python prototype is also available in python_transpiler/.

---

📁 Project Structure

```
SharpThon/
├── python_transpiler/      # Python prototype (MVP)
│   ├── transpiler.py
│   ├── indent_converter.py
│   └── runner.py
├── sharpton_cs/            # C# transpiler (current)
│   ├── Sharpton.Core/      # Core library
│   │   ├── Transpiler.cs
│   │   ├── Parser.cs       # Sprache-based
│   │   └── SharpThon.g4    # ANTLR grammar
│   ├── Sharpton.Cli/       # CLI tool
│   │   └── Program.cs
│   └── Sharpton.sln
├── test.spy                # Demo file
└── README.md
```

---

🗺️ Roadmap

Phase Status
Python MVP ✅ Complete
C# Transpiler (Regex) ✅ Complete
Functions with modifiers ✅ Complete
str → string, Any type ✅ Complete
ANTLR Parser 🚧 In progress
Classes (class) ❌
Imports (import x.y) ❌
for (i=0;i<10;i++) (C-style) ❌
ASP.NET Core support ❌
EF Core support ❌
Self-hosting (transpile SharpThon with SharpThon) ❌
NuGet package ❌

---

📄 License

MIT © OandONE

---

🙏 Acknowledgments

Inspired by Python (simplicity) and C# (power).

Built by a developer who wanted braces in Python — and the full power of .NET.
