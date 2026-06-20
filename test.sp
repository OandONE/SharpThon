test_str = "test";
test_int = 4;
test_bool = False;

def test() -> str {
    return "str";
}

text = test();
Write(text);

foreach (i in range(5)) {
    Write(i);
}

if (test_int > 5) {
    Write("big");
}
else {
    Write("small");
}

def add(num1: int, num2: int) -> int {
    return num1 + num2;
}

jam = add(50,30);

Write(jam); # result : 80
