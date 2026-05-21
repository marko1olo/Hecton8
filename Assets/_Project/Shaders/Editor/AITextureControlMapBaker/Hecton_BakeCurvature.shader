Shader "Hecton8/Editor/AITexture/Hecton_BakeCurvature"
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

            float _CurvatureScale;
            float _CurvatureEdgeGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            float3 safe_normalize(float3 v)
            {
                return v * rsqrt(max(dot(v, v), 1e-8));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = float4(v.uv.x * 2.0 - 1.0, v.uv.y * 2.0 - 1.0, 0.0, 1.0);
                o.positionOS = v.vertex.xyz;
                o.normalWS = safe_normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = safe_normalize(i.normalWS);
                float3 dpdx = ddx(i.positionOS);
                float3 dpdy = ddy(i.positionOS);
                float3 dndx = ddx(n);
                float3 dndy = ddy(n);
                float signedCurvature = (dot(dndx, safe_normalize(dpdx)) + dot(dndy, safe_normalize(dpdy))) * _CurvatureScale;
                float edgeStrength = saturate((length(dndx) + length(dndy)) * _CurvatureEdgeGain);
                float value = lerp(0.5, saturate(0.5 + signedCurvature), edgeStrength);
                return float4(value, value, value, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
