"""Markdown → Word(.docx) 转换器（只覆盖本项目文档用到的子集）

支持的语法：
  # / ## / ### 三级标题
  | a | b | 形式的表格（第一行表头，第二行 --- 分隔）
  - 无序列表（支持一级缩进） 与 1. 有序列表
  > 引用块
  ``` 代码块 ```
  行内 **加粗**、`行内代码`、[文本](链接)
用法：
  python Tools/md2docx.py <输入.md> <输出.docx>
"""
import re
import sys
from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Pt, RGBColor, Cm

BODY_FONT = "微软雅黑"
CODE_FONT = "Consolas"


def set_cell_shading(cell, hex_color):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), hex_color)
    tc_pr.append(shd)


def set_run_font(run, name=BODY_FONT, size=None, bold=None, color=None):
    run.font.name = name
    run._element.rPr.rFonts.set(qn("w:eastAsia"), name)
    if size is not None:
        run.font.size = Pt(size)
    if bold is not None:
        run.font.bold = bold
    if color is not None:
        run.font.color.rgb = RGBColor(*color)


INLINE_RE = re.compile(r"(\*\*.+?\*\*|`[^`]+`|\[[^\]]+\]\([^)]+\))")

# Word 里 emoji 常缺字形（会显示成方框），输出时换成中文/符号
SANITIZE = {
    "⬜": "待填",
    "✅": "已填",
    "🟡": "待补",
    "📷": "（录屏）",
    "🧠": "",
}


def sanitize(text: str) -> str:
    for bad, good in SANITIZE.items():
        text = text.replace(bad, good)
    return text


def local_link(url: str) -> bool:
    return not re.match(r"^(https?|mailto):", url, re.I)


def add_inline(paragraph, text, size=10.5, bold=False, color=None):
    """把一段带行内标记的文本拆成多个 run 写进段落。"""
    text = sanitize(text)
    for part in INLINE_RE.split(text):
        if not part:
            continue
        if part.startswith("**") and part.endswith("**") and len(part) > 4:
            run = paragraph.add_run(part[2:-2])
            set_run_font(run, size=size, bold=True, color=color)
        elif part.startswith("`") and part.endswith("`") and len(part) > 2:
            run = paragraph.add_run(part[1:-1])
            set_run_font(run, name=CODE_FONT, size=size - 0.5, bold=bold, color=(0x9C, 0x27, 0x30))
        elif part.startswith("[") and "](" in part and part.endswith(")"):
            m = re.match(r"\[([^\]]+)\]\(([^)]+)\)", part)
            label, url = m.group(1), m.group(2)
            run = paragraph.add_run(label)
            set_run_font(run, size=size, bold=bold, color=(0x1F, 0x4E, 0x79))
            if not local_link(url):
                tail = paragraph.add_run(f" ({url})")
                set_run_font(tail, size=size - 1, color=(0x60, 0x60, 0x60))
        else:
            run = paragraph.add_run(part)
            set_run_font(run, size=size, bold=bold, color=color)


def parse_table_row(line):
    cells = line.strip().strip("|").split("|")
    return [c.strip() for c in cells]


def is_separator(line):
    return bool(re.match(r"^\|[\s:|-]+\|$", line.strip()))


