"""Attempt to recover text from a PDF whose glyphs carry no ToUnicode map.

Strategy: find embedded TrueType font programs, parse their `cmap` tables,
invert unicode -> glyph id into glyph id -> unicode, then decode the
single-byte codes used in the (..)Tj operators.
"""

import re
import struct
import sys
import zlib

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

PDF = r"C:\Users\kuang\Documents\pdf\游戏Boss设计：坦克与直升机抉择.pdf"
OUT = r"C:\Users\kuang\AppData\Local\Temp\pdf_text.txt"

data = open(PDF, "rb").read()

# ---------------------------------------------------------------- streams
streams = []
pos = 0
pat = re.compile(rb"stream\r?\n")
while True:
    m = pat.search(data, pos)
    if not m:
        break
    start = m.end()
    end = data.find(b"endstream", start)
    if end == -1:
        break
    raw = data[start:end]
    pos = end + 9
    try:
        streams.append(zlib.decompress(raw))
    except Exception:
        streams.append(raw)


def parse_cmap(font):
    """Return {glyph_id: unicode} from a TrueType font program."""
    try:
        num_tables = struct.unpack(">H", font[4:6])[0]
    except Exception:
        return {}

    tables = {}
    for i in range(num_tables):
        off = 12 + i * 16
        tag = font[off:off + 4]
        offset, length = struct.unpack(">II", font[off + 8:off + 16])
        tables[tag] = (offset, length)

    if b"cmap" not in tables:
        return {}

    cmap_off = tables[b"cmap"][0]
    try:
        n = struct.unpack(">H", font[cmap_off + 2:cmap_off + 4])[0]
    except Exception:
        return {}

    gid2uni = {}
    for i in range(n):
        rec = cmap_off + 4 + i * 8
        platform, encoding, sub_off = struct.unpack(">HHI", font[rec:rec + 8])
        sub = cmap_off + sub_off
        try:
            fmt = struct.unpack(">H", font[sub:sub + 2])[0]
        except Exception:
            continue
        if fmt == 4:
            try:
                seg_x2 = struct.unpack(">H", font[sub + 6:sub + 8])[0]
                seg = seg_x2 // 2
                end_c = sub + 14
                start_c = end_c + seg_x2 + 2
                id_delta = start_c + seg_x2
                id_range = id_delta + seg_x2
                for s in range(seg):
                    end_cv = struct.unpack(">H", font[end_c + s * 2:end_c + s * 2 + 2])[0]
                    start_cv = struct.unpack(">H", font[start_c + s * 2:start_c + s * 2 + 2])[0]
                    delta = struct.unpack(">h", font[id_delta + s * 2:id_delta + s * 2 + 2])[0]
                    ro = struct.unpack(">H", font[id_range + s * 2:id_range + s * 2 + 2])[0]
                    if start_cv == 0xFFFF:
                        continue
                    for c in range(start_cv, min(end_cv, 0xFFFF) + 1):
                        if ro == 0:
                            gid = (c + delta) & 0xFFFF
                        else:
                            addr = id_range + s * 2 + ro + (c - start_cv) * 2
                            if addr + 2 > len(font):
                                continue
                            gid = struct.unpack(">H", font[addr:addr + 2])[0]
                            if gid:
                                gid = (gid + delta) & 0xFFFF
                        if gid and gid not in gid2uni:
                            gid2uni[gid] = c
            except Exception:
                continue
        elif fmt == 12:
            try:
                ngroups = struct.unpack(">I", font[sub + 12:sub + 16])[0]
                for g in range(ngroups):
                    p = sub + 16 + g * 12
                    sc, ec, sg = struct.unpack(">III", font[p:p + 12])
                    for c in range(sc, min(ec, sc + 512) + 1):
                        gid = sg + (c - sc)
                        if gid not in gid2uni:
                            gid2uni[gid] = c
            except Exception:
                continue
    return gid2uni


gid2uni = {}
font_count = 0
for s in streams:
    if s[:4] in (b"\x00\x01\x00\x00", b"true", b"ttcf", b"OTTO"):
        font_count += 1
        m = parse_cmap(s)
        for gid, uni in m.items():
            gid2uni.setdefault(gid, uni)

print("fonts found:", font_count, "glyph entries:", len(gid2uni))

# ---------------------------------------------------------------- text
out_lines = []
tj_re = re.compile(rb"\((.*?)\)\s*Tj", re.S)

for s in streams:
    if b"BT" not in s:
        continue
    chars = []
    for m in tj_re.finditer(s):
        raw = m.group(1)
        # unescape
        raw = raw.replace(b"\\(", b"(").replace(b"\\)", b")").replace(b"\\\\", b"\\")
        for b in raw:
            uni = gid2uni.get(b)
            chars.append(chr(uni) if uni else "")
    text = "".join(chars).strip()
    if text:
        out_lines.append(text)

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(out_lines))

print("lines:", len(out_lines))
print("\n".join(out_lines[:40]))
