"""Build the FYP progress report (development diary) DOCX - short, boss-scoped."""

import sys
import os

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

from docx import Document
from docx.shared import Pt, Inches, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

OUT = r"D:\FYP\Docs\FYP_DevDiary_Entry2_2026-11-13.docx"

doc = Document()

normal = doc.styles["Normal"]
normal.font.name = "Times New Roman"
normal.font.size = Pt(12)
normal.paragraph_format.line_spacing = 1.5
normal.paragraph_format.space_after = Pt(6)
rpr = normal.element.get_or_add_rPr()
rfonts = rpr.get_or_add_rFonts()
rfonts.set(qn("w:eastAsia"), "Times New Roman")

for name, size in (("Heading 1", 14), ("Heading 2", 12.5)):
    st = doc.styles[name]
    st.font.name = "Times New Roman"
    st.font.size = Pt(size)
    st.font.bold = True
    st.font.color.rgb = RGBColor(0, 0, 0)
    st.paragraph_format.space_before = Pt(12)
    st.paragraph_format.space_after = Pt(4)
    st.paragraph_format.line_spacing = 1.5

section = doc.sections[0]
section.top_margin = Inches(1)
section.bottom_margin = Inches(1)
section.right_margin = Inches(1)
section.left_margin = Inches(1.5)


def h1(text):
    doc.add_heading(text, level=1)


def para(text="", bold=False, italic=False, size=None):
    p = doc.add_paragraph()
    r = p.add_run(text)
    r.bold = bold
    r.italic = italic
    if size:
        r.font.size = Pt(size)
    return p


def bullet(text, bold_lead=None):
    p = doc.add_paragraph(style="List Bullet")
    p.paragraph_format.line_spacing = 1.5
    if bold_lead:
        r = p.add_run(bold_lead)
        r.bold = True
    p.add_run(text)
    return p


def make_table(headers, rows, widths=None, header=True):
    table = doc.add_table(rows=0, cols=len(headers))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER

    if header:
        cells = table.add_row().cells
        for i, text in enumerate(headers):
            p = cells[i].paragraphs[0]
            r = p.add_run(text)
            r.bold = True
            r.font.size = Pt(11)
            p.paragraph_format.line_spacing = 1.15
            p.paragraph_format.space_after = Pt(2)

    for row in rows:
        cells = table.add_row().cells
        for i, text in enumerate(row):
            p = cells[i].paragraphs[0]
            r = p.add_run(text)
            r.font.size = Pt(10.5)
            p.paragraph_format.line_spacing = 1.15
            p.paragraph_format.space_after = Pt(2)

    if widths:
        for row in table.rows:
            for i, w in enumerate(widths):
                row.cells[i].width = Inches(w)

    doc.add_paragraph()
    return table


# ------------------------------------------------------------------ heading
t = doc.add_paragraph()
t.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = t.add_run("Final Year Project — Progress Report (Development Diary)")
r.bold = True
r.font.size = Pt(15)

t2 = doc.add_paragraph()
t2.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = t2.add_run("Entry 2  ·  13 November 2026  ·  Boss Battle System")
r.font.size = Pt(12)
r.italic = True

t3 = doc.add_paragraph()
t3.alignment = WD_ALIGN_PARAGRAPH.CENTER
r = t3.add_run("Kuang Tsz Hei (22605458)  ·  Supervisor: VINCENT LAM")
r.font.size = Pt(10.5)
t3.paragraph_format.space_after = Pt(12)

make_table(
    ["Item", "Detail"],
    [
        ["Project", "Project Void: Breach (黑洞：突破收容) — 3D mobile action-puzzle"],
        ["Engine / platform", "Tuanjie (Unity) 2022.3.62t6, C#; Android and iOS"],
        ["My role", "Boss battle system and enemy AI"],
        ["Team", "Lin Siu Wai (black hole mechanics), Kuang Tsz Hei (boss), Yu Sai Ho (level design and assets)"],
        ["Period covered", "28 October – 13 November 2026"],
    ],
    widths=[1.4, 4.6],
)

# ------------------------------------------------------------------ 1
h1("1.  What I Did")

