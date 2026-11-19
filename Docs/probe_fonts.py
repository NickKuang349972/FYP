import re
import sys
import zlib

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PDF = r"C:\Users\kuang\Documents\pdf\游戏Boss设计：坦克与直升机抉择.pdf"
data = open(PDF, "rb").read()

stream_re = re.compile(rb"stream\r?\n", re.S)
pos = 0
found = 0
text_sample = None

while True:
    m = stream_re.search(data, pos)
    if not m:
        break
    start = m.end()
    end = data.find(b"endstream", start)
    if end == -1:
        break
    raw = data[start:end]
    pos = end + 9
    try:
        out = zlib.decompress(raw)
    except Exception:
        continue

    if b"BaseFont" in out and found < 6:
        found += 1
        print("==== OBJSTM/DICT with BaseFont ====")
        print(out[:1200])
        print()

    if text_sample is None and b"BT" in out:
        i = out.find(b"BT")
        text_sample = out[max(0, i - 200): i + 900]

print("==== TEXT SAMPLE ====")
print(text_sample)
