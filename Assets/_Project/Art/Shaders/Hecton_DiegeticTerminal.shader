Shader "HECTON/UI/Diegetic Terminal"
{
    Properties
    {
        _TerminalTextureArray("Terminal Texture Array", 2DArray) = "" {}
        _TerminalSlice("Terminal Slice", Float) = 0
        _EmissionTint("Emission Tint", Color) = (0.62, 1.0, 0.82, 1.0)
        _HectonDiegeticGlitchQualityWeight("Global Quality Weight", Range(0, 1)) = 1
        _HectonTerminalGlow("Terminal Glow", Range(0, 4)) = 1.35
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
            Name "DiegeticTerminal"
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
                float _HectonDiegeticGlitchQualityWeight;
                float _HectonTerminalGlow;
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
                nointerpolation float quality : TEXCOORD2;
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
                output.quality = saturate(max(instance.SliceFlags.z, _HectonDiegeticGlitchQualityWeight));
#else
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.slice = _TerminalSlice;
                output.quality = saturate(_HectonDiegeticGlitchQualityWeight);
#endif
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;
                half4 sampleColor = SAMPLE_TEXTURE2D_ARRAY(_TerminalTextureArray, sampler_TerminalTextureArray, uv, input.slice);
                float quality = saturate(input.quality);
                float scan = lerp(0.72, 0.94, quality) + frac(uv.y * lerp(56.0, 192.0, quality)) * 0.04;
                float2 edge = abs(uv - 0.5) * 2.0;
                float vignette = saturate(1.15 - dot(edge, edge) * lerp(0.22, 0.08, quality));
                float glow = lerp(0.82, _HectonTerminalGlow, quality);
                half3 color = sampleColor.rgb * _EmissionTint.rgb * scan * vignette * glow;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
