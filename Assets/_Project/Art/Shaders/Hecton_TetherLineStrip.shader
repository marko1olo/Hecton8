Shader "Hecton8/Physics/TetherLineStrip"
{
    Properties
    {
        [MainColor] _TetherColor ("Tether Color", Color) = (0.22, 0.92, 0.96, 0.92)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "TetherLineStrip"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _TetherPositions;

            CBUFFER_START(UnityPerMaterial)
                float4 _TetherColor;
                int _TetherPointCount;
            CBUFFER_END

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                int clampedIndex = clamp((int)input.vertexID, 0, max(_TetherPointCount - 1, 0));
                float3 positionWS = _TetherPositions[clampedIndex];
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = _TetherColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
