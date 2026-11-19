// WarFX 双管线 Shader：同一文件内含 URP 与 Built-in 两套 SubShader。
// Unity 按当前渲染管线自动选择：
//   - URP      → 第一个 SubShader（RenderPipeline=UniversalPipeline 标签，HLSL；URP 库已内置在 Shaders/URPLib/，未安装 URP 包的 Built-in 工程导入也能正常编译）
//   - Built-in → 第二个 SubShader（RenderPipeline=""，CG，原版公式逐行保留）
// 因此切换管线（Project Settings → Graphics）不需要更换任何材质。

// WarFX URP
// 对应内置版 "WFX/Alpha Blended (No Soft Particles)"

Shader "WFX/Alpha Blended (No Soft Particles)"
{
	Properties
	{
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_MainTex ("Particle Texture", 2D) = "white" {}
		// 原版声明但未参与计算；保留该声明以免切换管线后材质上的 _InvFade 数值被丢弃
		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
	}

	// ---------------- URP（RenderPipeline=UniversalPipeline 标签，排首；URP 库已内置在 URPLib/） ----------------
	SubShader
	{
		Tags
		{
			"Queue" = "Transparent"
			"RenderType" = "Transparent"
			"RenderPipeline" = "UniversalPipeline"
			"IgnoreProjector" = "True"
			"PreviewType" = "Plane"
		}

		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB
		ZWrite Off
		Cull Off

		Pass
		{
			Name "WarFXAlphaBlend"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex WarFXVert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "WarFXURPCommon.hlsl"

			half4 frag (WarFXVaryings i) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				half4 col = 2.0h * i.color * _TintColor * tex;

				col.rgb = MixFog(col.rgb, i.fogFactor);
				return col;
			}
			ENDHLSL
		}
	}

	// ---------------- Built-in（原版 CG，公式逐行保留；RenderPipeline=""，排第二） ----------------
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="" }
		Blend SrcAlpha OneMinusSrcAlpha
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_particles
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _TintColor;

			struct appdata_t
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				UNITY_FOG_COORDS(1)
			};

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = 2.0f * i.color * _TintColor * tex2D(_MainTex, i.texcoord);
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}

	Fallback Off
}
