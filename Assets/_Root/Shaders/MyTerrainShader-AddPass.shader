// Toony Colors Pro+Mobile 2
// (c) 2014-2022 Jean Moreno

// Terrain AddPass shader:
// This shader is used if your terrain uses more than 4 texture layers.
// It will draw the additional texture layers additively, by groups of 4 layers.

Shader "Hidden/MyTerrainShader-AddPass"
{
	Properties
	{
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
		[TCP2Separator]
		[TCP2HeaderHelp(Terrain)]
		_HeightTransition ("Height Smoothing", Range(0, 1.0)) = 0.0
		_Layer0HeightOffset ("Layer 0 Height Offset", Range(-1,1)) = 0
		_Layer1HeightOffset ("Layer 1 Height Offset", Range(-1,1)) = 0
		_Layer2HeightOffset ("Layer 2 Height Offset", Range(-1,1)) = 0
		_Layer3HeightOffset ("Layer 3 Height Offset", Range(-1,1)) = 0
		[HideInInspector] TerrainMeta_maskMapTexture ("Mask Map", 2D) = "white" {}
		[HideInInspector] TerrainMeta_normalMapTexture ("Normal Map", 2D) = "bump" {}
		[HideInInspector] TerrainMeta_normalScale ("Normal Scale", Float) = 1
		[Toggle(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)] _EnableInstancedPerPixelNormal("Enable Instanced per-pixel normal", Float) = 1.0
		[TCP2Separator]
		
		[Toggle(TCP2_TEXTURED_THRESHOLD)] _UseTexturedThreshold ("Enable Textured Threshold", Float) = 0
		_StylizedThreshold ("Stylized Threshold", 2D) = "gray" {}
		[TCP2Separator]
		
		[TCP2ColorNoAlpha] _DiffuseTint ("Diffuse Tint", Color) = (1,0.5,0,1)
		[TCP2Separator]
		
		[HideInInspector] [NoScaleOffset] _Normal0 ("Layer 0 Normal Map AddPass", 2D) = "bump" {}
		[HideInInspector] [NoScaleOffset] _Normal1 ("Layer 1 Normal Map AddPass", 2D) = "bump" {}
		[HideInInspector] [NoScaleOffset] _Normal2 ("Layer 2 Normal Map AddPass", 2D) = "bump" {}
		[HideInInspector] [NoScaleOffset] _Normal3 ("Layer 3 Normal Map AddPass", 2D) = "bump" {}
		[HideInInspector] _Splat0 ("Layer 0 Albedo AddPass", 2D) = "gray" {}
		[HideInInspector] _Splat1 ("Layer 1 Albedo AddPass", 2D) = "gray" {}
		[HideInInspector] _Splat2 ("Layer 2 Albedo AddPass", 2D) = "gray" {}
		[HideInInspector] _Splat3 ("Layer 3 Albedo AddPass", 2D) = "gray" {}
		[HideInInspector] [NoScaleOffset] _Mask0 ("Layer 0 Mask", 2D) = "gray" {}
		[HideInInspector] [NoScaleOffset] _Mask1 ("Layer 1 Mask", 2D) = "gray" {}
		[HideInInspector] [NoScaleOffset] _Mask2 ("Layer 2 Mask", 2D) = "gray" {}
		[HideInInspector] [NoScaleOffset] _Mask3 ("Layer 3 Mask", 2D) = "gray" {}
		[MainTexture] _MainTex ("Albedo", 2D) = "white" {}

		// Avoid compile error if the properties are ending with a drawer
		[HideInInspector] __dummy__ ("unused", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderType" = "Opaque"
			"Queue"="Geometry-99"
			"IgnoreProjector"="True"
			"TerrainCompatible"="True"
		}

		CGINCLUDE

		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"	// needed for LightColor

		// Texture/Sampler abstraction
		#define TCP2_TEX2D_WITH_SAMPLER(tex)						UNITY_DECLARE_TEX2D(tex)
		#define TCP2_TEX2D_NO_SAMPLER(tex)							UNITY_DECLARE_TEX2D_NOSAMPLER(tex)
		#define TCP2_TEX2D_SAMPLE(tex, samplertex, coord)			UNITY_SAMPLE_TEX2D_SAMPLER(tex, samplertex, coord)
		#define TCP2_TEX2D_SAMPLE_LOD(tex, samplertex, coord, lod)	UNITY_SAMPLE_TEX2D_SAMPLER_LOD(tex, samplertex, coord, lod)

		// Terrain
		#define TERRAIN_INSTANCED_PERPIXEL_NORMAL
		#define TERRAIN_SPLAT_ADDPASS

		//================================================================
		// Terrain Shader specific
		
		//----------------------------------------------------------------
		// Per-layer variables
		
		CBUFFER_START(_Terrain)
			float4 _Control_ST;
			float4 _Control_TexelSize;
			half _HeightTransition;
			half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
			half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
			// half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
			half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
		
			#ifdef UNITY_INSTANCING_ENABLED
				float4 _TerrainHeightmapRecipSize;   // float4(1.0f/width, 1.0f/height, 1.0f/(width-1), 1.0f/(height-1))
				float4 _TerrainHeightmapScale;       // float4(hmScale.x, hmScale.y / (float)(kMaxHeight), hmScale.z, 0.0f)
			#endif
			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif
		CBUFFER_END
		
		//----------------------------------------------------------------
		// Terrain textures
		
		TCP2_TEX2D_WITH_SAMPLER(_Control);
		
		#if defined(TERRAIN_BASE_PASS)
			TCP2_TEX2D_WITH_SAMPLER(_MainTex);
			TCP2_TEX2D_WITH_SAMPLER(_NormalMap);
		#endif
		
		//----------------------------------------------------------------
		// Terrain Instancing
		
		#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
			#define ENABLE_TERRAIN_PERPIXEL_NORMAL
		#endif
		
		#ifdef UNITY_INSTANCING_ENABLED
			TCP2_TEX2D_NO_SAMPLER(_TerrainHeightmapTexture);
			TCP2_TEX2D_WITH_SAMPLER(_TerrainNormalmapTexture);
		#endif
		
		UNITY_INSTANCING_BUFFER_START(Terrain)
			UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)  // float4(xBase, yBase, skipScale, ~)
		UNITY_INSTANCING_BUFFER_END(Terrain)
		
		void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
		{
		#ifdef UNITY_INSTANCING_ENABLED
			float2 patchVertex = positionOS.xy;
			float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);
		
			float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z; // (xy + float2(xBase,yBase)) * skipScale
			float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));
		
			positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
			positionOS.y = height * _TerrainHeightmapScale.y;
		
			#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
				normal = float3(0, 1, 0);
			#else
				normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
			#endif
			uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
		#endif
		}
		
		void TerrainInstancing(inout float4 positionOS, inout float3 normal)
		{
			float2 uv = { 0, 0 };
			TerrainInstancing(positionOS, normal, uv);
		}
		
		//----------------------------------------------------------------
		// Terrain Holes
		
		#if defined(_ALPHATEST_ON)
			TCP2_TEX2D_WITH_SAMPLER(_TerrainHolesTexture);
		
			void ClipHoles(float2 uv)
			{
				float hole = TCP2_TEX2D_SAMPLE(_TerrainHolesTexture, _TerrainHolesTexture, uv).r;
				clip(hole == 0.0f ? -1 : 1);
			}
		#endif
		
		//----------------------------------------------------------------
		// Height-based blending
		
		void HeightBasedSplatModify(inout half4 splatControl, in half4 splatHeight)
		{
			// We multiply by the splat Control weights to get combined height
			splatHeight *= splatControl.rgba;
			half maxHeight = max(splatHeight.r, max(splatHeight.g, max(splatHeight.b, splatHeight.a)));
		
			// Ensure that the transition height is not zero.
			half transition = max(_HeightTransition, 1e-5);
		
			// This sets the highest splat to "transition", and everything else to a lower value relative to that
			// Then we clamp this to zero and normalize everything
			half4 weightedHeights = splatHeight + transition - maxHeight.xxxx;
			weightedHeights = max(0, weightedHeights);
		
			// We need to add an epsilon here for active layers (hence the blendMask again)
			// so that at least a layer shows up if everything's too low.
			weightedHeights = (weightedHeights + 1e-6) * splatControl;
		
			// Normalize (and clamp to epsilon to keep from dividing by zero)
			half sumHeight = max(dot(weightedHeights, half4(1, 1, 1, 1)), 1e-6);
			splatControl = weightedHeights / sumHeight.xxxx;
		}
		
		// Shader Properties
		TCP2_TEX2D_WITH_SAMPLER(_Normal0);
		TCP2_TEX2D_NO_SAMPLER(_Normal1);
		TCP2_TEX2D_NO_SAMPLER(_Normal2);
		TCP2_TEX2D_NO_SAMPLER(_Normal3);
		TCP2_TEX2D_WITH_SAMPLER(_Splat0);
		TCP2_TEX2D_NO_SAMPLER(_Splat1);
		TCP2_TEX2D_NO_SAMPLER(_Splat2);
		TCP2_TEX2D_NO_SAMPLER(_Splat3);
		TCP2_TEX2D_WITH_SAMPLER(_StylizedThreshold);
		TCP2_TEX2D_WITH_SAMPLER(_Mask0);
		TCP2_TEX2D_NO_SAMPLER(_Mask1);
		TCP2_TEX2D_NO_SAMPLER(_Mask2);
		TCP2_TEX2D_NO_SAMPLER(_Mask3);
		TCP2_TEX2D_WITH_SAMPLER(_MainTex);
		
		// Shader Properties
		float _Layer0HeightOffset;
		float _Layer1HeightOffset;
		float _Layer2HeightOffset;
		float _Layer3HeightOffset;
		float4 _Splat0_ST;
		float4 _Splat1_ST;
		float4 _Splat2_ST;
		float4 _Splat3_ST;
		float _HSV_H;
		float _HSV_S;
		float _HSV_V;
		fixed4 _Color;
		float4 _StylizedThreshold_ST;
		float _RampThreshold;
		float _RampSmoothing;
		fixed4 _HColor;
		fixed4 _SColor;
		fixed4 _DiffuseTint;
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

		// Main Surface Shader

		CGPROGRAM

		#pragma surface surf ToonyColorsCustom vertex:vertex_surface exclude_path:deferred exclude_path:prepass keepalpha addshadow fullforwardshadows nolppv decal:add nometa
		#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
		#pragma target 3.0

		//================================================================
		// SHADER KEYWORDS

		#pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
		#pragma multi_compile_local_fragment __ _ALPHATEST_ON
		#pragma shader_feature_local_fragment TCP2_TEXTURED_THRESHOLD

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
			half4 tangent : TANGENT;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Input
		{
			half3 tangent;
			float2 texcoord0;
		};

		//================================================================

		// Custom SurfaceOutput
		struct SurfaceOutputCustom
		{
			half atten;
			half3 Albedo;
			half3 Normal;
			half3 Emission;
			half Specular;
			half Gloss;
			half Alpha;
			float3 normalTS;

			Input input;

			half terrainWeight;

			// Shader Properties
			float __stylizedThreshold;
			float __stylizedThresholdScale;
			float __rampThreshold;
			float __rampSmoothing;
			float3 __highlightColor;
			float3 __shadowColor;
			float3 __diffuseTint;
			float __ambientIntensity;
		};

		//================================================================
		// VERTEX FUNCTION

		void vertex_surface(inout appdata_tcp2 v, out Input output)
		{
			UNITY_INITIALIZE_OUTPUT(Input, output);

			TerrainInstancing(v.vertex, v.normal, v.texcoord0.xy);
				v.tangent.xyz = cross(v.normal, float3(0,0,1));
				v.tangent.w = -1;

			// Texture Coordinates
			output.texcoord0 = v.texcoord0.xy;

			output.tangent = mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz;

		}

		//================================================================
		// SURFACE FUNCTION

		void surf(Input input, inout SurfaceOutputCustom output)
		{
			// Shader Properties Sampling
			float4 __layer0Mask = ( TCP2_TEX2D_SAMPLE(_Mask0, _Mask0, input.texcoord0.xy * _Splat0_ST.xy + _Splat0_ST.zw).rgba );
			float __layer0HeightSource = ( __layer0Mask.b );
			float __layer0HeightOffset = ( _Layer0HeightOffset );
			float4 __layer1Mask = ( TCP2_TEX2D_SAMPLE(_Mask1, _Mask0, input.texcoord0.xy * _Splat1_ST.xy + _Splat1_ST.zw).rgba );
			float __layer1HeightSource = ( __layer1Mask.b );
			float __layer1HeightOffset = ( _Layer1HeightOffset );
			float4 __layer2Mask = ( TCP2_TEX2D_SAMPLE(_Mask2, _Mask0, input.texcoord0.xy * _Splat2_ST.xy + _Splat2_ST.zw).rgba );
			float __layer2HeightSource = ( __layer2Mask.b );
			float __layer2HeightOffset = ( _Layer2HeightOffset );
			float4 __layer3Mask = ( TCP2_TEX2D_SAMPLE(_Mask3, _Mask0, input.texcoord0.xy * _Splat3_ST.xy + _Splat3_ST.zw).rgba );
			float __layer3HeightSource = ( __layer3Mask.b );
			float __layer3HeightOffset = ( _Layer3HeightOffset );
			float4 __layer0NormalMapAddpass = ( TCP2_TEX2D_SAMPLE(_Normal0, _Normal0, input.texcoord0.xy * _Splat0_ST.xy + _Splat0_ST.zw).rgba );
			float4 __layer1NormalMapAddpass = ( TCP2_TEX2D_SAMPLE(_Normal1, _Normal0, input.texcoord0.xy * _Splat1_ST.xy + _Splat1_ST.zw).rgba );
			float4 __layer2NormalMapAddpass = ( TCP2_TEX2D_SAMPLE(_Normal2, _Normal0, input.texcoord0.xy * _Splat2_ST.xy + _Splat2_ST.zw).rgba );
			float4 __layer3NormalMapAddpass = ( TCP2_TEX2D_SAMPLE(_Normal3, _Normal0, input.texcoord0.xy * _Splat3_ST.xy + _Splat3_ST.zw).rgba );
			float4 __layer0AlbedoAddpass = ( TCP2_TEX2D_SAMPLE(_Splat0, _Splat0, input.texcoord0.xy * _Splat0_ST.xy + _Splat0_ST.zw).rgba );
			float4 __layer1AlbedoAddpass = ( TCP2_TEX2D_SAMPLE(_Splat1, _Splat0, input.texcoord0.xy * _Splat1_ST.xy + _Splat1_ST.zw).rgba );
			float4 __layer2AlbedoAddpass = ( TCP2_TEX2D_SAMPLE(_Splat2, _Splat0, input.texcoord0.xy * _Splat2_ST.xy + _Splat2_ST.zw).rgba );
			float4 __layer3AlbedoAddpass = ( TCP2_TEX2D_SAMPLE(_Splat3, _Splat0, input.texcoord0.xy * _Splat3_ST.xy + _Splat3_ST.zw).rgba );
			float __albedoHue = ( _HSV_H );
			float __albedoSaturation = ( _HSV_S );
			float __albedoValue = ( _HSV_V );
			float4 __mainColor = ( _Color.rgba );
			output.__stylizedThreshold = ( TCP2_TEX2D_SAMPLE(_StylizedThreshold, _StylizedThreshold, input.texcoord0.xy * _StylizedThreshold_ST.xy + _StylizedThreshold_ST.zw).a );
			output.__stylizedThresholdScale = ( 1.0 );
			output.__rampThreshold = ( _RampThreshold );
			output.__rampSmoothing = ( _RampSmoothing );
			output.__highlightColor = ( _HColor.rgb );
			output.__shadowColor = ( _SColor.rgb );
			output.__diffuseTint = ( _DiffuseTint.rgb );
			output.__ambientIntensity = ( 1.0 );

			output.input = input;

			// Terrain
			
			float2 terrainTexcoord0 = input.texcoord0.xy;
			
			#if defined(_ALPHATEST_ON)
				ClipHoles(terrainTexcoord0.xy);
			#endif
			
			#if defined(TERRAIN_BASE_PASS)
			
				half4 terrain_mixedDiffuse = TCP2_TEX2D_SAMPLE(_MainTex, _MainTex, terrainTexcoord0.xy).rgba;
				half3 normalTS = half3(0.0h, 0.0h, 1.0h);
			
			#else
			
				// Sample the splat control texture generated by the terrain
				// adjust splat UVs so the edges of the terrain tile lie on pixel centers
				float2 terrainSplatUV = (terrainTexcoord0.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
				half4 terrain_splat_control_0 = TCP2_TEX2D_SAMPLE(_Control, _Control, terrainSplatUV);
				half height0 = __layer0HeightSource + __layer0HeightOffset;
				half height1 = __layer1HeightSource + __layer1HeightOffset;
				half height2 = __layer2HeightSource + __layer2HeightOffset;
				half height3 = __layer3HeightSource + __layer3HeightOffset;
				HeightBasedSplatModify(terrain_splat_control_0, half4(height0, height1, height2, height3));
			
				// Calculate weights and perform the texture blending
				half terrain_weight = dot(terrain_splat_control_0, half4(1,1,1,1));
			
				#if !defined(SHADER_API_MOBILE) && defined(TERRAIN_SPLAT_ADDPASS)
					clip(terrain_weight == 0.0f ? -1 : 1);
				#endif
			
				// Normalize weights before lighting and restore afterwards so that the overall lighting result can be correctly weighted
				terrain_splat_control_0 /= (terrain_weight + 1e-3f);
			
				// Sample terrain normal maps
				half4 normal0 = __layer0NormalMapAddpass;
				half4 normal1 = __layer1NormalMapAddpass;
				half4 normal2 = __layer2NormalMapAddpass;
				half4 normal3 = __layer3NormalMapAddpass;
				#define UnpackFunction UnpackNormalWithScale
				half3 normalTS = UnpackFunction(normal0, _NormalScale0) * terrain_splat_control_0.r;
				normalTS += UnpackFunction(normal1, _NormalScale1) * terrain_splat_control_0.g;
				normalTS += UnpackFunction(normal2, _NormalScale2) * terrain_splat_control_0.b;
				normalTS += UnpackFunction(normal3, _NormalScale3) * terrain_splat_control_0.a;
				normalTS.z += 1e-3f; // to avoid nan after normalizing
			
				output.Normal = normalTS;
			
			#endif // TERRAIN_BASE_PASS
			
			#if defined(INSTANCING_ON) && defined(SHADER_TARGET_SURFACE_ANALYSIS) && defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
				output.Normal = float3(0, 0, 1); // make sure that surface shader compiler realizes we write to normal, as UNITY_INSTANCING_ENABLED is not defined for SHADER_TARGET_SURFACE_ANALYSIS.
			#endif
				
			// Terrain normal, if using instancing and per-pixel normal map
			#if defined(UNITY_INSTANCING_ENABLED) && !defined(SHADER_API_D3D11_9X) && defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
				float2 terrainNormalCoords = (terrainTexcoord0.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
				float3 geomNormal = normalize(TCP2_TEX2D_SAMPLE(_TerrainNormalmapTexture, _TerrainNormalmapTexture, terrainNormalCoords.xy).xyz * 2 - 1);
			
				float3 geomTangent = normalize(cross(geomNormal, float3(0, 0, 1)));
				float3 geomBitangent = normalize(cross(geomTangent, geomNormal));
				output.Normal = output.Normal.x * geomTangent
							  + output.Normal.y * geomBitangent
							  + output.Normal.z * geomNormal;
				output.Normal = output.Normal.xzy;
			#endif
			
			output.Albedo = half3(1,1,1);
			output.Alpha = 1;

			#if !defined(TERRAIN_BASE_PASS)
				// Sample textures that will be blended based on the terrain splat map
				half4 splat0 = __layer0AlbedoAddpass;
				half4 splat1 = __layer1AlbedoAddpass;
				half4 splat2 = __layer2AlbedoAddpass;
				half4 splat3 = __layer3AlbedoAddpass;
			
				#define BLEND_TERRAIN_HALF4(outVariable, sourceVariable) \
					half4 outVariable = terrain_splat_control_0.r * sourceVariable##0; \
					outVariable += terrain_splat_control_0.g * sourceVariable##1; \
					outVariable += terrain_splat_control_0.b * sourceVariable##2; \
					outVariable += terrain_splat_control_0.a * sourceVariable##3;
				#define BLEND_TERRAIN_HALF(outVariable, sourceVariable) \
					half4 outVariable = dot(terrain_splat_control_0, half4(sourceVariable##0, sourceVariable##1, sourceVariable##2, sourceVariable##3));
			
				BLEND_TERRAIN_HALF4(terrain_mixedDiffuse, splat)
			
			#endif // !TERRAIN_BASE_PASS
			
			#if !defined(TERRAIN_BASE_PASS)
				output.terrainWeight = terrain_weight;
			#endif
			
			output.Albedo = terrain_mixedDiffuse.rgb;
			output.Alpha = terrain_mixedDiffuse.a;
			
			//Albedo HSV
			output.Albedo = ApplyHSV_3(output.Albedo, __albedoHue, __albedoSaturation, __albedoValue);
			
			output.Albedo *= __mainColor.rgb;

		}

		//================================================================
		// LIGHTING FUNCTION

		inline half4 LightingToonyColorsCustom(inout SurfaceOutputCustom surface, UnityGI gi)
		{

			half3 lightDir = gi.light.dir;
			#if defined(UNITY_PASS_FORWARDBASE)
				half3 lightColor = _LightColor0.rgb;
				half atten = surface.atten;
			#else
				// extract attenuation from point/spot lights
				half3 lightColor = _LightColor0.rgb;
				half atten = max(gi.light.color.r, max(gi.light.color.g, gi.light.color.b)) / max(_LightColor0.r, max(_LightColor0.g, _LightColor0.b));
			#endif

			half3 normal = normalize(surface.Normal);
			half ndl = dot(normal, lightDir);
			#if defined(TCP2_TEXTURED_THRESHOLD)
			float stylizedThreshold = surface.__stylizedThreshold;
			stylizedThreshold -= 0.5;
			stylizedThreshold *= surface.__stylizedThresholdScale;
			ndl += stylizedThreshold;
			#endif
			half3 ramp;
			
			#define		RAMP_THRESHOLD	surface.__rampThreshold
			#define		RAMP_SMOOTH		surface.__rampSmoothing
			ndl = saturate(ndl);
			ramp = smoothstep(RAMP_THRESHOLD - RAMP_SMOOTH*0.5, RAMP_THRESHOLD + RAMP_SMOOTH*0.5, ndl);

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
			ramp *= diffuseTint;
			
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

			#if !defined(TERRAIN_BASE_PASS)
				color.rgb *= surface.terrainWeight;
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
			half3 normal = surface.Normal;

			// GI without reflection probes
			half tcp2_atten;
			gi = UnityGlobalIllumination_TCP2(data, 1.0, normal, tcp2_atten); // occlusion is applied in the lighting function, if necessary

			surface.atten = tcp2_atten; // transfer attenuation to lighting function

		}

		ENDCG

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
			#pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd

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
				float4 tangent : TANGENT;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f_shadowcaster
			{
				V2F_SHADOW_CASTER_NOPOS
				float4 vcolor : TEXCOORD1;
				float2 pack1 : TEXCOORD2; /* pack1.xy = texcoord0 */
				UNITY_VERTEX_OUTPUT_STEREO
			};

			void vertex_shadowcaster (appdata_shadowcaster v, out v2f_shadowcaster output, out float4 opos : SV_POSITION)
			{
				UNITY_INITIALIZE_OUTPUT(v2f_shadowcaster, output);
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				// Texture Coordinates
				output.pack1.xy.xy = v.texcoord0.xy * _MainTex_ST.xy + _MainTex_ST.zw;

				TRANSFER_SHADOW_CASTER_NOPOS(output,opos)
			}

			half4 fragment_shadowcaster(v2f_shadowcaster input, UNITY_VPOS_TYPE vpos : VPOS) : SV_Target
			{

				// Shader Properties Sampling
				float4 __albedo = ( TCP2_TEX2D_SAMPLE(_MainTex, _MainTex, input.pack1.xy).rgba );
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
		UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
		UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
	}

	Fallback "Diffuse"
	CustomEditor "ToonyColorsPro.ShaderGenerator.MaterialInspector_SG2"
}

