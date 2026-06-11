Shader "Hecton8/PDA/FrequencyTuningWave"
{
    Properties
    {
        _HectonFrequencyTuningTubeRadius ("Tube Radius", Float) = 0.003
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FrequencyTuningWave"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4x4 _HectonFrequencyTuningLocalToWorld;
            float _HectonFrequencyTuningTubeRadius;
            float4 _HectonFrequencyTuningTimeErrorStage;
            float4 _HectonFrequencyTuningWaveScalars;
            float4 _HectonFrequencyTuningWaveLayout;
            float4 _HectonVrComfortSignals;
            float4 _HectonVrComfortMotion;
            float4 _HectonVRSomaticComfortState;
            float _HectonVRBrownoutIntensity;
            float _HectonTunnelingIntensity;
            float _H8GlobalQualityWeight;

            float TriangleWaveSigned(float phase)
            {
                float lane = frac(phase + 0.25);
                return 1.0 - abs(lane * 2.0 - 1.0) * 2.0;
            }

            float HectonComfortIgn(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float2 ResolveHectonComfortEyeStableScreenUV(float2 positionCS)
            {
                float2 screenUV = saturate(positionCS * rcp(max(_ScreenParams.xy, float2(1.0, 1.0))));
#if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                float4 stereoScaleOffset = unity_StereoScaleOffset[unity_StereoEyeIndex];
                screenUV = (screenUV - stereoScaleOffset.zw) * rcp(max(stereoScaleOffset.xy, float2(0.0001, 0.0001)));
#endif
                return saturate(screenUV);
            }

            float ResolveHectonComfortBlackAmount(float2 screenUV, float2 positionCS)
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
                float ign = HectonComfortIgn(floor(positionCS));
                float tunnelDither = step(ign, saturate(tunnelMask + vrComfortTunnel * 0.0625));
                float comfortQualityWeight = saturate(_H8GlobalQualityWeight);
                float ditherFloor = 0.56 - 0.06 * comfortQualityWeight;
                float ditherCeiling = 0.90 + 0.06 * comfortQualityWeight;
                float ditheredTunnel = tunnelMask * lerp(ditherFloor, ditherCeiling, tunnelDither);
                return saturate(max(ditheredTunnel, vrComfortBlackout));
            }

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR0;
            };

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint segmentCount = max(1u, (uint)(_HectonFrequencyTuningWaveLayout.x + 0.5));
                bool playerWave = input.instanceID >= segmentCount;
                uint segmentIndex = playerWave ? input.instanceID - segmentCount : input.instanceID;
                float invSegmentCount = rcp(max(1.0, (float)segmentCount));
                float normalized0 = (float)segmentIndex * invSegmentCount;
                float normalized1 = (float)(segmentIndex + 1u) * invSegmentCount;
                float localWidth = max(0.01, _HectonFrequencyTuningWaveLayout.y);
                float localHeight = max(0.01, _HectonFrequencyTuningWaveLayout.z);
                float frequency = playerWave ? _HectonFrequencyTuningWaveScalars.z : _HectonFrequencyTuningWaveScalars.x;
                float amplitude = playerWave ? _HectonFrequencyTuningWaveScalars.w : _HectonFrequencyTuningWaveScalars.y;
                float baseY = playerWave ? -0.18 : 0.18;
                float wave0 = TriangleWaveSigned(normalized0 * frequency) * amplitude;
                float wave1 = TriangleWaveSigned(normalized1 * frequency) * amplitude;
                float2 start = float2((normalized0 - 0.5) * localWidth, baseY * localHeight + wave0 * localHeight * 0.32);
                float2 finish = float2((normalized1 - 0.5) * localWidth, baseY * localHeight + wave1 * localHeight * 0.32);
                float2 delta = finish - start;
                float lengthSq = max(dot(delta, delta), 0.00000001);
                float invLength = rsqrt(lengthSq);
                float length = lengthSq * invLength;
                float2 tangent = delta * invLength;
                float2 center = (start + finish) * 0.5;
                float pulse = 1.0 + saturate(1.0 - _HectonFrequencyTuningTimeErrorStage.y) * 0.22;
                float2 normal = float2(-tangent.y, tangent.x);
                float tubeRadius = _HectonFrequencyTuningTubeRadius * pulse;
                float2 local2 = center +
                    tangent * (input.positionOS.x * length) +
                    normal * (input.positionOS.y * tubeRadius * 2.0);
                float3 local = float3(local2, 0.0);
                float3 world = mul(_HectonFrequencyTuningLocalToWorld, float4(local, 1.0)).xyz;
                float stage = _HectonFrequencyTuningTimeErrorStage.z;
                output.positionCS = TransformWorldToHClip(world);
                output.uv = input.uv;
                output.color = playerWave
                    ? half4(0.02h, 0.82h, 1.0h, 0.92h)
                    : half4(1.0h, 0.08h, 0.04h, (half)(0.92 + stage * 0.02));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centered = input.uv * 2.0 - 1.0;
                half sideMask = (half)saturate((1.0 - abs(centered.y)) * 3.5714286);
                half mask = sideMask;
                half alpha = input.color.a * mask;
                half3 color = input.color.rgb * (0.72h + mask * 0.55h);
                float2 comfortScreenUV = ResolveHectonComfortEyeStableScreenUV(input.positionCS.xy);
                float comfortBlackAmount = ResolveHectonComfortBlackAmount(comfortScreenUV, input.positionCS.xy);
                color = lerp(color, half3(0.0015h, 0.0023h, 0.0031h), (half)comfortBlackAmount);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
