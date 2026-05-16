Shader "Hecton8/Ambient/BiotaIndirect"
{
    Properties
    {
        _PlanktonTint ("Plankton Tint", Color) = (0.18, 0.86, 0.72, 0.68)
        _AbyssTint ("Abyss Tint", Color) = (0.42, 0.64, 0.92, 0.54)
        _PanicTint ("Panic Tint", Color) = (0.82, 1.00, 0.86, 0.88)
        _SubsurfaceTint ("Subsurface Tint", Color) = (0.52, 0.92, 1.00, 1.00)
        _Opacity ("Opacity", Range(0, 1)) = 0.78
        _AlphaClip ("Alpha Clip", Range(0, 1)) = 0.035
        _SiltStrength ("Silt Strength", Range(0, 1)) = 0.22
        _SaltGlintStrength ("Salt Glint Strength", Range(0, 2)) = 0.62
        _SssStrength ("SSS Strength", Range(0, 3)) = 0.74
        _SssPower ("SSS Power", Range(1, 12)) = 4.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define HECTON_BIOTA_LOW_TIER 2u
            #define HECTON_BIOTA_REACTIVE 16u

            struct AmbientBiotaGpuInstance
            {
                float4 PositionScale;
                float4 VelocityEmission;
                uint StateFlags;
                uint StableHash;
                uint SpeciesBucket;
                uint Reserved;
                float4 VisualParams;
            };

            StructuredBuffer<AmbientBiotaGpuInstance> _HectonBiotaInstances;

            CBUFFER_START(UnityPerMaterial)
                half4 _PlanktonTint;
                half4 _AbyssTint;
                half4 _PanicTint;
                half4 _SubsurfaceTint;
                half _Opacity;
                half _AlphaClip;
                half _SiltStrength;
                half _SaltGlintStrength;
                half _SssStrength;
                half _SssPower;
                float _HectonBiotaCapacity;
                float _HectonBiotaActiveCount;
                float _HectonBiotaBiomeHash;
                float _HectonBiotaQualityProfile;
                float _HectonBiotaSystemStress01;
                float4 _HectonBiotaFlowVector;
                float _HectonBiotaOverkill01;
                float4 _HectonBiotaOriginWS;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 velocityEmission : TEXCOORD3;
                float4 visualParams : TEXCOORD4;
                nointerpolation uint stateFlags : TEXCOORD5;
                nointerpolation uint speciesBucket : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash11(float value)
            {
                float hash = frac(value * 0.1031);
                hash *= hash + 33.33;
                hash *= hash + hash;
                return frac(hash);
            }

            float TriPulse(float phase)
            {
                return abs(frac(phase) * 2.0 - 1.0);
            }

            float TriangleSigned(float phase)
            {
                return TriPulse(phase) * 2.0 - 1.0;
            }

            float2 SafeNormalize2(float2 value, float2 fallback)
            {
                float lengthSq = dot(value, value);
                float invLength = rsqrt(max(lengthSq, 1e-8));
                return lengthSq > 1e-8 ? value * invLength : fallback;
            }

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                float invLength = rsqrt(max(lengthSq, 1e-8));
                return lengthSq > 1e-8 ? value * invLength : fallback;
            }

            float2 Parallax16(float2 uv, float hash, float overkill01)
            {
                float2 flow = SafeNormalize2(_HectonBiotaFlowVector.xz + float2(0.07, 0.11), float2(0.70710678, 0.70710678));
                float2 offset = flow * (0.0016 + 0.0032 * overkill01);
                [unroll]
                for (int i = 0; i < 16; i++)
                {
                    float tap = ((float)i + 1.0) * 0.0625;
                    float height = TriangleSigned(dot(uv + offset * tap, float2(17.0, 23.0)) + hash * 13.1 + tap);
                    uv += offset * height * overkill01;
                }
                return uv;
            }

            float3 CameraRightWS()
            {
                return SafeNormalize3(float3(UNITY_MATRIX_I_V[0].x, UNITY_MATRIX_I_V[1].x, UNITY_MATRIX_I_V[2].x), float3(1.0, 0.0, 0.0));
            }

            float3 CameraUpWS()
            {
                return SafeNormalize3(float3(UNITY_MATRIX_I_V[0].y, UNITY_MATRIX_I_V[1].y, UNITY_MATRIX_I_V[2].y), float3(0.0, 1.0, 0.0));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                uint instanceID = input.instanceID;
            #if UNITY_ANY_INSTANCING_ENABLED
                instanceID = unity_InstanceID;
            #endif
                AmbientBiotaGpuInstance biota = _HectonBiotaInstances[instanceID];
                float active = step(0.0001, biota.PositionScale.w);
                float lowTier = (biota.StateFlags & HECTON_BIOTA_LOW_TIER) != 0u ? 1.0 : 0.0;
                float hash = biota.VisualParams.z;
                float age01 = biota.VisualParams.x;
                float pulse = TriangleSigned(hash + _Time.y * lerp(0.17, 0.41, _HectonBiotaOverkill01));
                float squash = lerp(1.0, 1.0 + pulse * 0.16, _HectonBiotaOverkill01);
                float2 quad = input.positionOS.xy;
                quad.x *= lerp(1.0, 1.0 + age01 * 0.22, lowTier);
                quad.y *= squash;

                float3 velocity = biota.VelocityEmission.xyz;
                float3 cameraRight = CameraRightWS();
                float3 cameraUp = CameraUpWS();
                float3 driftDir = SafeNormalize3(velocity, float3(0.0, 0.0, 1.0));
                float3 driftRight = SafeNormalize3(cross(float3(0.0, 1.0, 0.0), driftDir) + cameraRight * 0.001, cameraRight);
                float3 rightWS = SafeNormalize3(lerp(cameraRight, driftRight, _HectonBiotaOverkill01), cameraRight);
                float3 upWS = cameraUp;
                float3 centerWS = _HectonBiotaOriginWS.xyz + biota.PositionScale.xyz;
                float3 worldOffset = (rightWS * quad.x + upWS * quad.y) * biota.PositionScale.w;
                centerWS = lerp(float3(0.0, -1000000.0, 0.0), centerWS, active);

                output.positionWS = centerWS + worldOffset;
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.normalWS = SafeNormalize3(cross(rightWS, upWS), float3(0.0, 0.0, 1.0));
                output.velocityEmission = biota.VelocityEmission;
                output.visualParams = biota.VisualParams;
                output.stateFlags = biota.StateFlags;
                output.speciesBucket = biota.SpeciesBucket;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float active = input.visualParams.w;
                clip(active - 0.001);

                float2 uv = input.uv;
                float hash = input.visualParams.z;
                if (_HectonBiotaOverkill01 > 0.5)
                    uv = Parallax16(uv, hash, _HectonBiotaOverkill01);

                float2 centered = uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float shell = smoothstep(0.05, 0.82, radial);
                float edge = 1.0 - smoothstep(0.68, 1.0, radial);
                float biomeMix = Hash11(_HectonBiotaBiomeHash * 0.000001 + (float)(input.speciesBucket & 65535u) * 0.017 + hash);
                float reactive = (input.stateFlags & HECTON_BIOTA_REACTIVE) != 0u ? 1.0 : 0.0;
                half3 baseColor = lerp(_AbyssTint.rgb, _PlanktonTint.rgb, biomeMix);
                baseColor = lerp(baseColor, _PanicTint.rgb, saturate(input.velocityEmission.w + reactive * 0.45));

                float3 viewDirWS = SafeNormalize3(_WorldSpaceCameraPos.xyz - input.positionWS, float3(0.0, 0.0, 1.0));
                float rim = pow(saturate(1.0 - dot(SafeNormalize3(input.normalWS, float3(0.0, 0.0, 1.0)), viewDirWS)), _SssPower);
                float silt = TriPulse(dot(input.positionWS.xz, _HectonBiotaFlowVector.xz * 0.19 + float2(0.031, 0.047)) + _Time.y * 0.07 + hash * 9.7);
                float salt = pow(saturate(edge * TriPulse(uv.x * 11.0 + uv.y * 7.0 + hash * 19.0)), 8.0);

                half3 color = baseColor;
                color += _SubsurfaceTint.rgb * rim * _SssStrength * lerp(0.35, 1.0, _HectonBiotaOverkill01);
                color += silt * _SiltStrength * _HectonBiotaOverkill01;
                color += salt * _SaltGlintStrength * _HectonBiotaOverkill01;

                float alpha = radial * shell * _Opacity * lerp(0.62, 1.0, input.velocityEmission.w);
                alpha *= lerp(1.0, 0.55, saturate(_HectonBiotaSystemStress01));
                clip(alpha - _AlphaClip);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
