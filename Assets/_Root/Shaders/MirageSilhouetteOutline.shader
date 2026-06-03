Shader "Kiyamet/MirageSilhouetteOutline"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.92, 1, 0.85)
        _OutlineWidth ("Outline Width", Range(0.001, 0.08)) = 0.016
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+120"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 normalWs = normalize(UnityObjectToWorldNormal(v.normal));
                float3 positionWs = mul(unity_ObjectToWorld, v.vertex).xyz;
                positionWs += normalWs * _OutlineWidth;
                o.pos = UnityWorldToClipPos(positionWs);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = _Color;
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
