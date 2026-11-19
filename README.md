# Project Void: Breach (黑洞：突破收容)

A 3D mobile action-puzzle game for **06CIT4002 Final Year Project**, Higher Diploma in Computer Science, 2026–27.

The player controls a black hole that escapes from a containment laboratory, grows by absorbing matter, and fights its way to a final confrontation with a military helicopter gunship.

| | |
|---|---|
| **Team** | Lin Siu Wai · Kuang Tsz Hei · Yu Sai Ho |
| **Supervisor** | Vincent Lam |
| **Engine** | Tuanjie (Unity) 2022.3.62t6, C# |
| **Platform** | Android / iOS |
| **Repository** | Development diary + Unity project |

## Development diary

| Entry | Date | Focus | Link |
|---|---|---|---|
| 01 | 2026-10-15 | Concept, references and proposal; project set-up | [Entry 1](DevLog/2026-10-15-entry-01.md) |
| 02 | 2026-11-13 | Boss finite state machine, machine-gun strafing run, ground warning zone, homing missiles | [Entry 2](DevLog/2026-11-13-entry-02.md) |

Each entry records what was built, the problems encountered with their causes, and the plan for the next period.

## Repository layout

```
Assets/            Unity project assets and C# scripts
  Script/          BossController, HomingMissile, IDamageable, PlayerHealth
ProjectSettings/   Unity project settings
Packages/          Package manifest
DevLog/            Development diary entries (Markdown)
Docs/              Submitted progress reports (.docx)
```

## Division of work

| Member | Responsibility |
|---|---|
| Lin Siu Wai | Black hole mechanics — mobile touch control, growth and scaling, absorption, HUD |
| Kuang Tsz Hei | Boss battle system and enemy AI — state machine, attack patterns, hit detection, death |
| Yu Sai Ho | Level design — lab, city and boss arena, environmental puzzles, asset integration |
