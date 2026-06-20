import re
from indent_converter import braces_to_indent

def transpile(sharpton_code):
    """تبدیل SharpThon به Python"""
    python_code = sharpton_code

    python_code = braces_to_indent(python_code)

    python_code = python_code.replace('foreach', 'for')
    
    # ── ۱. متغیرها ──
    python_code = re.sub(r'(\w+):\s*(\w+)\s*=', r'\1 =', python_code)  # type hint → ignore
    
    # ── ۲. Write → print ──
    python_code = re.sub(r'Write\(', 'print(', python_code)
    
    # ── ۳. False/True/None ──
    python_code = python_code.replace('False', 'False')
    python_code = python_code.replace('True', 'True')
    python_code = python_code.replace('None', 'None')
    
    # ── ۴. foreach (i in range) → for i in range ──
    python_code = re.sub(r'foreach\s*\((\w+)\s+in\s+(.+?)\)\s*\{', r'for \1 in \2:', python_code)
    
    # ── ۵. for (i=0;i!=10;i++) → while style (موقتاً) ──
    # اینو بعداً درست می‌کنیم
    
    # ── ۶. try/catch → try/except ──
    python_code = python_code.replace('catch', 'except')
    
    # ── ۷. elif → elif (پایتون خودش داره) ──
    
    # ── ۸. def → def (همونه) ─ـ
    
    # ── ۹. class → class (همونه) ─ـ
    
    # ── ۱۰. آکولاد → حذف (پایتون فاصله می‌خواد) ─ـ
    # این پیچیده‌ست — فعلاً آکولاد رو ignore کن
    
    return python_code
