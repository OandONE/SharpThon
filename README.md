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

Python is simple. C# is fast. **SharpThon** gives you both.

Write Python-like code with C# syntax — braces, semicolons, and type hints.  
Transpile to **clean C#** and run anywhere .NET runs.

```spy
name = "OandONE"
age: int = 16

if (age == 16) {
    Write(f"{name} is {age}")
}
```

---

✨ Features

· ✅ Python-like syntax — familiar, readable
· ✅ C# target — transpiles to clean, idiomatic C#
· ✅ Optional type hints — x = 10 or x: int = 10
· ✅ Braces {} — no more indentation errors
· ✅ Write() — same as print() or Console.WriteLine()
· ✅ if/elif/else — C# gets else if
· ✅ for (i in n) — becomes foreach + Enumerable.Range
· ✅ while, try/catch, ++/--
· ✅ Comments: # → //
· 🚧 Classes (in progress)
· 🚧 Functions (in progress)
· 🚧 Imports (in progress)

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
Write(f"Hello {name}!")
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
    Write(i)   // 0, 1, 2, 3, 4
}

counter = 0
while (counter < 3) {
    Write(counter)
    counter++
}
```

Error Handling

```spy
try {
    risky_code()
}
catch (Exception e) {
    Write(e)
}
```

---

🔄 How It Works

```
SharpThon (.spy) → Transpiler → C# (.cs) → .NET Build → Run
```

The transpiler is written in C#.
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
│   │   └── Transpiler.cs
│   ├── Sharpton.Cli/       # CLI tool
│   │   └── Program.cs
│   └── Sharpton.sln
├── test.spy                # Sample files
└── README.md
```

---

🗺️ Roadmap

Phase Status
Python MVP ✅ Complete
C# Transpiler (basic) ✅ Complete
Functions (def) 🚧 In progress
Classes (class) 🚧 In progress
Imports (import x.y) 🚧 In progress
for (i=0;i<10;i++) (C-style) ❌
Self-hosting (transpile SharpThon with SharpThon) ❌
NuGet package ❌

---

📄 License

MIT © OandONE

---

🙏 Acknowledgments

Inspired by Python (simplicity) and C# (power).

Built by a developer who wanted braces in Python.
