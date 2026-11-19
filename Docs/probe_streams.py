import re
import sys
import zlib

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PDF = r"C:\Users\kuang\Documents\pdf\游戏Boss设计：坦克与直升机抉择.pdf"
data = open(PDF, "rb").read()

stream_re = re.compile(rb"stream\r?\n", re.S)
counts = {"streams": 0, "inflated": 0, "BT": 0, "Tj": 0, "TJ": 0, "ToUnicode_cmap": 0}
samples = []

pos = 0
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
    counts["streams"] += 1
    try:
        out = zlib.decompress(raw)
        counts["inflated"] += 1
    except Exception:
        continue

    if b"beginbfchar" in out or b"beginbfrange" in out:
        counts["ToUnicode_cmap"] += 1
    if b"BT" in out:
        counts["BT"] += 1
        counts["Tj"] += out.count(b"Tj")
        counts["TJ"] += out.count(b"TJ")
        if len(samples) < 2:
            samples.append(out[:600])

print(counts)
for i, s in enumerate(samples):
    print("---- sample", i)
    print(s[:600])