def convert(md_path, docx_path):
    with open(md_path, "r", encoding="utf-8") as fh:
        lines = fh.read().replace("\r\n", "\n").split("\n")

    doc = Document()

    # 页面与正文样式
    section = doc.sections[0]
    section.top_margin = Cm(2)
    section.bottom_margin = Cm(2)
    section.left_margin = Cm(2)
    section.right_margin = Cm(2)

    normal = doc.styles["Normal"]
    normal.font.name = BODY_FONT
    normal.font.size = Pt(10.5)
    normal.element.rPr.rFonts.set(qn("w:eastAsia"), BODY_FONT)

    for name, size in (("Heading 1", 20), ("Heading 2", 15), ("Heading 3", 12.5)):
        style = doc.styles[name]
        style.font.name = BODY_FONT
        style.font.size = Pt(size)
        style.font.bold = True
        style.element.rPr.rFonts.set(qn("w:eastAsia"), BODY_FONT)

    i = 0
    total = len(lines)
    while i < total:
        line = lines[i]
        stripped = line.strip()

        # 空行
        if not stripped:
            i += 1
            continue

        # 代码块
        if stripped.startswith("```"):
            i += 1
            code_lines = []
            while i < total and not lines[i].strip().startswith("```"):
                code_lines.append(lines[i].rstrip())
                i += 1
            i += 1
            for code in code_lines:
                p = doc.add_paragraph()
                p.paragraph_format.space_after = Pt(0)
                p.paragraph_format.left_indent = Cm(0.4)
                run = p.add_run(code if code else " ")
                set_run_font(run, name=CODE_FONT, size=9)
                run.font.color.rgb = RGBColor(0x22, 0x22, 0x22)
            doc.add_paragraph().paragraph_format.space_after = Pt(2)
            continue

        # 表格
        if stripped.startswith("|") and i + 1 < total and is_separator(lines[i + 1]):
            header = parse_table_row(lines[i])
            i += 2
            rows = []
            while i < total and lines[i].strip().startswith("|"):
                rows.append(parse_table_row(lines[i]))
                i += 1
            table = doc.add_table(rows=1, cols=len(header))
            table.style = "Table Grid"
            table.alignment = WD_TABLE_ALIGNMENT.CENTER
            for idx, text in enumerate(header):
                cell = table.rows[0].cells[idx]
                cell.text = ""
                add_inline(cell.paragraphs[0], text, size=10, bold=True)
                set_cell_shading(cell, "E8EEF7")
            for row in rows:
                cells = table.add_row().cells
                for idx in range(len(header)):
                    value = row[idx] if idx < len(row) else ""
                    cells[idx].text = ""
                    add_inline(cells[idx].paragraphs[0], value, size=10)
            doc.add_paragraph().paragraph_format.space_after = Pt(2)
            continue

        # 标题
        if stripped.startswith("#"):
            level = len(stripped) - len(stripped.lstrip("#"))
            text = stripped[level:].strip()
            if level == 1:
                p = doc.add_heading(level=1)
                add_inline(p, text, size=20, bold=True)
                p.alignment = WD_ALIGN_PARAGRAPH.LEFT
            elif level == 2:
                p = doc.add_heading(level=2)
                add_inline(p, text, size=15, bold=True)
            else:
                p = doc.add_heading(level=3)
                add_inline(p, text, size=12.5, bold=True)
            i += 1
            continue

        # 引用
        if stripped.startswith(">"):
            text = stripped.lstrip("> ").strip()
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.6)
            add_inline(p, text, size=10, color=(0x44, 0x44, 0x44))
            i += 1
            continue

        # 分隔线
        if stripped in ("---", "***", "___"):
            p = doc.add_paragraph()
            p.paragraph_format.space_after = Pt(0)
            p_pr = p._p.get_or_add_pPr()
            borders = OxmlElement("w:pBdr")
            bottom = OxmlElement("w:bottom")
            bottom.set(qn("w:val"), "single")
            bottom.set(qn("w:sz"), "6")
            bottom.set(qn("w:color"), "BBBBBB")
            borders.append(bottom)
            p_pr.append(borders)
            i += 1
            continue

        # 列表
        if re.match(r"^\s*[-*]\s+", line):
            indent = len(line) - len(line.lstrip())
            text = re.sub(r"^\s*[-*]\s+", "", line)
            style = "List Bullet 2" if indent >= 2 else "List Bullet"
            try:
                p = doc.add_paragraph(style=style)
            except KeyError:
                p = doc.add_paragraph(style="List Bullet")
            add_inline(p, text)
            i += 1
            continue

        if re.match(r"^\s*\d+\.\s+", line):
            text = re.sub(r"^\s*\d+\.\s+", "", line)
            p = doc.add_paragraph(style="List Number")
            add_inline(p, text)
            i += 1
            continue

        # 普通段落
        p = doc.add_paragraph()
        add_inline(p, stripped)
        i += 1

    doc.save(docx_path)
    return docx_path


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("用法: python Tools/md2docx.py <输入.md> <输出.docx>")
        sys.exit(1)
    out = convert(sys.argv[1], sys.argv[2])
    print(f"已生成: {out}")
