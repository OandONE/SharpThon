## Syntax

### Variables
```python
name = "ali"              # optional semicolon
age: int = 16             # optional type hint
```

Conditions

```python
if (a == 10) {
    Write("ten")
} elif (a == 20) {
    Write("twenty")
} else {
    Write("other")
}
```

Loops

```python
for (i in range(10)) {
    Write(i)
}

for (i = 0; i < 10; i++) {
    Write(i)
}
```

Functions

```python
def add(a: int, b: int) -> int {
    return a + b
}
Write(add(5, 3))  # 8
```

Classes

```python
class Person {
    def __init__(self, name) {
        self.name = name
    }
    @property
    def info() {
        return self.name
    }
}
```

Error Handling

```python
try {
    risky_code()
} catch (ValueError) {
    Write("bad value")
} catch (Exception as e) {
    Write(e)
}
```

Imports (C#-style)

```python
import colorama.Fore
Write(Fore.GREEN + "green text")
```
