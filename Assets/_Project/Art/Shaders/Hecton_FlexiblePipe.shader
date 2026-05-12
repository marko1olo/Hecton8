Shader "Hecton/FlexiblePipe"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.30, 0.82, 0.95, 0.88)
        _Color ("Color", Color) = (0.30, 0.82, 0.95, 0.88)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.22
        _Metallic ("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Pass
        {
            Name "FlexiblePipeForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend Off
            ZWrite On
            AlphaToMask On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct FlexiblePipeInstance
            {
                float4 P0Radius;
                float4 P1Flags;
                float4 P2;
                float4 P3;
            };

            StructuredBuffer<FlexiblePipeInstance> _HectonFlexiblePipeInstances;
            float _HectonLogisticsPathHighlight;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Color;
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR0;
                half3 normalWS : TEXCOORD0;
                half rupture01 : TEXCOORD1;
                half pipeT : TEXCOORD2;
                half rust01 : TEXCOORD3;
                half flow01 : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeNormalize(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

            half FastTriangleSine01(half phase)
            {
                return 1.0h - abs(frac(phase * 0.15915494h + 0.25h) * 2.0h - 1.0h);
            }

            float HectonDitherCoverage(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float FastTriangleSine(float phase)
            {
                return (1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0)) * 2.0 - 1.0;
            }

            float3 EvaluateBezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float omt = 1.0 - t;
                float omt2 = omt * omt;
                float omt3 = omt2 * omt;
                float t2 = t * t;
                float t3 = t2 * t;
                return omt3 * p0 + 3.0 * omt2 * t * p1 + 3.0 * omt * t2 * p2 + t3 * p3;
            }

            float3 EvaluateBezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
            {
                float omt = 1.0 - t;
                return 3.0 * omt * omt * (p1 - p0) +
                       6.0 * omt * t * (p2 - p1) +
                       3.0 * t * t * (p3 - p2);
            }

            void ResolveFrame(float3 tangent, out float3 normal, out float3 binormal)
            {
                float3 referenceUp = abs(tangent.y) > 0.98 ? float3(1.0, 0.0, 0.0) : float3(0.0, 1.0, 0.0);
                normal = SafeNormalize(referenceUp - tangent * dot(referenceUp, tangent), float3(0.0, 0.0, 1.0));
                binormal = SafeNormalize(cross(tangent, normal), float3(1.0, 0.0, 0.0));
                normal = SafeNormalize(cross(binormal, tangent), float3(0.0, 1.0, 0.0));
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
                FlexiblePipeInstance instanceData = _HectonFlexiblePipeInstances[instanceID];
                float3 p0 = instanceData.P0Radius.xyz;
                float3 p1 = instanceData.P1Flags.xyz;
                float3 p2 = instanceData.P2.xyz;
                float3 p3 = instanceData.P3.xyz;
                float radius = max(instanceData.P0Radius.w, 0.001);
                uint flags = (uint)round(instanceData.P1Flags.w);
                float ruptureStartTime = instanceData.P2.w;
                float flowEligible = instanceData.P3.w;

                float t = saturate(input.positionOS.y * 0.5 + 0.5);
                float3 center = EvaluateBezier(p0, p1, p2, p3, t);
                float3 tangent = SafeNormalize(EvaluateBezierTangent(p0, p1, p2, p3, t), float3(0.0, 0.0, 1.0));
                float3 normal;
                float3 binormal;
                ResolveFrame(tangent, normal, binormal);

                float2 radialSource = input.positionOS.xz;
                float radialSourceLengthSq = dot(radialSource, radialSource);
                float radialSourceInvLength = rsqrt(max(radialSourceLengthSq, 1e-6));
                float radialSourceLength = radialSourceLengthSq * radialSourceInvLength;
                float radialScale = saturate(radialSourceLength * 2.0);
                float2 radialLocal = radialSourceLengthSq > 1e-6 ? radialSource * radialSourceInvLength : float2(1.0, 0.0);
                float3 radialDirection = SafeNormalize(normal * radialLocal.x + binormal * radialLocal.y, normal);
                float3 positionWS = center + radialDirection * radius * radialScale;
                if ((flags & 1u) != 0u)
                    positionWS += radialDirection * (FastTriangleSine(positionWS.z * 15.0) * 0.15);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = (half3)radialDirection;
                float ruptured01 = ((flags & 1u) != 0u) ? 1.0 : 0.0;
                output.color = (half4)_BaseColor;
                output.rupture01 = (half)ruptured01;
                output.pipeT = (half)t;
                output.rust01 = (half)(ruptured01 > 0.5 && ruptureStartTime > 0.0
                    ? saturate((_Time.y - ruptureStartTime) * (1.0 / 300.0))
                    : 0.0);
                output.flow01 = (half)(saturate(flowEligible * _HectonLogisticsPathHighlight) * (1.0 - ruptured01));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half normalLenSq = max(dot(input.normalWS, input.normalWS), 0.0001h);
                half3 normalWS = input.normalWS * rsqrt(normalLenSq);
                half3 lightDir = half3(0.3043h, 0.8519h, 0.4260h);
                half diffuse = saturate(dot(normalWS, lightDir)) * 0.55h + 0.45h;
                half3 color = input.color.rgb * diffuse;
                half ruptureStripe = FastTriangleSine01(input.pipeT * 80.0h) * input.rupture01;
                color = lerp(color, color * half3(1.35h, 0.78h, 0.58h), ruptureStripe * 0.22h);
                color = lerp(color, half3(0.88h, 0.31h, 0.07h) * diffuse, input.rust01 * 0.72h);
                half flowPulse = FastTriangleSine01((input.pipeT * 18.0h - _Time.y * 2.4h) * 6.28318h);
                flowPulse = smoothstep(0.62h, 1.0h, flowPulse) * input.flow01;
                color += half3(0.0h, 0.52h, 0.82h) * flowPulse * 0.75h;
                clip(input.color.a - (half)HectonDitherCoverage(input.positionCS.xy));
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
