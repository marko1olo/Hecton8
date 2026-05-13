Shader "HECTON/HUD/RadarBlipInstanced"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1.0, 0.24, 0.28, 0.92)
        _FlickerFrequency ("Flicker Frequency", Float) = 18
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.4)) = 0.18
        _EdgeWidth ("Edge Width", Range(0.01, 0.3)) = 0.08
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.36
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+20"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "HUDForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _FlickerFrequency;
                float _FlickerIntensity;
                float _EdgeWidth;
                float _FillAlpha;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstance)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceData)
            UNITY_INSTANCING_BUFFER_END(PerInstance)

            struct HectonRadarBlipGpuData
            {
                float4 LocalPositionSize;
                float4 ColorAlpha;
            };

            StructuredBuffer<HectonRadarBlipGpuData> _HectonRadarBlips;
            StructuredBuffer<float4> _HectonGroundRadarPings;
            float4x4 _HectonRadarLocalToWorld;
            float4 _HectonRadarGprOriginRadius;
            float _HectonRadarProcedural;
            float _HectonRadarGprProcedural;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceId : SV_InstanceID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float instanceAlpha : TEXCOORD1;
                float3 instanceColor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float HectonFastTriangleSine(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
            }

            float3 HectonSafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 0.000001 ? value * rsqrt(lengthSq) : fallback;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = input.uv;
                output.instanceColor = 1.0.xxx;

                if (_HectonRadarProcedural > 0.5)
                {
                    if (_HectonRadarGprProcedural > 0.5)
                    {
                        float4 ping = _HectonGroundRadarPings[input.instanceId];
                        float strength = saturate(ping.w);
                        float radius = max(1.0, _HectonRadarGprOriginRadius.w);
                        float invRadius = rcp(radius);
                        float3 delta = ping.xyz - _HectonRadarGprOriginRadius.xyz;
                        float3 localCenter = float3(delta.x * invRadius * 0.42, delta.z * invRadius * 0.42, 0.0);
                        float3 worldCenter = mul(_HectonRadarLocalToWorld, float4(localCenter, 1.0)).xyz;
                        float3 cameraRight = HectonSafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0, 0.0, 0.0));
                        float3 cameraUp = HectonSafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0, 1.0, 0.0));
                        float gprSize = lerp(0.018, 0.055, strength);
                        float3 worldPosition = worldCenter + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * gprSize;
                        output.positionCS = TransformWorldToHClip(worldPosition);
                        output.instanceAlpha = strength;
                        output.instanceColor = lerp(float3(0.02, 0.1, 0.85), float3(0.08, 1.0, 0.28), strength);
                        return output;
                    }

                    HectonRadarBlipGpuData blip = _HectonRadarBlips[input.instanceId];
                    float3 worldCenter = mul(_HectonRadarLocalToWorld, float4(blip.LocalPositionSize.xyz, 1.0)).xyz;
                    float3 cameraRight = HectonSafeNormalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m20), float3(1.0, 0.0, 0.0));
                    float3 cameraUp = HectonSafeNormalize(float3(UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m21), float3(0.0, 1.0, 0.0));
                    float3 worldPosition = worldCenter + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * blip.LocalPositionSize.w;
                    output.positionCS = TransformWorldToHClip(worldPosition);
                    output.instanceAlpha = saturate(blip.ColorAlpha.a);
                    output.instanceColor = saturate(blip.ColorAlpha.rgb);
                    return output;
                }

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(PerInstance, _InstanceData);
                output.instanceAlpha = lerp(1.0, saturate(instanceData.x), saturate(instanceData.y));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float instanceAlpha = saturate(input.instanceAlpha);
                float2 centered = input.uv * 2.0 - 1.0;
                float diamond = abs(centered.x) + abs(centered.y);
                float border = 1.0 - smoothstep(1.0 - _EdgeWidth, 1.0, abs(diamond - 1.0));
                float fill = smoothstep(1.0, 0.82, diamond) * _FillAlpha * instanceAlpha;
                float flicker = 1.0 - _FlickerIntensity + _FlickerIntensity * HectonFastTriangleSine(_Time.y * _FlickerFrequency * 6.2831853);
                float alpha = saturate((border + fill) * _BaseColor.a * flicker * instanceAlpha);
                return half4(_BaseColor.rgb * input.instanceColor * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
