Shader "Hecton8/Editor/AITexture/Hecton_BakeWorldNormal"
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

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            float3 safe_normalize(float3 v)
            {
                return v * rsqrt(max(dot(v, v), 1e-8));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = float4(v.uv.x * 2.0 - 1.0, v.uv.y * 2.0 - 1.0, 0.0, 1.0);
                o.normalWS = safe_normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = safe_normalize(i.normalWS);
                return float4(n * 0.5 + 0.5, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
