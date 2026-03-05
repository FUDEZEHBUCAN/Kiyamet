// Toony Colors Pro+Mobile 2
// (c) 2014-2022 Jean Moreno

Shader "Toony Colors Pro 2/User/My TCP2 Shader"
{
	Properties
	{
		[Enum(Front, 2, Back, 1, Both, 0)] _Cull ("Render Face", Float) = 2.0
		[TCP2ToggleNoKeyword] _ZWrite ("Depth Write", Float) = 1.0
		[HideInInspector] _RenderingMode ("rendering mode", Float) = 0.0
		[HideInInspector] _SrcBlend ("blending source", Float) = 1.0
		[HideInInspector] _DstBlend ("blending destination", Float) = 0.0
		[TCP2Separator]

		[TCP2HeaderHelp(Base)]
		_Color ("Color", Color) = (1,1,1,1)
		[TCP2ColorNoAlpha] _HColor ("Highlight Color", Color) = (0.75,0.75,0.75,1)
		[TCP2ColorNoAlpha] _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
		[TCP2HeaderHelp(Albedo HSV)]
		[HideInInspector] __BeginGroup_AlbedoHSV ("Albedo HSV", Float) = 0
		_HSV_H ("Hue", Range(-180,180)) = 0
		_HSV_S ("Saturation", Range(-1,1)) = 0
		_HSV_V ("Value", Range(-1,1)) = 0
		[HideInInspector] __EndGroup ("Albedo HSV", Float) = 0
		[TCP2Separator]

		[TCP2Header(Ramp Shading)]
		_RampThreshold ("Threshold", Range(0.01,1)) = 0.5
		_RampSmoothing ("Smoothing", Range(0.001,1)) = 0.5
		[IntRange] _BandsCount ("Bands Count", Range(1,20)) = 4
		_BandsSmoothing ("Bands Smoothing", Range(0.001,1)) = 0.1
		[TCP2Separator]
		
		[TCP2HeaderHelp(Rim Lighting)]
		[Toggle(TCP2_RIM_LIGHTING)] _UseRim ("Enable Rim Lighting", Float) = 0
		[TCP2ColorNoAlpha] _RimColor ("Rim Color", Color) = (0.8,0.8,0.8,0.5)
		_RimMin ("Rim Min", Range(0,2)) = 0.5
		_RimMax ("Rim Max", Range(0,2)) = 1
		[TCP2Separator]
		
		[TCP2HeaderHelp(Triplanar Mapping)]
		_TriGround ("Ground", 2D) = "white" {}
		_TriSide ("Walls", 2D) = "white" {}
		_TriCeiling ("Ceiling", 2D) = "white" {}
		_CeilingMin ("Ceiling Threshold Min", Float) = -1
		_CeilingMax ("Ceiling Threshold Max", Float) = 1
		[Space]
		[TCP2Vector4Floats(Contrast X,Contrast Y,Contrast Z,Smoothing,1,16,1,16,1,16,0.01,1)] _TriplanarBlendStrength ("Triplanar Parameters", Vector) = (2,8,2,0.5)
		[TCP2HeaderHelp(Triplanar Mapping Normal Maps)]
		[NoScaleOffset] _TriGroundBump ("Ground Normal Map", 2D) = "bump" {}
		_TriGroundBumpScale ("Scale", Range(-2,2)) = 1
		[NoScaleOffset] _TriSideBump ("Walls Normal Map", 2D) = "bump" {}
		_TriSideBumpScale ("Scale", Range(-2,2)) = 1
		[NoScaleOffset] _TriCeilingBump ("Ceiling Normal Map", 2D) = "bump" {}
		_TriCeilingBumpScale ("Scale", Range(-2,2)) = 1
		[TCP2Separator]
		
		[TCP2ColorNoAlpha] _DiffuseTint ("Diffuse Tint", Color) = (1,0.5,0,1)
		[NoScaleOffset] _DiffuseTintMask ("Diffuse Tint Mask", 2D) = "white" {}
		[TCP2Separator]
		
		[TCP2HeaderHelp(Vertical Fog)]
		[Toggle(TCP2_VERTICAL_FOG)] _UseVerticalFog ("Enable Vertical Fog", Float) = 0
		_VerticalFogThreshold ("Y Threshold", Float) = 0
		_VerticalFogSmoothness ("Smoothness", Float) = 0.5
		[TCP2Separator]
		
		[TCP2HeaderHelp(Outline)]
		_OutlineWidth ("Width", Range(0.1,4)) = 1
		_OutlineMinWidth ("Min Width", Range(0,20)) = 1
		_OutlineMaxWidth ("Max Width", Range(0,20)) = 1
		_OutlineColorVertex ("Color", Color) = (0,0,0,1)
		// Outline Normals
		[TCP2MaterialKeywordEnumNoPrefix(Regular, _, Vertex Colors, TCP2_COLORS_AS_NORMALS, Tangents, TCP2_TANGENT_AS_NORMALS, UV1, TCP2_UV1_AS_NORMALS, UV2, TCP2_UV2_AS_NORMALS, UV3, TCP2_UV3_AS_NORMALS, UV4, TCP2_UV4_AS_NORMALS)]
		_NormalsSource ("Outline Normals Source", Float) = 0
		[TCP2MaterialKeywordEnumNoPrefix(Full XYZ, TCP2_UV_NORMALS_FULL, Compressed XY, _, Compressed ZW, TCP2_UV_NORMALS_ZW)]
		_NormalsUVType ("UV Data Type", Float) = 0
		[TCP2Separator]
		
		[MainTexture] _MainTex ("Albedo", 2D) = "white" {}

		// Avoid compile error if the properties are ending with a drawer
		[HideInInspector] __dummy__ ("unused", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderType"="Opaque"
		}

		CGINCLUDE

		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"	// needed for LightColor

		// Texture/Sampler abstraction
		#define TCP2_TEX2D_WITH_SAMPLER(tex)						UNITY_DECLARE_TEX2D(tex)
		#define TCP2_TEX2D_NO_SAMPLER(tex)							UNITY_DECLARE_TEX2D_NOSAMPLER(tex)
		#define TCP2_TEX2D_SAMPLE(tex, samplertex, coord)			UNITY_SAMPLE_TEX2D_SAMPLER(tex, samplertex, coord)
		#define TCP2_TEX2D_SAMPLE_LOD(tex, samplertex, coord, lod)	UNITY_SAMPLE_TEX2D_SAMPLER_LOD(tex, samplertex, coord, lod)

		// Shader Properties
		TCP2_TEX2D_WITH_SAMPLER(_TriGround);
		TCP2_TEX2D_WITH_SAMPLER(_TriCeiling);
		TCP2_TEX2D_WITH_SAMPLER(_TriSide);
		TCP2_TEX2D_NO_SAMPLER(_TriGroundBump);
		TCP2_TEX2D_WITH_SAMPLER(_TriCeilingBump);
		TCP2_TEX2D_WITH_SAMPLER(_TriSideBump);
		TCP2_TEX2D_WITH_SAMPLER(_DiffuseTintMask);
		TCP2_TEX2D_WITH_SAMPLER(_MainTex);
		
		// Shader Properties
		float _OutlineMinWidth;
		float _OutlineWidth;
		float _OutlineMaxWidth;
		fixed4 _OutlineColorVertex;
		float _VerticalFogThreshold;
		float _VerticalFogSmoothness;
		float4 _TriGround_ST;
		float4 _TriCeiling_ST;
		float4 _TriSide_ST;
		float4 _TriplanarBlendStrength;
		float _CeilingMax;
		float _CeilingMin;
		float _HSV_H;
		float _HSV_S;
		float _HSV_V;
		fixed4 _Color;
		float _TriGroundBumpScale;
		float _TriCeilingBumpScale;
		float _TriSideBumpScale;
		float _RampThreshold;
		float _RampSmoothing;
		float _BandsCount;
		float _BandsSmoothing;
		fixed4 _HColor;
		fixed4 _SColor;
		fixed4 _DiffuseTint;
		float _RimMin;
		float _RimMax;
		fixed4 _RimColor;
		float4 _MainTex_ST;

		//--------------------------------
		// HSV HELPERS
		// source: http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
		
		float3 rgb2hsv(float3 c)
		{
			float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
			float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
			float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
		
			float d = q.x - min(q.w, q.y);
			float e = 1.0e-10;
			return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
		}
		
		float3 hsv2rgb(float3 c)
		{
			c.g = max(c.g, 0.0); //make sure that saturation value is positive
			float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
			float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
			return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
		}
		
		float3 ApplyHSV_3(float3 color, float h, float s, float v)
		{
			float3 hsv = rgb2hsv(color.rgb);
			hsv += float3(h/360,s,v);
			return hsv2rgb(hsv);
		}
		float3 ApplyHSV_3(float color, float h, float s, float v) { return ApplyHSV_3(color.xxx, h, s ,v); }
		
		float4 ApplyHSV_4(float4 color, float h, float s, float v)
		{
			float3 hsv = rgb2hsv(color.rgb);
			hsv += float3(h/360,s,v);
			return float4(hsv2rgb(hsv), color.a);
		}
		float4 ApplyHSV_4(float color, float h, float s, float v) { return ApplyHSV_4(color.xxxx, h, s, v); }

		ENDCG

		// Outline Include
		CGINCLUDE

		#pragma multi_compile_fog

		struct appdata_outline
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			#if TCP2_UV1_AS_NORMALS
			float4 texcoord0 : TEXCOORD0;
		#elif TCP2_UV2_AS_NORMALS
			float4 texcoord1 : TEXCOORD1;
		#elif TCP2_UV3_AS_NORMALS
			float4 texcoord2 : TEXCOORD2;
		#elif TCP2_UV4_AS_NORMALS
			float4 texcoord3 : TEXCOORD3;
		#endif
		#if TCP2_COLORS_AS_NORMALS
			float4 vertexColor : COLOR;
		#endif
		#if TCP2_TANGENT_AS_NORMALS
			float4 tangent : TANGENT;
		#endif
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct v2f_outline
		{
			float4 vertex : SV_POSITION;
			UNITY_FOG_COORDS(0)
			float4 vcolor : TEXCOORD1;
			float3 pack1 : TEXCOORD2; /* pack1.xyz = worldNormal */
			float3 pack2 : TEXCOORD3; /* pack2.xyz = worldPos */
			UNITY_VERTEX_OUTPUT_STEREO
		};

		v2f_outline vertex_outline (appdata_outline v)
		{
			v2f_outline output;
			UNITY_INITIALIZE_OUTPUT(v2f_outline, output);
			UNITY_SETUP_INSTANCE_ID(v);
			UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

			float3 worldNormalUv = mul(unity_ObjectToWorld, float4(v.normal, 1.0)).xyz;
			// Shader Properties Sampling
			float __outlineMinWidth = ( _OutlineMinWidth );
			float __outlineWidth = ( _OutlineWidth );
			float __outlineMaxWidth = ( _OutlineMaxWidth );
			float4 __outlineColorVertex = ( _OutlineColorVertex.rgba );

			float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
			output.pack2.xyz = worldPos;
			output.pack1.xyz = worldNormalUv;
		
		#ifdef TCP2_COLORS_AS_NORMALS
			//Vertex Color for Normals
			float3 normal = (v.vertexColor.xyz*2) - 1;
		#elif TCP2_TANGENT_AS_NORMALS
			//Tangent for Normals
			float3 normal = v.tangent.xyz;
		#elif TCP2_UV1_AS_NORMALS || TCP2_UV2_AS_NORMALS || TCP2_UV3_AS_NORMALS || TCP2_UV4_AS_NORMALS
			#if TCP2_UV1_AS_NORMALS
				#define uvChannel texcoord0
			#elif TCP2_UV2_AS_NORMALS
				#define uvChannel texcoord1
			#elif TCP2_UV3_AS_NORMALS
				#define uvChannel texcoord2
			#elif TCP2_UV4_AS_NORMALS
				#define uvChannel texcoord3
			#endif
		
			#if TCP2_UV_NORMALS_FULL
			//UV for Normals, full
			float3 normal = v.uvChannel.xyz;
			#else
			//UV for Normals, compressed
			#if TCP2_UV_NORMALS_ZW
				#define ch1 z
				#define ch2 w
			#else
				#define ch1 x
				#define ch2 y
			#endif
			float3 n;
			//unpack uvs
			v.uvChannel.ch1 = v.uvChannel.ch1 * 255.0/16.0;
			n.x = floor(v.uvChannel.ch1) / 15.0;
			n.y = frac(v.uvChannel.ch1) * 16.0 / 15.0;
			//- get z
			n.z = v.uvChannel.ch2;
			//- transform
			n = n*2 - 1;
			float3 normal = n;
			#endif
		#else
			float3 normal = v.normal;
		#endif
		
		#if TCP2_ZSMOOTH_ON
			//Correct Z artefacts
			normal = UnityObjectToViewPos(normal);
			normal.z = -_ZSmooth;
		#endif
			float size = 1;
		
		#if !defined(SHADOWCASTER_PASS)
			output.vertex = UnityObjectToClipPos(v.vertex.xyz);
			normal = mul(unity_ObjectToWorld, float4(normal, 0)).xyz;
			float2 clipNormals = normalize(mul(UNITY_MATRIX_VP, float4(normal,0)).xy);
			half2 screenRatio = half2(1.0, _ScreenParams.x / _ScreenParams.y);
			half2 outlineWidth = max(
				(__outlineMinWidth * output.vertex.w) / (_ScreenParams.xy / 2.0),
				(__outlineWidth / 100) * screenRatio
			);
			outlineWidth = min(
				(__outlineMaxWidth * output.vertex.w) / (_ScreenParams.xy / 2.0),
				outlineWidth
			);
			output.vertex.xy += clipNormals.xy * outlineWidth;
			
		#else
			v.vertex = v.vertex + float4(normal,0) * __outlineWidth * size * 0.01;
		#endif
		
			output.vcolor.xyzw = __outlineColorVertex;
			UNITY_TRANSFER_FOG(output, output.vertex);

			return output;
		}

		float4 fragment_outline (v2f_outline input) : SV_Target
		{

			// Shader Properties Sampling
			float4 __outlineColor = ( float4(1,1,1,1) );
			float __verticalFogThreshold = ( _VerticalFogThreshold );
			float __verticalFogSmoothness = ( _VerticalFogSmoothness );

			half4 outlineColor = __outlineColor * input.vcolor.xyzw;

			// Vertical Fog
			#if defined(TCP2_VERTICAL_FOG)
			half vertFogThreshold = input.pack2.xyz.y;
			half verticalFogThreshold = __verticalFogThreshold;
			half verticalFogSmooothness = __verticalFogSmoothness;
			half verticalFogMin = verticalFogThreshold - verticalFogSmooothness;
			half verticalFogMax = verticalFogThreshold + verticalFogSmooothness;
			half4 fogColor = unity_FogColor;
			#if defined(UNITY_PASS_FORWARDADD)
				fogColor.rgb = half3(0, 0, 0);
			#endif
			half vertFogFactor = 1 - saturate((vertFogThreshold - verticalFogMin) / (verticalFogMax - verticalFogMin));
			outlineColor.rgb = lerp(outlineColor.rgb, fogColor.rgb, vertFogFactor);
			#endif
			UNITY_APPLY_FOG(input.fogCoord, outlineColor);

			return outlineColor;
		}

		ENDCG
		// Outline Include End
		// Main Surface Shader
		Blend [_SrcBlend] [_DstBlend]
		Cull [_Cull]
		ZWrite [_ZWrite]

		CGPROGRAM

		#pragma surface surf ToonyColorsCustom vertex:vertex_surface exclude_path:deferred exclude_path:prepass keepalpha nolppv keepalpha
		#pragma target 3.0

		//================================================================
		// SHADER KEYWORDS

		#pragma shader_feature_local_fragment TCP2_RIM_LIGHTING
		#pragma shader_feature_local _ _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
		#pragma shader_feature_local_fragment TCP2_VERTICAL_FOG

		//================================================================
		// STRUCTS

		// Vertex input
		struct appdata_tcp2
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 texcoord0 : TEXCOORD0;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
		#if defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
			half4 tangent : TANGENT;
		#endif
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Input
		{
			half3 viewDir;
			float3 worldPos;
			half3 worldNormal; INTERNAL_DATA
			half3 worldNormalVertex;
			float2 texcoord0;
		};

		//================================================================

		// Custom SurfaceOutput
		struct SurfaceOutputCustom
		{
			half atten;
			half3 Albedo;
			half3 Normal;
			half4 NormalCustom;
			half3 worldNormal;
			half3 Emission;
			half Specular;
			half Gloss;
			half Alpha;
			half ndv;
			half ndvRaw;

			Input input;

			// Shader Properties
			float __rampThreshold;
			float __rampSmoothing;
			float __bandsCount;
			float __bandsSmoothing;
			float3 __highlightColor;
			float3 __shadowColor;
			float3 __diffuseTint;
			float3 __diffuseTintMask;
			float __ambientIntensity;
			float __rimMin;
			float __rimMax;
			float3 __rimColor;
			float __rimStrength;
			float __verticalFogThreshold;
			float __verticalFogSmoothness;
		};

		//================================================================
		// VERTEX FUNCTION

		void vertex_surface(inout appdata_tcp2 v, out Input output)
		{
			UNITY_INITIALIZE_OUTPUT(Input, output);

			// Texture Coordinates
			output.texcoord0 = v.texcoord0.xy;

			half3 worldNormal = UnityObjectToWorldNormal(v.normal);

			output.worldNormalVertex = worldNormal;

		}

		//================================================================
		// SURFACE FUNCTION

		void surf(Input input, inout SurfaceOutputCustom output)
		{
			// Shader Properties Sampling
			float4 __triplanarParameters = ( _TriplanarBlendStrength.xyzw );
			float __ceilingSmoothness = ( .1 );
			float __ceilingMax = ( _CeilingMax );
			float __ceilingMin = ( _CeilingMin );
			float __albedoHue = ( _HSV_H );
			float __albedoSaturation = ( _HSV_S );
			float __albedoValue = ( _HSV_V );
			float4 __mainColor = ( _Color.rgba );
			float __bumpScaleGround = ( _TriGroundBumpScale );
			float __bumpScaleCeiling = ( _TriCeilingBumpScale );
			float __bumpScaleWalls = ( _TriSideBumpScale );
			output.__rampThreshold = ( _RampThreshold );
			output.__rampSmoothing = ( _RampSmoothing );
			output.__bandsCount = ( _BandsCount );
			output.__bandsSmoothing = ( _BandsSmoothing );
			output.__highlightColor = ( _HColor.rgb );
			output.__shadowColor = ( _SColor.rgb );
			output.__diffuseTint = ( _DiffuseTint.rgb );
			output.__diffuseTintMask = ( TCP2_TEX2D_SAMPLE(_DiffuseTintMask, _DiffuseTintMask, input.texcoord0.xy).rgb );
			output.__ambientIntensity = ( 1.0 );
			output.__rimMin = ( _RimMin );
			output.__rimMax = ( _RimMax );
			output.__rimColor = ( _RimColor.rgb );
			output.__rimStrength = ( 1.0 );
			output.__verticalFogThreshold = ( _VerticalFogThreshold );
			output.__verticalFogSmoothness = ( _VerticalFogSmoothness );

			output.input = input;

			half3 worldNormal = WorldNormalVector(input, output.Normal);
			output.worldNormal = worldNormal;

			half ndv = abs(dot(input.viewDir, normalize(output.Normal.xyz)));
			half ndvRaw = ndv;
			output.ndv = ndv;
			output.ndvRaw = ndvRaw;

			output.Albedo = half3(1,1,1);
			output.Alpha = 1;

			half4 albedoAlpha = half4(output.Albedo, output.Alpha);
			
			// Triplanar Texture Blending
			half2 uv_ground = input.worldPos.xz;
			half2 uv_sideX = input.worldPos.zy;
			half2 uv_sideZ = input.worldPos.xy;
			float3 triplanarNormal = input.worldNormalVertex;
			
			//ground
			half4 triplanar = ( TCP2_TEX2D_SAMPLE(_TriGround, _TriGround, uv_ground * _TriGround_ST.xy + _TriGround_ST.zw).rgba );
			
			//ceiling
			fixed4 tex_ceiling = ( TCP2_TEX2D_SAMPLE(_TriCeiling, _TriCeiling, uv_ground * _TriCeiling_ST.xy + _TriCeiling_ST.zw).rgba );
			
			//walls
			fixed4 tex_sideX = ( TCP2_TEX2D_SAMPLE(_TriSide, _TriSide, uv_sideX * _TriSide_ST.xy + _TriSide_ST.zw).rgba );
			fixed4 tex_sideZ = ( TCP2_TEX2D_SAMPLE(_TriSide, _TriSide, uv_sideZ * _TriSide_ST.xy + _TriSide_ST.zw).rgba );
			
			//blending
			half3 blendWeights = pow(abs(triplanarNormal), __triplanarParameters.xyz / __triplanarParameters.w);
			blendWeights = blendWeights / (blendWeights.x + abs(blendWeights.y) + blendWeights.z);
			
			float triplanar_ceiling_lerp = smoothstep(input.worldPos.y - __ceilingSmoothness, input.worldPos.y, __ceilingMax) - smoothstep(input.worldPos.y, input.worldPos.y + __ceilingSmoothness, __ceilingMin);
			triplanar = lerp(tex_ceiling, triplanar, triplanar_ceiling_lerp);
			blendWeights.y = abs(blendWeights.y);
			triplanar = tex_sideX * blendWeights.x + triplanar * blendWeights.y + tex_sideZ * blendWeights.z;
			albedoAlpha *= triplanar;
			output.Albedo = albedoAlpha.rgb;
			
			//Albedo HSV
			output.Albedo = ApplyHSV_3(output.Albedo, __albedoHue, __albedoSaturation, __albedoValue);
			
			output.Albedo *= __mainColor.rgb;
			
			//Triplanar Normal Map Blending
			//ground
			half3 normalMap = UnpackScaleNormal( ( TCP2_TEX2D_SAMPLE(_TriGroundBump, _TriGround, uv_ground * _TriGround_ST.xy + _TriGround_ST.zw).rgba ), __bumpScaleGround );
			
			//ceiling
			half3 tex_ceiling_bump = UnpackScaleNormal( ( TCP2_TEX2D_SAMPLE(_TriCeilingBump, _TriCeilingBump, uv_ground * _TriCeiling_ST.xy + _TriCeiling_ST.zw).rgba ), __bumpScaleCeiling );
			
			//walls
			half3 tex_sideX_bump = UnpackScaleNormal( ( TCP2_TEX2D_SAMPLE(_TriSideBump, _TriSideBump, uv_sideX * _TriSide_ST.xy + _TriSide_ST.zw).rgba ), __bumpScaleWalls );
			half3 tex_sideZ_bump = UnpackScaleNormal( ( TCP2_TEX2D_SAMPLE(_TriSideBump, _TriSideBump, uv_sideZ * _TriSide_ST.xy + _TriSide_ST.zw).rgba ), __bumpScaleWalls );
			
			normalMap.xyz = lerp(tex_ceiling_bump, normalMap.xyz, triplanar_ceiling_lerp);
			
			//Whiteout blending
			tex_sideX_bump = half3(tex_sideX_bump.xy + triplanarNormal.zy,abs(tex_sideX_bump.z) * triplanarNormal.x);
			normalMap.xyz = half3(normalMap.xy + triplanarNormal.xz,abs(normalMap.z) * triplanarNormal.y);
			tex_sideZ_bump = half3(tex_sideZ_bump.xy + triplanarNormal.xy,abs(tex_sideZ_bump.z) * triplanarNormal.z);
			
			output.NormalCustom.w = blendWeights.y * triplanar_ceiling_lerp;
			output.NormalCustom.xyz = normalize(tex_sideX_bump.zyx * blendWeights.x + normalMap.xzy * blendWeights.y + tex_sideZ_bump.xyz * blendWeights.z);

			// Recalculate NDV to take the triplanar normals into account:
			ndv = abs(dot(input.viewDir, normalize(output.NormalCustom.xyz)));
			ndvRaw = ndv;
			output.ndv = ndv;
			output.ndvRaw = ndvRaw;

		}

		//================================================================
		// LIGHTING FUNCTION

		inline half4 LightingToonyColorsCustom(inout SurfaceOutputCustom surface, half3 viewDir, UnityGI gi)
		{

			half ndv = surface.ndv;
			half3 lightDir = gi.light.dir;
			#if defined(UNITY_PASS_FORWARDBASE)
				half3 lightColor = _LightColor0.rgb;
				half atten = surface.atten;
			#else
				// extract attenuation from point/spot lights
				half3 lightColor = _LightColor0.rgb;
				half atten = max(gi.light.color.r, max(gi.light.color.g, gi.light.color.b)) / max(_LightColor0.r, max(_LightColor0.g, _LightColor0.b));
			#endif

			half3 normal = surface.NormalCustom.xyz;
			half ndl = dot(normal, lightDir);
			half3 ramp;
			
			#define		RAMP_THRESHOLD		surface.__rampThreshold
			#define		RAMP_SMOOTH			surface.__rampSmoothing
			#define		RAMP_BANDS			surface.__bandsCount
			#define		RAMP_BANDS_SMOOTH	surface.__bandsSmoothing
			ndl = saturate(ndl);
			half bandsNdl = smoothstep(RAMP_THRESHOLD - RAMP_SMOOTH*0.5, RAMP_THRESHOLD + RAMP_SMOOTH*0.5, ndl);
			half bandsSmooth = RAMP_BANDS_SMOOTH * 0.5;
			ramp = saturate((smoothstep(0.5 - bandsSmooth, 0.5 + bandsSmooth, frac(bandsNdl * RAMP_BANDS)) + floor(bandsNdl * RAMP_BANDS)) / RAMP_BANDS).xxx;

			// Apply attenuation (shadowmaps & point/spot lights attenuation)
			ramp *= atten;

			// Highlight/Shadow Colors
			#if !defined(UNITY_PASS_FORWARDBASE)
				ramp = lerp(half3(0,0,0), surface.__highlightColor, ramp);
			#else
				ramp = lerp(surface.__shadowColor, surface.__highlightColor, ramp);
			#endif

			// Diffuse Tint
			half3 diffuseTint = saturate(surface.__diffuseTint + ndl);
			ramp = lerp(ramp, ramp * diffuseTint, surface.__diffuseTintMask);
			
			// Output color
			half4 color;
			color.rgb = surface.Albedo * lightColor.rgb * ramp;
			color.a = surface.Alpha;

			// Apply indirect lighting (ambient)
			half occlusion = 1;
			#ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
				half3 ambient = gi.indirect.diffuse;
				ambient *= surface.Albedo * occlusion * surface.__ambientIntensity;

				color.rgb += ambient;
			#endif

			// Premultiply blending
			#if defined(_ALPHAPREMULTIPLY_ON)
				color.rgb *= color.a;
			#endif

			// Rim Lighting
			#if defined(TCP2_RIM_LIGHTING)
			#if !defined(UNITY_PASS_FORWARDADD)
			half rim = 1 - surface.ndvRaw;
			rim = ( rim );
			half rimMin = surface.__rimMin;
			half rimMax = surface.__rimMax;
			rim = smoothstep(rimMin, rimMax, rim);
			half3 rimColor = surface.__rimColor;
			half rimStrength = surface.__rimStrength;
			color.rgb += rim * rimColor * rimStrength;
			#endif
			#endif

			// Vertical Fog
			#if defined(TCP2_VERTICAL_FOG)
			half vertFogThreshold = surface.input.worldPos.y;
			half verticalFogThreshold = surface.__verticalFogThreshold;
			half verticalFogSmooothness = surface.__verticalFogSmoothness;
			half verticalFogMin = verticalFogThreshold - verticalFogSmooothness;
			half verticalFogMax = verticalFogThreshold + verticalFogSmooothness;
			half4 fogColor = unity_FogColor;
			#if defined(UNITY_PASS_FORWARDADD)
				fogColor.rgb = half3(0, 0, 0);
			#endif
			half vertFogFactor = 1 - saturate((vertFogThreshold - verticalFogMin) / (verticalFogMax - verticalFogMin));
			color.rgb = lerp(color.rgb, fogColor.rgb, vertFogFactor);
			#endif

			// Apply alpha to Forward Add passes
			#if defined(_ALPHABLEND_ON) && defined(UNITY_PASS_FORWARDADD)
				color.rgb *= color.a;
			#endif

			return color;
		}

		// Same as UnityGI_Base but with attenuation extraction that works with lightmaps
		inline UnityGI UnityGI_Base_TCP2(UnityGIInput data, half occlusion, half3 normalWorld, out half tcp2_atten)
		{
			UnityGI o_gi;
			ResetUnityGI(o_gi);

			// Base pass with Lightmap support is responsible for handling ShadowMask / blending here for performance reason
			#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
				half bakedAtten = UnitySampleBakedOcclusion(data.lightmapUV.xy, data.worldPos);
				float zDist = dot(_WorldSpaceCameraPos - data.worldPos, UNITY_MATRIX_V[2].xyz);
				float fadeDist = UnityComputeShadowFadeDistance(data.worldPos, zDist);
				data.atten = UnityMixRealtimeAndBakedShadows(data.atten, bakedAtten, UnityComputeShadowFade(fadeDist));
			#endif

			o_gi.light = data.light;

			// TCP2: don't apply attenuation to light color
			// o_gi.light.color *= data.atten;

			// TCP2: extract attenuation
			tcp2_atten = data.atten;

			#if UNITY_SHOULD_SAMPLE_SH
				o_gi.indirect.diffuse = ShadeSHPerPixel(normalWorld, data.ambient, data.worldPos);
			#endif

			#if defined(LIGHTMAP_ON)
				// Baked lightmaps
				half4 bakedColorTex = UNITY_SAMPLE_TEX2D(unity_Lightmap, data.lightmapUV.xy);
				half3 bakedColor = DecodeLightmap(bakedColorTex);

				#ifdef DIRLIGHTMAP_COMBINED
					fixed4 bakedDirTex = UNITY_SAMPLE_TEX2D_SAMPLER (unity_LightmapInd, unity_Lightmap, data.lightmapUV.xy);
					o_gi.indirect.diffuse += DecodeDirectionalLightmap (bakedColor, bakedDirTex, normalWorld);

					#if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
						ResetUnityLight(o_gi.light);
						o_gi.indirect.diffuse = SubtractMainLightWithRealtimeAttenuationFromLightmap (o_gi.indirect.diffuse, data.atten, bakedColorTex, normalWorld);
					#endif

				#else // not directional lightmap
					o_gi.indirect.diffuse += bakedColor;

					#if defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK) && defined(SHADOWS_SCREEN)
						ResetUnityLight(o_gi.light);
						o_gi.indirect.diffuse = SubtractMainLightWithRealtimeAttenuationFromLightmap(o_gi.indirect.diffuse, data.atten, bakedColorTex, normalWorld);
					#endif

				#endif
			#endif

			#ifdef DYNAMICLIGHTMAP_ON
				// Dynamic lightmaps
				fixed4 realtimeColorTex = UNITY_SAMPLE_TEX2D(unity_DynamicLightmap, data.lightmapUV.zw);
				half3 realtimeColor = DecodeRealtimeLightmap (realtimeColorTex);

				#ifdef DIRLIGHTMAP_COMBINED
					half4 realtimeDirTex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_DynamicDirectionality, unity_DynamicLightmap, data.lightmapUV.zw);
					o_gi.indirect.diffuse += DecodeDirectionalLightmap (realtimeColor, realtimeDirTex, normalWorld);
				#else
					o_gi.indirect.diffuse += realtimeColor;
				#endif
			#endif

			o_gi.indirect.diffuse *= occlusion;
			return o_gi;
		}

		inline UnityGI UnityGlobalIllumination_TCP2 (UnityGIInput data, half occlusion, half3 normalWorld, out half tcp2_atten)
		{
			return UnityGI_Base_TCP2(data, occlusion, normalWorld, tcp2_atten);
		}

		inline UnityGI UnityGlobalIllumination_TCP2 (UnityGIInput data, half occlusion, half3 normalWorld, Unity_GlossyEnvironmentData glossIn, out half tcp2_atten)
		{
			UnityGI o_gi = UnityGI_Base_TCP2(data, occlusion, normalWorld, tcp2_atten);
			o_gi.indirect.specular = UnityGI_IndirectSpecular(data, occlusion, glossIn);
			return o_gi;
		}

		void LightingToonyColorsCustom_GI(inout SurfaceOutputCustom surface, UnityGIInput data, inout UnityGI gi)
		{
			half3 normal = surface.NormalCustom.xyz;

			// GI without reflection probes
			half tcp2_atten;
			gi = UnityGlobalIllumination_TCP2(data, 1.0, normal, tcp2_atten); // occlusion is applied in the lighting function, if necessary

			surface.atten = tcp2_atten; // transfer attenuation to lighting function

		}

		ENDCG

		// Outline
		Pass
		{
			Name "Outline"
			Tags
			{
				"LightMode"="ForwardBase"
			}
			Cull Front

			CGPROGRAM
			#pragma vertex vertex_outline
			#pragma fragment fragment_outline
			#pragma target 3.0
			#pragma multi_compile _ TCP2_COLORS_AS_NORMALS TCP2_TANGENT_AS_NORMALS TCP2_UV1_AS_NORMALS TCP2_UV2_AS_NORMALS TCP2_UV3_AS_NORMALS TCP2_UV4_AS_NORMALS
			#pragma multi_compile _ TCP2_UV_NORMALS_FULL TCP2_UV_NORMALS_ZW
			#pragma multi_compile_instancing
			ENDCG
		}
		//================================================================
		// SHADOW CASTER PASS

		// Shadow Caster (for shadows and depth texture)
		Pass
		{
			Name "ShadowCaster"
			Tags
			{
				"LightMode" = "ShadowCaster"
			}
			ZWrite On
			Blend Off

			CGPROGRAM

			#define SHADOWCASTER_PASS

			#pragma vertex vertex_shadowcaster
			#pragma fragment fragment_shadowcaster
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing

			// half _Cutoff;
			sampler3D	_DitherMaskLOD;

			struct appdata_shadowcaster
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
				float4 texcoord0 : TEXCOORD0;
			#if TCP2_COLORS_AS_NORMALS
				float4 vertexColor : COLOR;
			#endif
			// TODO: need a way to know if texcoord1 is used in the Shader Properties
			#if TCP2_UV2_AS_NORMALS
				float2 uv2 : TEXCOORD1;
			#endif
			#if TCP2_TANGENT_AS_NORMALS
				float4 tangent : TANGENT;
			#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f_shadowcaster
			{
				V2F_SHADOW_CASTER_NOPOS
				float4 vcolor : TEXCOORD1;
				float3 pack1 : TEXCOORD2; /* pack1.xyz = worldPos */
				float3 pack2 : TEXCOORD3; /* pack2.xyz = worldNormal */
				float2 pack3 : TEXCOORD4; /* pack3.xy = texcoord0 */
				UNITY_VERTEX_OUTPUT_STEREO
			};

			void vertex_shadowcaster (appdata_shadowcaster v, out v2f_shadowcaster output, out float4 opos : SV_POSITION)
			{
				UNITY_INITIALIZE_OUTPUT(v2f_shadowcaster, output);
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 worldNormalUv = mul(unity_ObjectToWorld, float4(v.normal, 1.0)).xyz;
				// Texture Coordinates
				output.pack3.xy.xy = v.texcoord0.xy * _MainTex_ST.xy + _MainTex_ST.zw;
				// Shader Properties Sampling
				float __outlineMinWidth = ( _OutlineMinWidth );
				float __outlineWidth = ( _OutlineWidth );
				float __outlineMaxWidth = ( _OutlineMaxWidth );
				float4 __outlineColorVertex = ( _OutlineColorVertex.rgba );

				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				output.pack1.xyz = worldPos;
				output.pack2.xyz = worldNormalUv;
			
			#ifdef TCP2_COLORS_AS_NORMALS
				//Vertex Color for Normals
				float3 normal = (v.vertexColor.xyz*2) - 1;
			#elif TCP2_TANGENT_AS_NORMALS
				//Tangent for Normals
				float3 normal = v.tangent.xyz;
			#elif TCP2_UV1_AS_NORMALS || TCP2_UV2_AS_NORMALS || TCP2_UV3_AS_NORMALS || TCP2_UV4_AS_NORMALS
				#if TCP2_UV1_AS_NORMALS
					#define uvChannel texcoord0
				#elif TCP2_UV2_AS_NORMALS
					#define uvChannel texcoord1
				#elif TCP2_UV3_AS_NORMALS
					#define uvChannel texcoord2
				#elif TCP2_UV4_AS_NORMALS
					#define uvChannel texcoord3
				#endif
			
				#if TCP2_UV_NORMALS_FULL
				//UV for Normals, full
				float3 normal = v.uvChannel.xyz;
				#else
				//UV for Normals, compressed
				#if TCP2_UV_NORMALS_ZW
					#define ch1 z
					#define ch2 w
				#else
					#define ch1 x
					#define ch2 y
				#endif
				float3 n;
				//unpack uvs
				v.uvChannel.ch1 = v.uvChannel.ch1 * 255.0/16.0;
				n.x = floor(v.uvChannel.ch1) / 15.0;
				n.y = frac(v.uvChannel.ch1) * 16.0 / 15.0;
				//- get z
				n.z = v.uvChannel.ch2;
				//- transform
				n = n*2 - 1;
				float3 normal = n;
				#endif
			#else
				float3 normal = v.normal;
			#endif
			
			#if TCP2_ZSMOOTH_ON
				//Correct Z artefacts
				normal = UnityObjectToViewPos(normal);
				normal.z = -_ZSmooth;
			#endif
				float size = 1;
			
			#if !defined(SHADOWCASTER_PASS)
				output.vertex = UnityObjectToClipPos(v.vertex.xyz);
				normal = mul(unity_ObjectToWorld, float4(normal, 0)).xyz;
				float2 clipNormals = normalize(mul(UNITY_MATRIX_VP, float4(normal,0)).xy);
				half2 screenRatio = half2(1.0, _ScreenParams.x / _ScreenParams.y);
				half2 outlineWidth = max(
					(__outlineMinWidth * output.vertex.w) / (_ScreenParams.xy / 2.0),
					(__outlineWidth / 100) * screenRatio
				);
				outlineWidth = min(
					(__outlineMaxWidth * output.vertex.w) / (_ScreenParams.xy / 2.0),
					outlineWidth
				);
				output.vertex.xy += clipNormals.xy * outlineWidth;
				
			#else
				v.vertex = v.vertex + float4(normal,0) * __outlineWidth * size * 0.01;
			#endif
			
				output.vcolor.xyzw = __outlineColorVertex;

				TRANSFER_SHADOW_CASTER_NOPOS(output,opos)
			}

			half4 fragment_shadowcaster(v2f_shadowcaster input, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
			{

				// Shader Properties Sampling
				float4 __albedo = ( TCP2_TEX2D_SAMPLE(_MainTex, _MainTex, input.pack3.xy).rgba );
				float4 __mainColor = ( _Color.rgba );
				float __alpha = ( __albedo.a * __mainColor.a );

				// Use dither mask for alpha blended shadows, based on pixel position xy
				// and alpha level. Our dither texture is 4x4x16.
				half alpha = ;
				half alphaRef = tex3D(_DitherMaskLOD, float3(vpos.xy*0.25,alpha*0.9375)).a;
				clip (alphaRef - 0.01);

				SHADOW_CASTER_FRAGMENT(input)
			}

			ENDCG
		}
	}

	Fallback "Diffuse"
	CustomEditor "ToonyColorsPro.ShaderGenerator.MaterialInspector_SG2"
}

