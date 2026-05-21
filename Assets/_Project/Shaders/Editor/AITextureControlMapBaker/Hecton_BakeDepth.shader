Shader "Hecton8/Editor/AITexture/Hecton_BakeDepth"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BakeBoundsMin;
            float4 _BakeBoundsInvSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float depth01 : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = float4(v.uv.x * 2.0 - 1.0, v.uv.y * 2.0 - 1.0, 0.0, 1.0);
                o.depth01 = saturate((v.vertex.z - _BakeBoundsMin.z) * _BakeBoundsInvSize.z);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return float4(i.depth01, i.depth01, i.depth01, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
