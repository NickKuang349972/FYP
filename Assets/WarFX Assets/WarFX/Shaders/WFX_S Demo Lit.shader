// WarFX 双管线 Shader：Demo 场景环境材质用（替代 Standard / URP Lit 的双管线合体）。
// Unity 按当前渲染管线自动选择：
//   - URP      → 第一个 SubShader（RenderPipeline=UniversalPipeline 标签，HLSL；URP 库已内置在 Shaders/URPLib/，未安装 URP 包的 Built-in 工程导入也能正常编译）
//   - Built-in → 第二个 SubShader（RenderPipeline=""，CG，原版公式逐行保留）
// 属性名与 URP/Lit 一致（_BaseMap/_BaseColor/_Metallic/_Smoothness），
// 材质从 URP/Lit 换到本 Shader 时数值自动保留。

Shader "WFX/Demo Lit"
{
	Properties
	{
		_BaseColor ("Base Color", Color) = (1,1,1,1)
		_BaseMap ("Base Map", 2D) = "white" {}
		_BumpScale ("Normal Scale", Float) = 1.0
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
		_OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0
		_OcclusionMap ("Occlusion", 2D) = "white" {}
	}

	// ---------------- URP（RenderPipeline=UniversalPipeline 标签，排首；URP 库已内置在 URPLib/） ----------------
	SubShader
	{
		Tags
		{
			"Queue" = "Geometry"
			"RenderType" = "Opaque"
			"RenderPipeline" = "UniversalPipeline"
		}

		Pass
		{
			Name "WarFXDemoLitForward"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local _OCCLUSIONMAP
			#pragma multi_compile_fog
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
			#pragma multi_compile _ _SHADOWS_SOFT

			#include "URPLib/Core.hlsl"
			#include "URPLib/Lighting.hlsl"

			TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
			TEXTURE2D(_BumpMap);       SAMPLER(sampler_BumpMap);
			TEXTURE2D(_OcclusionMap);  SAMPLER(sampler_OcclusionMap);

			CBUFFER_START(UnityPerMaterial)
				float4 _BaseMap_ST;
				half4  _BaseColor;
				float  _BumpScale;
				float  _Metallic;
				float  _Smoothness;
				float  _OcclusionStrength;
			CBUFFER_END

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float4 tangentOS  : TANGENT;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uv         : TEXCOORD0;
				float3 normalWS   : TEXCOORD1;
				float4 tangentWS  : TEXCOORD2;
				float3 positionWS : TEXCOORD3;
			};

			Varyings vert (Attributes v)
			{
				Varyings o;
				VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
				o.positionCS = p.positionCS;
				o.positionWS = p.positionWS;
				o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
				o.normalWS = TransformObjectToWorldNormal(v.normalOS);
				o.tangentWS = float4(normalize(TransformObjectToWorldDir(v.tangentOS.xyz)), v.tangentOS.w);
				return o;
			}

			half4 frag (Varyings i) : SV_Target
			{
				half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
				half3 albedo = baseMap.rgb * _BaseColor.rgb;

				half3 normalTS = half3(0, 0, 1);
			#if defined(_NORMALMAP)
				normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
			#endif
				half occ = 1.0h;
			#if defined(_OCCLUSIONMAP)
				occ = lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, i.uv).g, _OcclusionStrength);
			#endif

				InputData d = (InputData)0;
				d.positionWS = i.positionWS;
				d.positionCS = i.positionCS;
				d.normalWS = NormalizeNormalPerVertex(i.normalWS);
			#if defined(_NORMALMAP)
				half3 bitangent = i.tangentWS.w * cross(d.normalWS, i.tangentWS.xyz);
				d.normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(i.tangentWS.xyz, bitangent, d.normalWS)));
			#endif
				d.viewDirectionWS = GetWorldSpaceViewDir(i.positionWS);
				d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
				d.fogCoord = ComputeFogFactor(i.positionCS.z);
				d.bakedGI = SampleSH(d.normalWS) * occ;
				d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
				d.shadowMask = half4(1, 1, 1, 1);

				SurfaceData s = (SurfaceData)0;
				s.albedo = albedo;
				s.alpha = _BaseColor.a;
				s.metallic = _Metallic;
				s.smoothness = _Smoothness;
				s.occlusion = occ;
				s.normalTS = normalTS;

				half4 col = UniversalFragmentPBR(d, s);
				col.rgb = MixFog(col.rgb, d.fogCoord);
				return col;
			}
			ENDHLSL
		}
	}

	// ---------------- Built-in（原版 CG，公式逐行保留；RenderPipeline=""，排第二） ----------------
	SubShader
	{
		Tags { "Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="" }

		CGPROGRAM
		#pragma surface surf Standard fullforwardshadows nolightmap
		#pragma target 3.0
		#pragma shader_feature_local _NORMALMAP
		#pragma shader_feature_local _OCCLUSIONMAP

		sampler2D _BaseMap;
		fixed4 _BaseColor;
		sampler2D _BumpMap;
		half _BumpScale;
		half _Metallic;
		half _Smoothness;
		sampler2D _OcclusionMap;
		half _OcclusionStrength;

		struct Input
		{
			float2 uv_BaseMap;
		};

		void surf (Input IN, inout SurfaceOutputStandard o)
		{
			fixed4 c = tex2D(_BaseMap, IN.uv_BaseMap) * _BaseColor;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
		#if defined(_NORMALMAP)
			o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_BaseMap), _BumpScale);
		#endif
		#if defined(_OCCLUSIONMAP)
			o.Occlusion = lerp(1, tex2D(_OcclusionMap, IN.uv_BaseMap).g, _OcclusionStrength);
		#endif
		}
		ENDCG
	}

	Fallback Off
}