para(
    "Project Void: Breach is a 3D mobile game in which the player controls a black hole that has escaped a "
    "containment laboratory. My part is the military helicopter boss deployed in the final arena. The plan for "
    "this period was the boss finite state machine and the lock-on mechanism; both are now working, together "
    "with the attack systems built on top of them."
)

bullet(
    " four states (Idle, NormalAttack, BerserkMode, Defeated). Berserk starts at 50 % health with a faster "
    "fire rate, and Defeated disables the colliders, rolls the aircraft into a dive and destroys it.",
    bold_lead="State machine —",
)
bullet(
    " two muzzles fire a burst that is locked to the player's position at the moment it starts, so it can be "
    "dodged. A translucent red rectangle is projected on the ground for 0.7 s as a warning, then the impact "
    "point walks from the ground up to the player.",
    bold_lead="Machine-gun strafe —",
)
bullet(
    " each shot draws a short six-unit segment that travels to the impact point and disappears. The gaps "
    "between shots are what make it read as bullets rather than a laser.",
    bold_lead="Tracers —",
)
bullet(
    " a 0.25 s arming delay so it cannot detonate inside the aircraft, an explosion on timeout instead of "
    "silently vanishing, and a prefab refill once the four modelled missiles are used up.",
    bold_lead="Homing missile —",
)
bullet(
    " damage goes through an IDamageable interface instead of SendMessage, and the boss raises health and "
    "defeated events so a health bar can subscribe later.",
    bold_lead="Damage and health —",
)

# ------------------------------------------------------------------ 2
h1("2.  Problems Faced")

make_table(
    ["Problem", "Cause and fix"],
    [
        [
            "Missiles flew through the player and never exploded.",
            "The missile prefab already had a non-trigger collider and a kinematic rigidbody, and the player is "
            "kinematic too, so OnTriggerEnter could never fire. Fixed by forcing the collider to be a trigger at "
            "launch and adding the arming delay.",
        ],
        [
            "The boss stopped attacking after its first burst.",
            "CompareTag(\"Player\") threw because the tag was not defined in the project; the exception killed the "
            "coroutine before the attacking flag was reset. Fixed by defining the tag, switching to IDamageable, "
            "and resetting the flag in a finally block.",
        ],
        [
            "The attack could not be dodged at all.",
            "Every shot re-aimed at the player's live position with an instant raycast. Fixed by locking the aim "
            "line at the start of the burst and previewing it on the ground.",
        ],
    ],
    widths=[1.9, 4.1],
)

# ------------------------------------------------------------------ 3
h1("3.  Next Period (14 Nov – 11 Dec)")

bullet("connect the WarFX explosion prefabs to the missile detonation and the boss death. (20 Nov)")
bullet("boss health bar and hit feedback, using the events already in place. (20 Nov)")
bullet(
    "the counter-attack mechanic: the energy payload the boss drops during an attack must be absorbable by "
    "the black hole and launchable back at the boss. The drop point and the prefab hook already exist. (27 Nov)"
)
bullet("mobile performance pass: tracer count, particle budget, frame rate on a real handset. (27 Nov)")
bullet("a second attack pattern — a missile barrage — so the fight is not only machine-gun fire. (4 Dec)")
bullet(
    "boss arena transition and the camera sequence that introduces the fight, as listed in the proposal. (4 Dec)"
)
bullet(
    "play-test against the black-hole controller once my teammate's movement is in, then tune damage and "
    "the warning time from the results. (11 Dec)"
)

# ------------------------------------------------------------------ 4
h1("4.  Contribution")

make_table(
    ["Member", "Area", "Status"],
    [
        ["Kuang Tsz Hei", "Boss battle system and enemy AI", "Combat loop working in the editor"],
        ["Lin Siu Wai", "Black hole mechanics and physics", "In progress"],
        ["Yu Sai Ho", "Level design, puzzles and asset integration", "In progress"],
    ],
    widths=[1.3, 2.9, 1.8],
)

saved_path = None
candidates = [OUT] + [OUT.replace(".docx", f"_v{i}.docx") for i in range(2, 10)]

for path in candidates:
    try:
        doc.save(path)
        saved_path = path
        break
    except PermissionError:
        continue

if saved_path is None:
    raise SystemExit("every candidate path is locked")

print("saved:", saved_path, os.path.getsize(saved_path))
