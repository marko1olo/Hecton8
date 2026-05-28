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
        [HideInInspector] _TerminalInputStateCount("Terminal Input State Count", Float) = 0
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
                float _TerminalInputStateCount;
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

            struct TerminalInputStateGPU
            {
                float2 ProjectedUV;
                uint TerminalHashID;
                uint InputFlags;
                float4 Reserved0;
            };

            StructuredBuffer<TerminalPanelInstanceDTO> _TerminalPanelInstances;
            StructuredBuffer<GlobalDecryptionPuzzleDTO> _GlobalDecryptionPuzzles;
            StructuredBuffer<TerminalInputStateGPU> _TerminalInputStates;
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;


            float _H8GlobalQualityWeight;
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
                nointerpolation uint terminalIndex : TEXCOORD3;
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
                    output.terminalIndex = input.instanceID;
                }
                else
                {
                    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                    output.positionCS = positionInputs.positionCS;
                    output.slice = _TerminalSlice;
                    output.quality = saturate(_HectonDiegeticGlitchQualityWeight);
                    output.terminalIndex = (uint)(saturate(_TerminalSlice * (1.0 / 63.0)) * 63.0 + 0.5);
                }
                output.uv = input.uv;
                return output;
            }

            float H8LinearRamp01(float edge0, float edge1, float value)
            {
                return saturate((value - edge0) / max(edge1 - edge0, 0.000001));
            }

            float H8LinearRampInv01(float edge0, float edge1, float value)
            {
                return 1.0 - H8LinearRamp01(edge0, edge1, value);
            }

            float H8TerminalTriangleSigned(float phase)
            {
                return abs(frac(phase + 0.25) * 2.0 - 1.0) * 2.0 - 1.0;
            }

            float H8TerminalHash21(float2 value)
            {
                float3 p3 = frac(float3(value.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float H8TerminalComfortIgn(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 H8ResolveComfortEyeStableScreenUV(float2 positionCS)
            {
                float2 screenUV = saturate(positionCS * rcp(max(_ScreenParams.xy, float2(1.0, 1.0))));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                float4 stereoScaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                screenUV = (screenUV - stereoScaleOffset.zw) * rcp(max(stereoScaleOffset.xy, float2(0.0001, 0.0001)));
#endif
                return saturate(screenUV);
            }

            float H8ResolveComfortBlackAmount(float2 screenUV, float2 positionCS)
            {
                float vrComfortEnabled = saturate(_HectonVrComfortSignals.w);
                float somaticTunnel = saturate(_HectonVRSomaticComfortState.x);
                float vrComfortTunnel = saturate(max(max(_HectonVrComfortSignals.x, _HectonVrComfortMotion.z) * vrComfortEnabled, max(_HectonTunnelingIntensity, somaticTunnel)));
                float vrComfortBlackout = saturate(max(_HectonVrComfortSignals.y * vrComfortEnabled, _HectonVRBrownoutIntensity));
                float2 radial = screenUV * 2.0 - 1.0;
                radial.x *= _ScreenParams.x * rcp(max(_ScreenParams.y, 1.0));
                float radialMagnitudeSq = saturate(dot(radial, radial));
                float tunnelInner = lerp(0.74, 0.34, vrComfortTunnel);
                float tunnelInnerSq = tunnelInner * tunnelInner;
                float tunnelMask = saturate((radialMagnitudeSq - tunnelInnerSq) * rcp(max(1.0 - tunnelInnerSq, 0.0009765625))) * vrComfortTunnel;
                float ign = H8TerminalComfortIgn(floor(positionCS));
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            float H8TerminalWaveLine(float2 uv, float frequency, float phase, float thickness)
            {
                float waveY = 0.5 + H8TerminalTriangleSigned(uv.x * frequency + phase) * 0.215;
                float distanceToLine = abs(uv.y - waveY);
                return H8LinearRampInv01(thickness, thickness * 2.5, distanceToLine);
            }

            half3 H8ApplyDecryptionOverlay(half3 baseColor, float2 uv, float slice, float quality)
            {
                uint puzzleCount = (uint)max(0.0, _GlobalDecryptionPuzzleCount);
                uint puzzleIndex = (uint)(saturate(slice * (1.0 / 63.0)) * 63.0 + 0.5);
                if (puzzleCount == 0u || puzzleIndex >= puzzleCount)
                    return baseColor;

                GlobalDecryptionPuzzleDTO puzzle = _GlobalDecryptionPuzzles[puzzleIndex];
                uint activeMask = 1u | 4u;
                if ((puzzle.Flags & activeMask) != activeMask)
                    return baseColor;

                float oscMask = H8LinearRamp01(0.16, 0.22, uv.y) * H8LinearRampInv01(0.68, 0.76, uv.y);
                if (oscMask <= 0.0001)
                    return baseColor;

                float alignment = saturate(puzzle.AlignmentAccuracy01);
                float noiseDensity = saturate(_HectonDecryptionNoiseDensity * 0.5);
                float thickness = lerp(0.0024, 0.0075, quality);
                float targetLine = H8TerminalWaveLine(uv, max(0.1, puzzle.TargetFrequency), puzzle.TargetPhase, thickness * 1.15);
                float playerLine = H8TerminalWaveLine(uv, max(0.1, puzzle.PlayerFrequency), puzzle.PlayerPhase, thickness);
                float grid = H8LinearRampInv01(0.004, 0.012, abs(frac(uv.x * lerp(12.0, 32.0, quality)) - 0.5)) * 0.065;
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

            half3 H8ApplyTerminalCursor(half3 baseColor, float2 uv, uint terminalIndex, float quality)
            {
                if (terminalIndex >= (uint)max(0.0, _TerminalInputStateCount))
                    return baseColor;

                TerminalInputStateGPU state = _TerminalInputStates[terminalIndex];
                uint hover = state.InputFlags & 1u;
                if (hover == 0u)
                    return baseColor;

                float2 cursorUv = saturate(state.ProjectedUV);
                float2 delta = uv - cursorUv;
                float distanceSq = dot(delta, delta);
                float radius = lerp(0.0065, 0.0035, quality);
                float ringRadius = radius * lerp(2.2, 3.4, quality);
                float core = H8LinearRampInv01(radius * radius, radius * radius * 2.25, distanceSq);
                float ringDeltaSq = abs(distanceSq - ringRadius * ringRadius);
                float ringWidthSq = max(radius * ringRadius, 0.000001);
                float ring = H8LinearRampInv01(ringWidthSq * 0.55, ringWidthSq * 1.35, ringDeltaSq) * H8LinearRamp01(0.22, 0.75, quality);
                float pressed = (state.InputFlags & 2u) != 0u ? 1.0 : 0.0;
                half3 cursorColor = lerp(half3(0.24h, 1.0h, 0.78h), half3(1.0h, 0.94h, 0.32h), (half)pressed);
                half mask = (half)saturate(core + ring * lerp(0.45, 0.8, quality));
                return lerp(baseColor, max(baseColor, cursorColor * (1.15h + (half)quality)), mask);
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
                if (_HectonTerminalInstancedMode >= 0.5)
                    color = H8ApplyTerminalCursor(color, uv, input.terminalIndex, quality);
                float2 screenUV = H8ResolveComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = H8ResolveComfortBlackAmount(screenUV, input.positionCS.xy);
                color = lerp(color, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
