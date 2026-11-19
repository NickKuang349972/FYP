// WarFX 双管线 Shader：同一文件内含 URP 与 Built-in 两套 SubShader。
// Unity 按当前渲染管线自动选择：
//   - URP      → 第一个 SubShader（RenderPipeline=UniversalPipeline 标签，HLSL；URP 库已内置在 Shaders/URPLib/，未安装 URP 包的 Built-in 工程导入也能正常编译）
//   - Built-in → 第二个 SubShader（RenderPipeline=""，CG，原版公式逐行保留）
// 因此切换管线（Project Settings → Graphics）不需要更换任何材质。

// WarFX URP
// 对应内置版 "WFX/Additive (Soft) Alpha8"
// Blend OneMinusDstColor One

Shader "WFX/Additive (Soft) Alpha8"
{
	Properties
	{
		_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
		_MainTex ("Particle Texture (alpha)", 2D) = "white" {}
		_InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0
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

		Blend OneMinusDstColor One
		ZWrite Off
		Cull Off

		Pass
		{
			Name "WarFXAdditiveSoft"
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

				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
				half4 col = i.color;
				col.a *= softFade;

				col = 2.0h * col * _TintColor * tex.a;

				col = WarFXApplyFogTowards(col, i.fogFactor, 0.0h);
				return col;
			}
			ENDHLSL
		}
	}

	// ---------------- Built-in（原版 CG，公式逐行保留；RenderPipeline=""，排第二） ----------------
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="" }
		Blend OneMinusDstColor One
		Cull Off Lighting Off ZWrite Off Fog { Color (0,0,0,0) }
		BindChannels
		{
			Bind "Color", color
			Bind "Vertex", vertex
			Bind "TexCoord", texcoord
		}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest
			#pragma multi_compile_particles

			#include "UnityCG.cginc"

			sampler2D _MainTex;
			fixed4 _TintColor;

			struct appdata_t
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float2 texcoord : TEXCOORD0;
				#ifdef SOFTPARTICLES_ON
				float4 projPos : TEXCOORD1;
				#endif
			};

			float4 _MainTex_ST;

			v2f vert (appdata_t v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				#ifdef SOFTPARTICLES_ON
				o.projPos = ComputeScreenPos (o.vertex);
				COMPUTE_EYEDEPTH(o.projPos.z);
				#endif
				o.color = v.color;
				o.texcoord = TRANSFORM_TEX(v.texcoord,_MainTex);
				return o;
			}

			sampler2D _CameraDepthTexture;
			float _InvFade;

			fixed4 frag (v2f i) : COLOR
			{
			#ifdef SOFTPARTICLES_ON
				float sceneZ = LinearEyeDepth (UNITY_SAMPLE_DEPTH(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos))));
				float partZ = i.projPos.z;
				float fade = saturate (_InvFade * (sceneZ-partZ));
				i.color.a *= fade;
				#endif

				return 2.0 * i.color * _TintColor * tex2D(_MainTex, i.texcoord).a;
			}
			ENDCG
		}
	}

	Fallback Off
}
