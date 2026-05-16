Shader "Kiyamet/TimeDistortionDome"
{
    Properties
    {
        [Header(Main Texture)]
        _MainTex ("Albedo (RGB) Alpha (A)", 2D) = "white" {}
        _Color ("Tint", Color) = (0.12, 0.75, 0.9, 0.32)

        [Header(Emission)]
        _EmissionMap ("Emission (optional)", 2D) = "black" {}
        _EmissionColor ("Emission", Color) = (0.08, 1.1, 0.45, 1)
        _EmissionStrength ("Emission Strength", Range(0, 3)) = 0.4

        [Header(Rim)]
        _RimColor ("Rim", Color) = (0.25, 1, 0.85, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.4
        _FresnelBoost ("Fresnel Boost", Range(0, 3)) = 1.35
        _InteriorAlphaBoost ("Interior Alpha Boost", Range(0, 2)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 200

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _EmissionMap;
            float4 _EmissionMap_ST;

            fixed4 _Color;
            fixed4 _EmissionColor;
            fixed4 _RimColor;
            float _RimPower;
            float _FresnelBoost;
            float _InteriorAlphaBoost;
            float _EmissionStrength;

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(worldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                fixed3 emissionSample = tex2D(_EmissionMap, TRANSFORM_TEX(i.uv, _EmissionMap)).rgb;

                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float ndv = abs(dot(n, v));
                float rim = pow(1.0 - saturate(ndv), _RimPower) * _FresnelBoost;

                fixed4 col = albedo;
                col.rgb += emissionSample * _EmissionColor.rgb * _EmissionStrength;
                col.rgb += _EmissionColor.rgb * _EmissionStrength * 0.25;
                col.rgb += _RimColor.rgb * rim * _RimColor.a;
                col.a = saturate(albedo.a + rim * _InteriorAlphaBoost);
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
