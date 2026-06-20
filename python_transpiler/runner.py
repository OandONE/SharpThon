import sys
from transpiler import transpile

def run(filepath: str):
    """خوندن فایل .sp و اجرای کد تبدیل‌شده"""
    with open(filepath, 'r', encoding='utf-8') as f:
        sp_code = f.read()
    
    py_code = transpile(sp_code)
    
    exec(py_code)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python runner.py <file.sp>")
        sys.exit(1)
    
    run(sys.argv[1])
