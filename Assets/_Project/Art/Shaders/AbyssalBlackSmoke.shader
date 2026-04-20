Shader "Hecton8/VFX/AbyssalBlackSmoke"
{
    Properties
    {
        _AshTint ("Ash Tint", Color) = (0.08, 0.08, 0.08, 0.28)
        _AshHotTint ("Ash Hot Tint", Color) = (0.22, 0.17, 0.12, 0.34)
        _Softness ("Softness", Range(0.5, 4.0)) = 2.2
        _MaxViewDistance ("Max View Distance", Float) = 95
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AshParticleData
            {
                float3 PositionWS;
                float Size;
                float3 VelocityWS;
                float Alpha;
                float Lifetime;
                float MaxLifetime;
                float Seed;
                float VentIndex;
            };

            StructuredBuffer<AshParticleData> _AshParticles;

            float3 _CameraPositionWS;
            float3 _CameraRightWS;
            float3 _CameraUpWS;
            float4 _AshTint;
            float4 _AshHotTint;
            float _Softness;
            float _MaxViewDistance;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            float2 ResolveQuadCorner(uint vertexID)
            {
                if (vertexID == 0) return float2(-1.0, -1.0);
                if (vertexID == 1) return float2(-1.0,  1.0);
                if (vertexID == 2) return float2( 1.0,  1.0);
                if (vertexID == 3) return float2(-1.0, -1.0);
                if (vertexID == 4) return float2( 1.0,  1.0);
                return float2(1.0, -1.0);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                AshParticleData particle = _AshParticles[input.instanceID];
                float2 corner = ResolveQuadCorner(input.vertexID);
                float distanceFade = saturate(1.0 - distance(particle.PositionWS, _CameraPositionWS) / max(_MaxViewDistance, 0.001));
                float size = particle.Size * lerp(0.55, 1.0, distanceFade);
                float3 billboardOffset = (_CameraRightWS * corner.x + _CameraUpWS * corner.y) * size;
                float3 worldPosition = particle.PositionWS + billboardOffset;

                output.positionCS = TransformWorldToHClip(worldPosition);
                output.uv = corner * 0.5 + 0.5;

                float heatT = saturate(particle.Lifetime / max(particle.MaxLifetime, 0.001));
                output.color = lerp(_AshHotTint, _AshTint, heatT);
                output.color.a *= particle.Alpha;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centered = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centered, centered));
                float alpha = pow(radial, _Softness) * input.color.a;
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
