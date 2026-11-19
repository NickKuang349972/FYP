import os
import sys
from docx import Document

paths = [
    r"C:\Users\kuang\Documents\pdf\游戏Boss设计：坦克与直升机抉择.pdf",
    r"C:\Users\kuang\Documents\FYP\06CIT4002 FYP_ProjectGuide (Apps and Game Dev)2026_27.docx",
]

for p in paths:
    print("=" * 80)
    print(p)
    print("exists:", os.path.exists(p))
    if os.path.exists(p):
        print("size:", os.path.getsize(p))

docx_path = paths[1]
if os.path.exists(docx_path):
    doc = Document(docx_path)
    print("\n" + "=" * 80)
    print("DOCX PARAGRAPHS:", len(doc.paragraphs), " TABLES:", len(doc.tables))
    print("=" * 80)
    for i, para in enumerate(doc.paragraphs):
        text = para.text.strip()
        if text:
            print(f"[{i}] ({para.style.name}) {text}")
