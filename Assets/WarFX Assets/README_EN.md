# War FX

WarFX war effects pack (v1.8, Jean Moreno), production-ready edition: **Built-in and URP dual pipeline support**. Every shader uses a single-file dual-SubShader architecture — the same shader file contains both rendering paths, materials never need to change, and switching pipelines requires zero migration. The scripts are namespaced under `WarFX` and include several bug fixes over the original.

## Render Pipeline Support

| Pipeline | Status | Notes |
|---|---|---|
| Built-in | ✅ Supported | First SubShader of every shader (CG program), formulas identical to the original |
| URP | ✅ Supported | SubShader tagged `"RenderPipeline"="UniversalPipeline"`; all materials verified |
| HDRP | ❌ Not supported | Would require a full rewrite; not provided |

Unity automatically selects the SubShader matching the current pipeline, so **material GUIDs never change and switching pipelines requires no material edits**. Built-in projects do not need the URP package installed — the URP HLSL library is bundled with the pack (`Shaders/URPLib/`, from official URP 14.0.12 source).

Pipeline switching follows the official Unity flow: set or clear `Scriptable Render Pipeline Settings` under `Project Settings → Graphics` (and per Quality level). **This package ships no auto-switching scripts or editor menus.**

## Contents

```
WarFX/
├── Runtime/          Effects/ and Pooling/, assembly WarFX.Runtime (referencable from your code)
├── Editor/           Object pool inspector, assembly WarFX.Editor
├── Demo/             Demo scene (WarFX Demo New.unity), assembly WarFX.Demo
├── Shaders/          13 dual-pipeline shaders + shared include + Demo Lit
├── Desktop/ Mobile/  Materials and textures
└── _Effects/         Effect prefabs (desktop / mobile)
```

## Getting Started

1. Import the `WarFX Assets` folder into your project (Built-in or URP both work);
2. Open `WarFX/Demo/WarFX Demo New.unity` to preview all effects;
3. Drag prefabs from `_Effects/` into your scene, or use `CFX_SpawnSystem` (WarFX/Runtime/Pooling) for pooled preloading.

## Shader List

| Shader | Blend Mode | Notes |
|---|---|---|
| `WFX/Additive Alpha8` | One One | |
| `WFX/Additive (Soft) Alpha8` | OneMinusDstColor One | |
| `WFX/Multiply Alpha8` | Zero SrcColor | |
| `WFX/Multiply Soft Tint` | DstColor SrcColor | |
| `WFX/Scroll/Additive` | One One | Scrolling UV |
| `WFX/Scroll/Alpha Blended` | SrcAlpha OneMinusSrcAlpha | Scrolling UV |
| `WFX/Scroll/Smoke` | DstColor SrcAlpha | Scrolling UV |
| `WFX/Scroll/Multiply Soft Tint` | DstColor SrcColor | Scrolling UV |
| `WFX/Alpha Blended (No Soft Particles)` | SrcAlpha OneMinusSrcAlpha | |
| `WFX/Transparent Diffuse` | Lit | Surface shader, dual pipeline |
| `WFX/Transparent Specular` | Lit | Surface shader, dual pipeline |
| `WFX/Particles/Additive` | SrcAlpha One | Replaces Legacy Particles/Additive |
| `WFX/Particles/Additive (Soft)` | OneMinusDstColor One | Replaces Legacy Particles/Additive (Soft) |
| `WFX/Demo Lit` | Demo ground / gun model | URP=handwritten PBR, Built-in=Surface Standard |

Property names are unchanged (`_MainTex` / `_TintColor` / `_InvFade` / `_ScrollSpeed` / `_Shininess`); URP-side properties live in a `UnityPerMaterial` CBUFFER for SRP Batcher compatibility.

## Soft Particles

- **URP**: per-material toggle — enable `Soft Particles` on the material (keyword `_SOFTPARTICLES_ON`). You must enable `Depth Texture` on your URP Asset first, otherwise the depth sampling is invalid;
- **Built-in**: uses the global `QualitySettings.softParticles` toggle;
- Disabled by default.

## Behavioral Differences from the Original

| Item | Difference |
|---|---|
| Fog | URP-side additive/multiplicative shaders interpolate toward a fixed fog color via `MixFogColor`; when scene fog is off nothing is applied — same semantics as built-in `UNITY_APPLY_FOG` |
| Soft particles | See section above |
| Transparency sorting | URP sorts by object; `DstColor` multiplicative smoke may sort differently — adjust the renderer's `Sorting Priority` if needed |
| Performance | URP side is SRP Batcher compatible; Built-in CG side is not |

## Script Fixes over v1.8

- `WFX_BulletHoleDecal`: wrong atlas row for non-square sheets, per-decal material clones leaking memory (now `MaterialPropertyBlock`), null refs after domain reload;
- `CFX_SpawnSystem`: unprotected static entry point, preloading array out-of-bounds, duplicate instances silently overwriting;
- `CFX_AutoDestructShuriken`: string-based coroutines and overlapping coroutine accumulation (GC/performance);
- `CFX_LightIntensityFade` / `WFX_LightFlicker`: broken after object pooling (now restart from OnEnable).

## Known Limitations

- Soft particles are disabled by default — see the section above to enable;
- Mobile prefabs share the same shaders as desktop, no separate trimming;
- Some effects look darker in Linear color space (upstream issue); compensate via Tint Color / Start Color.

## Copyright

Original effects pack: War FX v1.8 (2019/03/20, © Jean Moreno). This edition adds production hardening on top of it (namespaces, assemblies, bug fixes, dual-pipeline shaders); those modifications are distributed with the pack.
