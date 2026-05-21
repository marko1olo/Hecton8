Shader "Hidden/HECTON8/AITexture/ControlMapScenePreview"
{
    Properties
    {
        _PreviewMode ("Preview Mode", Float) = 3
        _BakeBoundsMin ("Bake Bounds Min", Vector) = (0,0,0,0)
        _BakeBoundsInvSize ("Bake Bounds Inv Size", Vector) = (1,1,1,0)
        _BakeColorId ("Bake Color Id", Vector) = (0.16,0.82,1,1)
        _CurvatureScale ("Curvature Scale", Float) = 0.85
        _CurvatureEdgeGain ("Curvature Edge Gain", Float) = 12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            int _PreviewMode;
            float4 _BakeBoundsMin;
            float4 _BakeBoundsInvSize;
            float4 _BakeColorId;
            float _CurvatureScale;
            float _CurvatureEdgeGain;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            float3 safe_normalize(float3 v)
            {
                return v * rsqrt(max(dot(v, v), 1e-8));
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                o.worldNormal = safe_normalize(UnityObjectToWorldNormal(v.normal));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal01 = safe_normalize(i.worldNormal) * 0.5 + 0.5;
                float depth01 = saturate((i.localPos.z - _BakeBoundsMin.z) * _BakeBoundsInvSize.z);
                float curvature = (length(ddx(normal01)) + length(ddy(normal01))) * _CurvatureScale;
                float curvature01 = saturate(0.5 + (curvature - 0.02) * _CurvatureEdgeGain);

                if (_PreviewMode == 0)
                    return fixed4(normal01, 1.0);
                if (_PreviewMode == 1)
                    return fixed4(depth01, depth01, depth01, 1.0);
                if (_PreviewMode == 2)
                    return fixed4(_BakeColorId.rgb, 1.0);

                return fixed4(curvature01, curvature01, curvature01, 1.0);
            }
            ENDCG
        }
    }
}
