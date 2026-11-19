# 版本说明 / Release Notes

## v2.0（2026-11-03）

基于 War FX v1.8（© Jean Moreno）的工程化升级版本。

### 中文

**重大更新：内置管线（Built-in）与 URP 双管线支持**

- 全部 13 个 Shader 重构为**单文件双 SubShader 架构**：同一份 Shader 内置 Built-in 与 URP 两条渲染路径，Unity 自动按当前管线选择；
- **切换管线零迁移**：材质引用永不改变，在 Built-in 与 URP 之间来回切换不需要改任何材质或重新导入；
- **导入零依赖**：Built-in 工程无需安装 URP 包，导入即用、控制台零报错；
- 全部 106 个材质在两条管线下完成渲染验证，无洋红、无着色异常；
- URP 侧属性收进 `UnityPerMaterial` CBUFFER，支持 SRP Batcher；
- 脚本按程序集划分（`WarFX.Runtime` / `WarFX.Editor` / `WarFX.Demo`），统一 `WarFX` 命名空间，业务代码可直接引用；
- 附中英双语使用说明（README.md / README_EN.md）。

**缺陷修复（相对 v1.8 原版）**

- 弹孔贴花 `WFX_BulletHoleDecal`：非方形图集行号计算错误；每弹孔克隆材质导致内存泄漏（改用 MaterialPropertyBlock）；域重载后空引用崩溃；
- 对象池 `CFX_SpawnSystem`：静态入口无实例保护导致的空引用；预加载数组越界；重复实例静默覆盖；
- 自动销毁 `CFX_AutoDestructShuriken`：字符串协程与重复协程叠加导致的 GC 与性能问题；
- `CFX_LightIntensityFade` / `WFX_LightFlicker`：对象池复用后失效（改由 OnEnable 重新启动）。

**兼容性**：Unity 2022.3 LTS+（Built-in / URP 14）；不支持 HDRP。

---

### English

**Major update: Built-in and URP dual pipeline support**

- All 13 shaders rebuilt with a **single-file dual-SubShader architecture**: each shader contains both a Built-in and a URP rendering path, selected automatically by Unity;
- **Zero migration on pipeline switch**: material references never change — switching between Built-in and URP requires no material edits or re-imports;
- **Zero-dependency import**: Built-in projects do not need the URP package — import and use with a clean console;
- All 106 materials render-verified under both pipelines — no magenta, no shading artifacts;
- URP-side properties live in a `UnityPerMaterial` CBUFFER for SRP Batcher support;
- Scripts organized into assemblies (`WarFX.Runtime` / `WarFX.Editor` / `WarFX.Demo`) under the `WarFX` namespace, ready to reference from your gameplay code;
- Bilingual documentation included (README.md / README_EN.md).

**Bug fixes over v1.8**

- `WFX_BulletHoleDecal`: wrong atlas row for non-square sheets; per-decal material clones leaking memory (now MaterialPropertyBlock); null reference after domain reload;
- `CFX_SpawnSystem`: unprotected static entry point (null refs); preloading array out-of-bounds; duplicate instances silently overwriting;
- `CFX_AutoDestructShuriken`: string-based coroutines and overlapping coroutine accumulation (GC/performance);
- `CFX_LightIntensityFade` / `WFX_LightFlicker`: broken after object pooling (now restart from OnEnable).

**Compatibility**: Unity 2022.3 LTS+ (Built-in / URP 14); HDRP not supported.
