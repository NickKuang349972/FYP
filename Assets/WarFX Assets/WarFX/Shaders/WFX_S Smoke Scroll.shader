// WarFX 双管线 Shader：同一文件内含 URP 与 Built-in 两套 SubShader。
// Unity 按当前渲染管线自动选择：
//   - URP      → 第一个 SubShader（RenderPipeline=UniversalPipeline 标签，HLSL；URP 库已内置在 Shaders/URPLib/，未安装 URP 包的 Built-in 工程导入也能正常编译）
//   - Built-in → 第二个 SubShader（RenderPipeline=""，CG，原版公式逐行保留）
// 因此切换管线（Project Settings → Graphics）不需要更换任何材质。

// WarFX URP
// 对应内置版 "WFX/Scroll/Smoke"
// Blend DstColor SrcAlpha，向 50% 灰插值（原版的"软"是颜色上浮，不是深度淡出）

Shader "WFX/Scroll/Smoke"
{
	Properties
	{
		_MainTex ("Looped Texture + Alpha Mask", 2D) = "white" {}
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
		_ScrollSpeed ("Scroll Speed", Float) = 2.0
		[Toggle(_SOFTPARTICLES_ON)] _SoftParticlesOn ("Soft Particles", Float) = 0
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

		Blend DstColor SrcAlpha
		ZWrite Off
		Cull Off

		Pass
		{
			Name "WarFXScrollSmoke"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex WarFXVert
			#pragma fragment frag
			#pragma shader_feature_local _SOFTPARTICLES_ON
			#pragma multi_compile_fog

			#include "WarFXURPCommon.hlsl"

			half4 frag (WarFXVaryings i) : SV_Target
			{
				half softFade = WarFXSoftFade(i.screenPos);

				half4 vcol = i.color;
				vcol.a *= softFade;

				half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * vcol.a;

				float2 uv = i.uv;
				uv.y -= fmod(_Time.x * _ScrollSpeed, 1.0);

				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
				tex.rgb *= vcol.rgb * _TintColor.rgb;
				tex.a = mask;
				tex = lerp(half4(0.5h, 0.5h, 0.5h, 0.5h), tex, mask);

				return WarFXApplyFogTowards(tex, i.fogFactor, 0.5h);
			}
			ENDHLSL
		}
	}

	// ---------------- Built-in（原版 CG，公式逐行保留；RenderPipeline=""，排第二） ----------------
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="" }
		Blend DstColor SrcAlpha
		Cull Off Lighting Off ZWrite Off Fog { Color (0.5,0.5,0.5,0.5) }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#include "UnityCG.cginc"

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct vdata
			{
				float4 vertex : POSITION;
				half2 texcoord : TEXCOORD0;
				fixed4 color : COLOR;
			};

			fixed4 _TintColor;
			sampler2D _MainTex;
			float _ScrollSpeed;

			v2f vert (vdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos (v.vertex);
				o.color = v.color;
				o.uv = v.texcoord;
				return o;
			}

			fixed4 frag (v2f i) : COLOR0
			{
				float mask = tex2D(_MainTex, i.uv).a * i.color.a;
				i.uv.y -= fmod(_Time*_ScrollSpeed,1);
				fixed4 tex = tex2D(_MainTex, i.uv);
				tex.rgb *= i.color.rgb * _TintColor.rgb;
				tex.a = mask;
				tex = lerp(fixed4(0.5,0.5,0.5,0.5), tex, mask);
				return tex;
			}

			ENDCG
		}
	}

	Fallback Off
}
