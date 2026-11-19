// WarFX URP 公共部分
// 顶点/结构体/软粒子/雾效统一放这里，11 个 URP Shader 共用，避免公式各写一遍走形。

#ifndef WARFX_URP_COMMON_INCLUDED
#define WARFX_URP_COMMON_INCLUDED

#include "URPLib/Core.hlsl"
#include "URPLib/DeclareDepthTexture.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

// SRP Batcher 要求所有材质属性集中在同一个 CBUFFER 内
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4  _TintColor;
    float  _InvFade;
    float  _ScrollSpeed;
    float  _SoftParticlesOn;
    // 仅受光弹孔（Transparent Specular）使用
    float  _Shininess;
    half4  _SpecColor;
CBUFFER_END

struct WarFXAttributes
{
    float4 positionOS : POSITION;
    float4 color      : COLOR;
    float3 normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
};

struct WarFXVaryings
{
    float4 positionCS : SV_POSITION;
    float4 color      : COLOR;
    float2 uv         : TEXCOORD0;
    float4 screenPos  : TEXCOORD1;
    float  fogFactor  : TEXCOORD2;
    float3 normalWS   : TEXCOORD3;
    float3 positionWS : TEXCOORD4;
};

WarFXVaryings WarFXVert(WarFXAttributes v)
{
    WarFXVaryings o;
    VertexPositionInputs posInput = GetVertexPositionInputs(v.positionOS.xyz);

    o.positionCS = posInput.positionCS;
    o.positionWS = posInput.positionWS;
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.color = v.color;
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.screenPos = ComputeScreenPos(o.positionCS);
    o.fogFactor = ComputeFogFactor(o.positionCS.z);
    return o;
}

// 软粒子：只在该材质显式开启关键字时生效。
// 未在 URP Asset 上勾选 Depth Texture 时，深度纹理不可用会得到错误衰减，所以默认关闭。
half WarFXSoftFade(float4 screenPos)
{
#if defined(_SOFTPARTICLES_ON)
    float2 screenUV = screenPos.xy / screenPos.w;
    float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
    float partEyeDepth = screenPos.w;
    return saturate(_InvFade * (sceneEyeDepth - partEyeDepth));
#else
    return 1.0h;
#endif
}

// 加法/乘法混合的雾：内置管线里这些 Shader 用 Fog{Color(0,0,0,0)} / (1,1,1,1) / (0.5..)
// 本质是「向固定雾色插值」。必须走 URP 的 MixFogColor：
// - 场景雾关闭时不做任何修改（与内置 UNITY_APPLY_FOG 在雾关闭时编译为空一致）；
// - 雾开启时按 URP 的强度曲线向指定雾色插值（加法向黑 = 淡出，乘法向白/灰 = 淡出）。
half4 WarFXApplyFogTowards(half4 color, float fogFactor, half fogColor)
{
    color.rgb = MixFogColor(color.rgb, half3(fogColor, fogColor, fogColor), fogFactor);
    return color;
}

#endif // WARFX_URP_COMMON_INCLUDED
