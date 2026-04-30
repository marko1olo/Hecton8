Shader "Hecton8/UI/PDA Sonar Point Cloud"
{
    Properties
    {
        _PointSize ("Point Size", Float) = 2.5
        _Opacity ("Opacity", Range(0, 1)) = 0.82
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha One

        Pass
        {
            Name "PDASonarPointCloud"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SonarPoint
            {
                float4 LocalPositionIntensity;
                float4 Color;
            };

            StructuredBuffer<SonarPoint> _SonarPoints;

            CBUFFER_START(UnityPerMaterial)
                float4x4 _PointCloudLocalToWorld;
                float _PointSize;
                float _Opacity;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR0;
                float size : PSIZE;
            };

            Varyings vert(uint vertexId : SV_VertexID)
            {
                SonarPoint pointData = _SonarPoints[vertexId];
                float4 worldPosition = mul(_PointCloudLocalToWorld, float4(pointData.LocalPositionIntensity.xyz, 1.0));

                Varyings output;
                output.positionCS = TransformWorldToHClip(worldPosition.xyz);
                output.color = pointData.Color;
                output.color.a *= saturate(pointData.LocalPositionIntensity.w) * saturate(_Opacity);
                output.size = max(_PointSize, 1.0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(input.color.a - 0.001);
                return half4(input.color.rgb, input.color.a);
            }
            ENDHLSL
        }
    }
}
