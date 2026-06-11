Shader "Hecton8/Construction/DroneFleetProcedural"
{
    Properties
    {
        _HullTint ("Hull Tint", Color) = (0.07, 0.20, 0.24, 1)
        _BeaconTint ("Beacon Tint", Color) = (0.10, 0.88, 0.82, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float4x4> _DroneMatrices;
            StructuredBuffer<float4x4> _InstanceMatrices;
            StructuredBuffer<float4> _PhantomColors;

            CBUFFER_START(UnityPerMaterial)
                float4 _HullTint;
                float4 _BeaconTint;
                float4 _DroneCameraOriginWS;
                int _UsePhantomColors;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float active : TEXCOORD1;
                float beacon : TEXCOORD2;
                float4 instanceColor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SafeNormalize3(float3 value, float3 fallback)
            {
                float lengthSq = dot(value, value);
                return lengthSq > 1e-6 ? value * rsqrt(lengthSq) : fallback;
            }

            float3 ResolveCubeVertex(uint id)
            {
                uint faceVertex = id % 6u;
                uint face = (id / 6u) % 6u;
                float2 uv;
                uv.x = (faceVertex == 1u || faceVertex == 2u || faceVertex == 5u) ? 1.0 : -1.0;
                uv.y = (faceVertex == 2u || faceVertex == 4u || faceVertex == 5u) ? 1.0 : -1.0;

                float3 p;
                if (face == 0u) p = float3(uv.x, uv.y, 1.35);
                else if (face == 1u) p = float3(-uv.x, uv.y, -1.35);
                else if (face == 2u) p = float3(1.0, uv.y, -uv.x * 1.35);
                else if (face == 3u) p = float3(-1.0, uv.y, uv.x * 1.35);
                else if (face == 4u) p = float3(uv.x, 0.62, -uv.y * 1.35);
                else p = float3(uv.x, -0.62, uv.y * 1.35);
                p.x *= 0.42;
                p.y *= 0.22;
                p.z *= 0.48;
                return p;
            }

            float3 ResolveCubeNormal(uint id)
            {
                uint face = (id / 6u) % 6u;
                if (face == 0u) return float3(0.0, 0.0, 1.0);
                if (face == 1u) return float3(0.0, 0.0, -1.0);
                if (face == 2u) return float3(1.0, 0.0, 0.0);
                if (face == 3u) return float3(-1.0, 0.0, 0.0);
                if (face == 4u) return float3(0.0, 1.0, 0.0);
                return float3(0.0, -1.0, 0.0);
            }

            UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_INSTANCING_BUFFER_END(Props)
            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint safeVertex = vertexID % 36u;
                float4x4 instanceMatrix = _DroneMatrices[instanceID];
                float activity = abs(instanceMatrix._m00) + abs(instanceMatrix._m11) + abs(instanceMatrix._m22) + abs(instanceMatrix._m33);
                float active = step(0.0001, activity);
                float3 localPosition = ResolveCubeVertex(safeVertex);
                float3 localNormal = ResolveCubeNormal(safeVertex);
                float4 relativeWS = mul(instanceMatrix, float4(localPosition, 1.0));
                float3 worldPosition = relativeWS.xyz + _DroneCameraOriginWS.xyz;
                float3 normalWS = SafeNormalize3(mul((float3x3)instanceMatrix, localNormal), float3(0.0, 1.0, 0.0));

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = normalWS;
                output.active = active;
                output.beacon = frac((float)instanceID * 0.61803398875);
                output.instanceColor = _UsePhantomColors != 0 ? _PhantomColors[instanceID] : float4(1.0, 1.0, 1.0, 1.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                clip(input.active - 0.5);
                float3 lightDirection = SafeNormalize3(float3(0.24, 0.86, 0.44), float3(0.0, 1.0, 0.0));
                float ndotl = saturate(dot(SafeNormalize3(input.normalWS, float3(0.0, 1.0, 0.0)), lightDirection));
                float beacon = smoothstep(0.78, 1.0, input.beacon);
                float3 color = lerp(_HullTint.rgb, _BeaconTint.rgb, beacon) * lerp(0.36, 1.0, ndotl);
                color *= input.instanceColor.rgb;
                return half4(color, saturate(input.instanceColor.a));
            }
            ENDHLSL
        }
    }
}