/* TCP_DATA u config(ver:"2.9.2";unity:"2022.3.62f3";tmplt:"SG2_Template_Default";features:list["UNITY_5_4","UNITY_5_5","UNITY_5_6","UNITY_2017_1","UNITY_2018_1","UNITY_2018_2","UNITY_2018_3","UNITY_2019_1","UNITY_2019_2","UNITY_2019_3","UNITY_2019_4","UNITY_2020_1","UNITY_2021_1","DITHERED_SHADOWS","TRIPLANAR","TRIPLANAR_BUMP","TT_SHADER_FEATURE","DIFFUSE_TINT","DIFFUSE_TINT_MASK","OUTLINE","AUTO_TRANSPARENT_BLENDING","TRIPLANAR_CEILING","TRIPLANAR_BUMP_SCALE","OUTLINE_MIN_WIDTH","OUTLINE_MAX_WIDTH","SKETCH_AMBIENT","SKETCH_SHADER_FEATURE","SKETCH_PROGRESSIVE_SMOOTH","TRIPLANAR_CEILING_MINMAX","ALBEDO_HSV","RAMP_BANDS","RIM_SHADER_FEATURE","RIM","MATCAP_SHADER_FEATURE","OUTLINE_CLIP_SPACE","VERTICAL_FOG","VERTICAL_FOG_SHADER_FEATURE","VERTICAL_FOG_COLOR","ENABLE_FOG","ENABLE_LIGHTMAPS"];flags:list[];flags_extra:dict[];keywords:dict[RENDER_TYPE="Opaque",RampTextureDrawer="[TCP2Gradient]",RampTextureLabel="Ramp Texture",SHADER_TARGET="3.0",RIM_LABEL="Rim Lighting"];shaderProperties:list[,,,,,,,,,,,,,,,,sp(name:"Ground Texture";imps:list[imp_mp_texture(uto:True;tov:"";tov_lbl:"";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"white";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_TriGround";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"e57446d3-b1ef-40e6-b3f6-38c9b42b2ef0";op:Multiply;lbl:"Ground";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),sp(name:"Walls Texture";imps:list[imp_mp_texture(uto:True;tov:"";tov_lbl:"";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"white";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_TriSide";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"72b6f4eb-1979-43b0-9a30-e7abed958748";op:Multiply;lbl:"Walls";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),sp(name:"Ceiling Texture";imps:list[imp_mp_texture(uto:True;tov:"";tov_lbl:"";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"white";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_TriCeiling";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"0d293c1e-c425-4e35-a4d6-28e00db46427";op:Multiply;lbl:"Ceiling";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),,,sp(name:"Ground Texture Normal Map";imps:list[imp_mp_texture(uto:True;tov:"_TriGround_ST";tov_lbl:"_TriGround_ST";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"bump";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:"_TriGround";prop:"_TriGroundBump";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"df40221d-83dc-4360-bdfb-c3142433381b";op:Multiply;lbl:"Ground Normal Map";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),sp(name:"Walls Texture Normal Map";imps:list[imp_mp_texture(uto:True;tov:"_TriSide_ST";tov_lbl:"_TriSide_ST";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"bump";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_TriSideBump";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"c136113f-35c0-41c7-94a8-42f564b5d3e7";op:Multiply;lbl:"Walls Normal Map";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),sp(name:"Ceiling Texture Normal Map";imps:list[imp_mp_texture(uto:True;tov:"_TriCeiling_ST";tov_lbl:"_TriCeiling_ST";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"bump";locked_uv:True;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_TriCeilingBump";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"0fc6564a-0974-4a0f-8c69-ac1d93da1fef";op:Multiply;lbl:"Ceiling Normal Map";gpu_inst:False;locked:True;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False),,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,,sp(name:"Normal Map";imps:list[imp_mp_texture(uto:True;tov:"";tov_lbl:"";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"bump";locked_uv:False;uv:0;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Texcoord;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_BumpMap";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"85e48721-41ba-486d-bf84-95733a7c811d";op:Multiply;lbl:"Normal Map";gpu_inst:False;locked:False;impl_index:0)];layers:list[];unlocked:list[];clones:dict[];isClone:False)];customTextures:list[];codeInjection:codeInjection(injectedFiles:list[];mark:False);matLayers:list[]) */
/* TCP_HASH ebe9a68b65624ae7a8efe38854621a51 */
