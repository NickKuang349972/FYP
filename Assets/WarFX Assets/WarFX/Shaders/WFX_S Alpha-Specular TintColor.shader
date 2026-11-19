// WarFX 双管线 Shader：同一文件内含 URP 与 Built-in 两套 SubShader。
// Unity 按当前渲染管线自动选择：
//   - URP      → 第一个 SubShader（RenderPipeline=UniversalPipeline 标签，HLSL；URP 库已内置在 Shaders/URPLib/，未安装 URP 包的 Built-in 工程导入也能正常编译）
//   - Built-in → 第二个 SubShader（RenderPipeline=""，CG，原版公式逐行保留）
// 因此切换管线（Project Settings → Graphics）不需要更换任何材质。

// WarFX URP
// 对应内置版 "WFX/Transparent Specular"（原版是 BlinnPhong Surface Shader）

Shader "WFX/Transparent Specular"
{
	Properties
	{
		_TintColor ("Main Color", Color) = (1,1,1,1)
		_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 0)
		_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
		_Shininess ("Shininess", Range (0.01, 1)) = 0.078125
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
		}

		Blend SrcAlpha OneMinusSrcAlpha
		ZWrite Off
		Cull Off

		Pass
		{
			Name "WarFXTransparentSpecular"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex WarFXVert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "WarFXURPCommon.hlsl"
			#include "URPLib/Lighting.hlsl"

			half4 frag (WarFXVaryings i) : SV_Target
			{
				half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

				half3 albedo = tex.rgb * _TintColor.rgb * i.color.rgb;
				half  alpha  = tex.a * _TintColor.a * i.color.a;

				float3 normalWS = normalize(i.normalWS);
				float3 viewDirWS = normalize(GetWorldSpaceViewDir(i.positionWS));

				Light mainLight = GetMainLight();
				half ndotl = saturate(dot(normalWS, mainLight.direction));
				half3 lighting = mainLight.color * ndotl + SampleSH(normalWS);

				// 原版 BlinnPhong 用 _Shininess 同时驱动高光强度与锐度，这里做等价近似
				half3 halfDir = normalize(mainLight.direction + viewDirWS);
				half specPower = lerp(8.0h, 128.0h, saturate(_Shininess));
				half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * _Shininess;

				half4 col = half4(albedo * lighting, alpha);
				// BlinnPhong 的高光色来自 _SpecColor.rgb，保持与内置版一致
				col.rgb += mainLight.color * spec * tex.rgb * _TintColor.rgb * _SpecColor.rgb;
				col.rgb = MixFog(col.rgb, i.fogFactor);
				return col;
			}
			ENDHLSL
		}
	}

	// ---------------- Built-in（原版 CG，公式逐行保留；RenderPipeline=""，排第二） ----------------
	SubShader
	{
		Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="" }
		Cull Off
		LOD 200

		CGPROGRAM
		#pragma surface surf BlinnPhong alpha noforwardadd approxview alpha:fade

		sampler2D _MainTex;
		fixed4 _TintColor;
		half _Shininess;

		struct Input
		{
			float2 uv_MainTex;
			float4 color:COLOR;
		};

		void surf (Input IN, inout SurfaceOutput o)
		{
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = c.rgb * _TintColor.rgb * IN.color.rgb;
			o.Alpha = c.a * _TintColor.a * IN.color.a;
			o.Specular = _Shininess;
			o.Gloss = c.rgb + 0.1;
		}
		ENDCG
	}

	Fallback Off
}
