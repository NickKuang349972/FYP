import re
import sys
import zlib

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PDF = r"C:\Users\kuang\Documents\pdf\游戏Boss设计：坦克与直升机抉择.pdf"

data = open(PDF, "rb").read()
print("size", len(data))
print("header", data[:20])

for key in [b"/Type /Page", b"/Type/Page", b"/Font", b"/ToUnicode", b"/FlateDecode", b"/Image", b"/DCTDecode", b"/ObjStm", b"/XRef"]:
    print(key, data.count(key))

# count pages roughly
pages = len(re.findall(rb"/Type\s*/Page[^s]", data))
print("page-ish count", pages)
