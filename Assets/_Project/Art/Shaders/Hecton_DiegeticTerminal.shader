Shader "HECTON/UI/Diegetic Terminal"
{
    Properties
    {
        _TerminalTextureArray("Terminal Texture Array", 2DArray) = "" {}
        _TerminalSlice("Terminal Slice", Float) = 0
        _EmissionTint("Emission Tint", Color) = (0.62, 1.0, 0.82, 1.0)
        _HectonDiegeticGlitchQualityWeight("Global Quality Weight", Range(0, 1)) = 1
        _HectonDecryptionNoiseDensity("Decryption Noise Density", Range(0, 2)) = 1
        _HectonTerminalInstancedMode("Instanced Mode", Float) = 0
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
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_TerminalTextureArray);
            SAMPLER(sampler_TerminalTextureArray);

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionTint;
                float _TerminalSlice;
                float _HectonDiegeticGlitchQualityWeight;
                float _HectonDecryptionNoiseDensity;
                float _HectonTerminalInstancedMode;
                float _HectonTerminalGlow;
                float _GlobalDecryptionPuzzleCount;
            CBUFFER_END

            struct TerminalPanelInstanceDTO
            {
                float4x4 LocalToWorld;
                float4 SliceFlags;
            };

            struct GlobalDecryptionPuzzleDTO
            {
                float PlayerFrequency;
                float PlayerPhase;
                float TargetFrequency;
                float TargetPhase;
                float AlignmentAccuracy01;
                uint PuzzleID;
                uint Flags;
                uint Pad0;
            };

            StructuredBuffer<TerminalPanelInstanceDTO> _TerminalPanelInstances;
            StructuredBuffer<GlobalDecryptionPuzzleDTO> _GlobalDecryptionPuzzles;

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
                if (_HectonTerminalInstancedMode >= 0.5)
                {
                    TerminalPanelInstanceDTO instance = _TerminalPanelInstances[input.instanceID];
                    float4 instancedWorld = mul(instance.LocalToWorld, float4(input.positionOS.xyz, 1.0));
                    output.positionCS = TransformWorldToHClip(instancedWorld.xyz);
                    output.slice = instance.SliceFlags.x;
                    output.quality = saturate(max(instance.SliceFlags.z, _HectonDiegeticGlitchQualityWeight));
                }
                else
                {
                    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                    output.positionCS = positionInputs.positionCS;
                    output.slice = _TerminalSlice;
                    output.quality = saturate(_HectonDiegeticGlitchQualityWeight);
                }
                output.uv = input.uv;
                return output;
            }

            float H8TerminalHash21(float2 value)
            {
                return frac(sin(dot(value, float2(12.9898, 78.233))) * 43758.5453);
            }

            float H8TerminalWaveLine(float2 uv, float frequency, float phase, float thickness)
            {
                float waveY = 0.5 + sin((uv.x * frequency + phase) * 6.2831853) * 0.215;
                float distanceToLine = abs(uv.y - waveY);
                return 1.0 - smoothstep(thickness, thickness * 2.5, distanceToLine);
            }

            half3 H8ApplyDecryptionOverlay(half3 baseColor, float2 uv, float slice, float quality)
            {
                uint puzzleCount = (uint)max(0.0, _GlobalDecryptionPuzzleCount);
                uint puzzleIndex = (uint)round(slice);
                if (puzzleCount == 0u || puzzleIndex >= puzzleCount)
                    return baseColor;

                GlobalDecryptionPuzzleDTO puzzle = _GlobalDecryptionPuzzles[puzzleIndex];
                uint activeMask = 1u | 4u;
                if ((puzzle.Flags & activeMask) != activeMask)
                    return baseColor;

                float oscMask = smoothstep(0.16, 0.22, uv.y) * (1.0 - smoothstep(0.68, 0.76, uv.y));
                if (oscMask <= 0.0001)
                    return baseColor;

                float alignment = saturate(puzzle.AlignmentAccuracy01);
                float noiseDensity = saturate(_HectonDecryptionNoiseDensity * 0.5);
                float thickness = lerp(0.0024, 0.0075, quality);
                float targetLine = H8TerminalWaveLine(uv, max(0.1, puzzle.TargetFrequency), puzzle.TargetPhase, thickness * 1.15);
                float playerLine = H8TerminalWaveLine(uv, max(0.1, puzzle.PlayerFrequency), puzzle.PlayerPhase, thickness);
                float grid = (1.0 - smoothstep(0.004, 0.012, abs(frac(uv.x * lerp(12.0, 32.0, quality)) - 0.5))) * 0.065;
                float noiseCells = lerp(32.0, 224.0, quality) * lerp(0.5, 2.0, noiseDensity);
                float noise = H8TerminalHash21(floor(uv * noiseCells) + _Time.yy * 37.0);
                float interference = (1.0 - alignment) * lerp(0.0, 0.22, noiseDensity) * lerp(0.35, 1.0, quality) * noise;
                half3 targetColor = half3(1.0h, 0.55h, 0.16h) * (0.32h + (half)(alignment * 0.24));
                half3 playerColor = half3(0.18h, 0.92h, 1.0h) * (0.45h + (half)(alignment * 0.9));
                half3 solvedColor = half3(0.42h, 1.0h, 0.62h);
                half solved = (puzzle.Flags & 2u) != 0u ? 1.0h : 0.0h;
                half3 overlay = targetColor * (half)targetLine + lerp(playerColor, solvedColor, solved) * (half)playerLine;
                overlay += half3(0.12h, 0.45h, 0.5h) * (half)grid;
                baseColor = lerp(baseColor, baseColor * (1.0h - (half)interference), (half)oscMask);
                return baseColor + overlay * (half)oscMask;
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
                color = H8ApplyDecryptionOverlay(color, uv, input.slice, quality);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
