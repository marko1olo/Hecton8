Shader "HECTON/UI/Terminal TextureArray Panel"
{
    Properties
    {
        _TerminalTextureArray("Terminal Texture Array", 2DArray) = "" {}
        _TerminalSlice("Terminal Slice", Float) = 0
        _EmissionTint("Emission Tint", Color) = (0.70, 1.0, 0.78, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "PreviewType" = "Plane"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Back
        ZWrite On
        Blend Off

        Pass
        {
            Name "TerminalPanel"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ HECTON_TERMINAL_INSTANCED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_TerminalTextureArray);
            SAMPLER(sampler_TerminalTextureArray);

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionTint;
                float _TerminalSlice;
            CBUFFER_END

            struct TerminalPanelInstanceDTO
            {
                float4x4 LocalToWorld;
                float4 SliceFlags;
            };

            StructuredBuffer<TerminalPanelInstanceDTO> _TerminalPanelInstances;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float slice : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
#if defined(HECTON_TERMINAL_INSTANCED)
                TerminalPanelInstanceDTO instance = _TerminalPanelInstances[input.instanceID];
                float4 world = mul(instance.LocalToWorld, float4(input.positionOS.xyz, 1.0));
                output.positionCS = TransformWorldToHClip(world.xyz);
                output.slice = instance.SliceFlags.x;
#else
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.slice = _TerminalSlice;
#endif
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 sampleColor = SAMPLE_TEXTURE2D_ARRAY(_TerminalTextureArray, sampler_TerminalTextureArray, input.uv, input.slice);
                return half4(sampleColor.rgb * _EmissionTint.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
