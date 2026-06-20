# indent_converter.py
import re

def braces_to_indent(code: str) -> str:
    lines = code.split('\n')
    result = []
    indent_level = 0
    indent_str = '    '  # 4 spaces

    # پیش‌پردازش: حذف سمی‌کالن‌های انتهای خطوط (ساده)
    cleaned_lines = []
    for line in lines:
        stripped = line.rstrip()
        if stripped.endswith(';'):
            stripped = stripped[:-1]
        cleaned_lines.append(stripped)
    lines = cleaned_lines

    for line in lines:
        stripped = line.strip()
        if not stripped:
            result.append('')
            continue

        # بستن آکولاد → کاهش indent
        if stripped == '}':
            indent_level = max(0, indent_level - 1)
            continue  # خط } رو چاپ نمی‌کنیم

        # باز کردن آکولاد → تبدیل به کولن و افزایش indent
        # الگوهای رایج
        # if/elif/else, for, foreach, while, def, class, try, catch, finally
        # الگو: keyword (condition) { → keyword condition:
        # یا keyword { → keyword:
        match = re.match(
            r'(if|elif|else|for|foreach|while|def|class|try|catch|finally)(\s*\(.*?\))?\s*\{',
            stripped
        )
        if match:
            keyword = match.group(1)
            condition = match.group(2) if match.group(2) else ''
            # حذف پرانتزهای شرط (اگه وجود داره) و جایگزینی با کولن
            if condition:
                # حذف پرانتز بیرونی
                inner = condition.strip()[1:-1]  # '(' and ')'
                line_transformed = f"{keyword} {inner}:"
            else:
                line_transformed = f"{keyword}:"
            # اضافه کردن با indent فعلی
            result.append(indent_str * indent_level + line_transformed)
            indent_level += 1
            continue

        # foreach خاص: foreach (i in range(10)) { → for i in range(10):
        foreach_match = re.match(r'foreach\s*\((.+)\)\s*\{', stripped)
        if foreach_match:
            inner = foreach_match.group(1)
            line_transformed = f"for {inner}:"
            result.append(indent_str * indent_level + line_transformed)
            indent_level += 1
            continue

        # for (i=0;i!=10;i++) { → تبدیل به while (پیچیده‌تر)
        cfor_match = re.match(r'for\s*\((.+);(.+);(.+)\)\s*\{', stripped)
        if cfor_match:
            init, cond, inc = cfor_match.groups()
            # تبدیل به while
            # اول init رو چاپ کن
            result.append(indent_str * indent_level + init.strip() + ';')
            result.append(indent_str * indent_level + f"while {cond.strip()}:")
            # افزایش indent برای بدنه
            indent_level += 1
            # دستور inc رو باید به انتهای بدنه اضافه کنیم — این نیاز به ردیابی داره
            # فعلاً ساده: خود inc رو به صورت کامنت بذار (یا نادیده بگیر)
            result.append(indent_str * indent_level + f"# increment: {inc.strip()}")
            continue  # این راه حل موقتی‌ست

        # اگر خط حاوی { باشه ولی الگو تشخیص داده نشد (مثلاً بلوک ساده)
        if '{' in stripped:
            line_transformed = stripped.replace('{', ':')
            result.append(indent_str * indent_level + line_transformed)
            indent_level += 1
            continue

        # خط عادی
        result.append(indent_str * indent_level + stripped)

    return '\n'.join(result)
