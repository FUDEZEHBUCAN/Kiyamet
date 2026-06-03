Shader "Kiyamet/ShadowDashEmissiveTrail"
{
    Properties
    {
        _EmissionColor ("Emission Color", Color) = (0.72, 0.35, 1, 1)
        _EmissionStrength ("Emission Strength", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+110"
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _EmissionColor;
            float _EmissionStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 emission = _EmissionColor * _EmissionStrength;
                emission.rgb *= i.color.rgb;
                emission.rgb *= i.color.a;
                emission.a = i.color.a;
                return emission;
            }
            ENDCG
        }
    }

    FallBack Off
}
