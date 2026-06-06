Shader "Kiyamet/ReflectorLightBeam"
{
    Properties
    {
        _EmissionColor ("Emission Color", Color) = (1, 0.94, 0.72, 1)
        _EmissionStrength ("Emission Strength", Float) = 6
        _PulseSpeed ("Pulse Speed", Float) = 5
        _ShimmerScale ("Shimmer Scale", Float) = 18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _EmissionColor;
            float _EmissionStrength;
            float _PulseSpeed;
            float _ShimmerScale;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float pulse = 0.82 + 0.18 * sin(_Time.y * _PulseSpeed);
                float shimmer = 0.88 + 0.12 * sin(_Time.y * (_PulseSpeed * 1.35f) + i.uv.x * _ShimmerScale);
                fixed4 emission = _EmissionColor * _EmissionStrength * pulse * shimmer;
                emission.rgb *= i.color.rgb;
                emission.rgb *= i.color.a;
                return emission;
            }
            ENDCG
        }
    }

    FallBack Off
}
