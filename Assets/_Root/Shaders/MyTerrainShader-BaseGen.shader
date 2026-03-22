// Toony Colors Pro+Mobile 2
// (c) 2014-2022 Jean Moreno

// Terrain BaseGen shader:
// This shader is used to generate full blended terrain maps at a low resolution, that will show if the camera is at the "Base Distance" setting of the terrain.
// This is a LOD-like system that prevents doing the full splat maps blending when the terrain is viewed from far away, and instead sample those generated maps only once.

Shader "Hidden/MyTerrainShader-BaseGen"
{
	Properties
	{
		[HideInInspector] _DstBlend("DstBlend", Float) = 0.0
	}
	SubShader
	{
		CGINCLUDE

		#include "UnityCG.cginc"
		sampler2D _Control;
		float4 _Control_ST;
		float4 _Control_TexelSize;

		float4 _Splat0_ST;
		float4 _Splat1_ST;
		float4 _Splat2_ST;
		float4 _Splat3_ST;

		struct Attributes
		{
			float4 vertex : POSITION;
			float2 texcoord : TEXCOORD0;
		};

		struct Varyings_Textures
		{
			float4 vertex : SV_POSITION;
			float2 texcoord0 : TEXCOORD0;
			float2 texcoord1 : TEXCOORD1;
			float2 texcoord2 : TEXCOORD2;
			float2 texcoord3 : TEXCOORD3;
			float2 texcoord4 : TEXCOORD4;
		};

		float2 ComputeControlUV(float2 uv, float4 texelSize)
		{
			// adjust splatUVs so the edges of the terrain tile lie on pixel centers
			return (uv * (texelSize.zw - 1.0f) + 0.5f) * texelSize.xy;
		}

		Varyings_Textures vert_textures(Attributes input)
		{
			Varyings_Textures output = (Varyings_Textures)0;
			output.vertex = UnityObjectToClipPos(input.vertex);
			float2 uv = TRANSFORM_TEX(input.texcoord, _Control);
			output.texcoord0.xy = ComputeControlUV(uv, _Control_TexelSize);
			output.texcoord1.xy = TRANSFORM_TEX(uv, _Splat0);
			output.texcoord2.xy = TRANSFORM_TEX(uv, _Splat1);
			output.texcoord3.xy = TRANSFORM_TEX(uv, _Splat2);
			output.texcoord4.xy = TRANSFORM_TEX(uv, _Splat3);
			return output;
		}

		struct Varyings_Simple
		{
			float4 vertex : SV_POSITION;
			float2 texcoord0 : TEXCOORD0;
		};

		Varyings_Simple vert_simple(Attributes input)
		{
			Varyings_Simple output = (Varyings_Simple)0;
			output.vertex = UnityObjectToClipPos(input.vertex);
			output.texcoord0 = ComputeControlUV(TRANSFORM_TEX(input.texcoord, _Control), _Control_TexelSize);
			return output;
		}

		ENDCG

		Pass
		{
			Tags
			{
				"Name" = "_MainTex"
				"Format" = "RGBA32"
				"Size" = "1"
			}

			ZTest Always
			ZWrite Off
			Cull Off
			Blend One [_DstBlend]

			CGPROGRAM

			#pragma vertex vert_textures
			#pragma fragment frag

			sampler2D _Splat0, _Splat1, _Splat2, _Splat3;

			float4 frag(Varyings_Textures input) : SV_Target
			{
				float4 alpha = tex2D(_Control, input.texcoord0.xy);

				float4 splat0 = tex2D(_Splat0, input.texcoord1.xy);
				float4 splat1 = tex2D(_Splat1, input.texcoord2.xy);
				float4 splat2 = tex2D(_Splat2, input.texcoord3.xy);
				float4 splat3 = tex2D(_Splat3, input.texcoord4.xy);

				float4 albedoSmoothness = splat0 * alpha.x;
				albedoSmoothness += splat1 * alpha.y;
				albedoSmoothness += splat2 * alpha.z;
				albedoSmoothness += splat3 * alpha.w;

				return albedoSmoothness;
			}

			ENDCG
		}

	}
	Fallback Off

}

