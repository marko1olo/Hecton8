Shader "Hecton8/Environment/Hecton_VoxelBakeGhost"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.045, 0.068, 0.082, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.16, 0.38, 0.46, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 0.16, 0.22, 1)
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.42
        _InstabilityScale ("Instability Scale", Range(0.1, 8.0)) = 1.4
        _InstabilitySpeed ("Instability Speed", Range(0.0, 6.0)) = 1.25
        _InstabilityStrength ("Instability Strength", Range(0.0, 1.0)) = 0.28
        _DitherBias ("Dither Bias", Range(-0.5, 0.5)) = 0.0
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _EmissionColor;
                half _Opacity;
                half _InstabilityScale;
                half _InstabilitySpeed;
                half _InstabilityStrength;
                half _DitherBias;
                half _FresnelPower;
            CBUFFER_END

            TEXTURE3D(_HectonDamageVolumeTex);
            SAMPLER(sampler_HectonDamageVolumeTex);
            float4 _HectonDamageVolumeWorldMin;
            float4 _HectonDamageVolumeInvSize;
            float _HectonDamageVolumeActive;

            struct Attributes
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            half ResolveInterleavedGradientNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return (half)frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half FastGhostFresnel(half value, half power)
            {
                half v = saturate(value);
                half v2 = v * v;
                half v4 = v2 * v2;
                half v8 = v4 * v4;
                half lowPowerBlend = saturate(2.0h - power);
                half highPowerBlend = saturate((power - 2.0h) * 0.16666667h);
                return lerp(lerp(v2, v8, highPowerBlend), v, lowPowerBlend);
            }

            half EvaluateDearLieDamageVolume(float3 positionWS)
            {
                if (_HectonDamageVolumeActive < 0.5)
                    return 0.0h;

                float3 uvw = (positionWS - _HectonDamageVolumeWorldMin.xyz) * _HectonDamageVolumeInvSize.xyz;
                if (uvw.x < 0.0 || uvw.x > 1.0 || uvw.y < 0.0 || uvw.y > 1.0 || uvw.z < 0.0 || uvw.z > 1.0)
                    return 0.0h;

                return SAMPLE_TEXTURE3D_LOD(_HectonDamageVolumeTex, sampler_HectonDamageVolumeTex, uvw, 0).r;
            }

            void ApplyDearLieGhostClip(float3 positionWS, float2 positionCS)
            {
                half carveMask = saturate(EvaluateDearLieDamageVolume(positionWS));
                half clipStrength = saturate((carveMask - 0.45h) * 1.8181818h);
                half coverage = saturate(1.0h - clipStrength);
                clip(coverage - ResolveInterleavedGradientNoise(positionCS) * 0.125h);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ApplyDearLieGhostClip(input.positionWS, input.positionCS.xy);
                half3 normalWS = SafeNormalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                half fresnel = FastGhostFresnel(1.0h - dot(normalWS, viewDirWS), _FresnelPower);

                float instabilitySeed = Hash31(
                    input.positionWS * max((float)_InstabilityScale, 0.001) +
                    _Time.y * max((float)_InstabilitySpeed, 0.0));
                half instability = (half)instabilitySeed;
                half threshold = ResolveInterleavedGradientNoise(input.positionCS.xy);

                half alpha =
                    saturate(
                        _Opacity +
                        (instability - 0.5h) * _InstabilityStrength +
                        fresnel * 0.18h -
                        _DitherBias);

                clip(alpha - threshold);

                half edgeBlend = saturate(fresnel * 0.82h + instability * 0.28h);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeBlend);
                color += _EmissionColor.rgb * (0.35h + fresnel * 0.65h);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
