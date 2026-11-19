import os
import sys
import glob

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

candidates = []
roots = [
    r"C:\Program Files\LibreOffice\program\soffice.exe",
    r"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
    r"C:\Users\kuang\.dsh",
]
for r in roots:
    if os.path.isfile(r):
        candidates.append(r)

for base in [r"C:\Users\kuang\.dsh", r"C:\Program Files", r"C:\Program Files (x86)"]:
    if not os.path.isdir(base):
        continue
    for pat in ("**/soffice.exe", "**/soffice.com"):
        try:
            candidates.extend(glob.glob(os.path.join(base, pat), recursive=True))
        except Exception as e:
            print("scan error", base, e)

seen = []
for c in candidates:
    if c not in seen:
        seen.append(c)
print("FOUND:")
for c in seen[:20]:
    print(c)
print("count", len(seen))
