import os
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

from docx import Document

PATH = r"D:\FYP\Docs\FYP_DevDiary_Entry2_2026-11-13.docx"
doc = Document(PATH)

words = sum(len(p.text.split()) for p in doc.paragraphs)
for tb in doc.tables:
    for row in tb.rows:
        for c in row.cells:
            words += len(c.text.split())

print("size", os.path.getsize(PATH))
print("paragraphs", len(doc.paragraphs), "tables", len(doc.tables), "sections", len(doc.sections))
print("words", words)
print("headings:", [p.text for p in doc.paragraphs if p.style.name.startswith("Heading")])
print("margins L", doc.sections[0].left_margin.inches, "T", doc.sections[0].top_margin.inches)
