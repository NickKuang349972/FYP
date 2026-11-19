# War FX

WarFX 战争特效包（v1.8，Jean Moreno）工程化版本：**内置管线（Built-in）与 URP 双管线支持**，所有 Shader 采用单文件双 SubShader 架构——同一份 Shader 同时包含两条渲染路径，材质永不切换，切管线零迁移成本。脚本层完成命名空间隔离（`WarFX`）与若干缺陷修复。

## 渲染管线支持

| 管线 | 状态 | 说明 |
|---|---|---|
| Built-in | ✅ 支持 | 每个 Shader 的第一个 SubShader（CG 程序），公式与原版一致 |
| URP | ✅ 支持 | 带 `"RenderPipeline"="UniversalPipeline"` 标签的 SubShader；全部材质已验证渲染 |
| HDRP | ❌ 不支持 | 需要完全重写，暂未提供 |

Unity 会自动为每个 Shader 选择匹配当前管线的 SubShader，**材质引用的 GUID 不变，切换管线不需要改任何材质**。Built-in 工程无需安装 URP 包——URP 的 HLSL 库已随包内置（`Shaders/URPLib/`，源自 URP 14.0.12 官方源码）。

切换管线走 Unity 官方流程：`Project Settings → Graphics` 设置/清空 `Scriptable Render Pipeline Settings`（Quality 各档同理）。**本包不提供任何自动切换脚本或编辑器菜单。**

## 内容结构

```
WarFX/
├── Runtime/          Effects/ 与 Pooling/，程序集 WarFX.Runtime（可直接被你的代码引用）
├── Editor/           对象池 Inspector，程序集 WarFX.Editor
├── Demo/             演示场景（WarFX Demo New.unity），程序集 WarFX.Demo
├── Shaders/          13 个双管线 Shader + 公共 include + Demo Lit
├── Desktop/ Mobile/  材质与贴图
└── _Effects/         特效预制体（桌面版 / 移动版）
```

## 快速开始

1. 将 `WarFX Assets` 文件夹导入你的工程（Built-in 或 URP 均可）；
2. 打开 `WarFX/Demo/WarFX Demo New.unity` 查看全部特效；
3. 把 `_Effects/` 下的预制体拖入场景，或配合 `CFX_SpawnSystem`（WarFX/Runtime/Pooling）做对象池预加载。

## Shader 清单

| Shader | 混合模式 | 说明 |
|---|---|---|
| `WFX/Additive Alpha8` | One One | |
| `WFX/Additive (Soft) Alpha8` | OneMinusDstColor One | |
| `WFX/Multiply Alpha8` | Zero SrcColor | |
| `WFX/Multiply Soft Tint` | DstColor SrcColor | |
| `WFX/Scroll/Additive` | One One | 滚动 UV |
| `WFX/Scroll/Alpha Blended` | SrcAlpha OneMinusSrcAlpha | 滚动 UV |
| `WFX/Scroll/Smoke` | DstColor SrcAlpha | 滚动 UV |
| `WFX/Scroll/Multiply Soft Tint` | DstColor SrcColor | 滚动 UV |
| `WFX/Alpha Blended (No Soft Particles)` | SrcAlpha OneMinusSrcAlpha | |
| `WFX/Transparent Diffuse` | 受光 | 原 Surface Shader 双管线化 |
| `WFX/Transparent Specular` | 受光 | 原 Surface Shader 双管线化 |
| `WFX/Particles/Additive` | SrcAlpha One | 替代 Legacy Particles/Additive |
| `WFX/Particles/Additive (Soft)` | OneMinusDstColor One | 替代 Legacy Particles/Additive (Soft) |
| `WFX/Demo Lit` | Demo 场景地面/枪模 | URP=手写 PBR，Built-in=Surface Standard |

属性名保持一致（`_MainTex` / `_TintColor` / `_InvFade` / `_ScrollSpeed` / `_Shininess`），URP 侧属性已收进 `UnityPerMaterial` CBUFFER，可进 SRP Batcher。

## 软粒子

- **URP**：材质级开关——材质上勾选 `Soft Particles`（关键字 `_SOFTPARTICLES_ON`），启用前必须先在 URP Asset 打开 `Depth Texture`，否则深度采样无效；
- **Built-in**：沿用 `QualitySettings.softParticles` 全局开关；
- 默认关闭。

## 与原版的行为差异

| 项 | 差异 |
|---|---|
| 雾 | URP 侧加法/乘法类 Shader 用 `MixFogColor` 向固定雾色插值，场景雾关闭时不做处理——与内置 `UNITY_APPLY_FOG` 语义一致 |
| 软粒子 | 见上节 |
| 透明排序 | URP 按物体排序，`DstColor` 乘法混合的烟团可能出现穿插顺序差异，必要时调渲染器 `Sorting Priority` |
| 性能 | URP 侧可进 SRP Batcher；Built-in 侧不支持 |

## 已修复的脚本缺陷（相对 v1.8 原版）

- `WFX_BulletHoleDecal`：非方形图集行号取错、每弹孔克隆材质泄漏（改 `MaterialPropertyBlock`）、域重载后空引用；
- `CFX_SpawnSystem`：静态入口无实例保护、预载数组越界、重复实例覆盖；
- `CFX_AutoDestructShuriken`：字符串协程与重复协程叠加导致的 GC 与性能问题；
- `CFX_LightIntensityFade` / `WFX_LightFlicker`：对象池复用后失效（改 OnEnable 启动）。

## 已知限制

- 软粒子默认关闭，开启方法见上节；
- 移动版预制体与桌面版共用同一套 Shader，未做单独裁剪；
- 线性色彩空间（Linear）下部分特效偏暗（上游已知问题），可调 Tint Color / Start Color 补偿。

## 版权说明

原始特效包：War FX v1.8（2019/03/20，© Jean Moreno）。本版本在其基础上做了工程化改造（命名空间、程序集划分、缺陷修复、双管线 Shader），改造内容随包分发。
