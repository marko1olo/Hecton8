Shader "Hecton8/VFX/ParasiteSwarmUnlit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.11, 0.75, 0.54, 1)
        _HotColor ("Hot Color", Color) = (1.0, 0.18, 0.05, 1)
        _ParticleSize ("Particle Size", Float) = 0.045
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct ParasiteGpuParticleDTO
            {
                float3 Position;
                float Life01;
                float3 Velocity;
                uint Flags;
            };

            StructuredBuffer<ParasiteGpuParticleDTO> _H8ParasiteReadA;
            StructuredBuffer<ParasiteGpuParticleDTO> _H8ParasiteReadB;
            StructuredBuffer<uint> _H8ParasiteVisibleIndices;
            StructuredBuffer<float4> _H8ParasiteDrawParams;
            float4 _BaseColor;
            float4 _HotColor;
            float _ParticleSize;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };

            float2 QuadCorner(uint vertexId)
            {
                uint v = vertexId % 6u;
                if (v == 0u) return float2(-1.0, -1.0);
                if (v == 1u) return float2(1.0, -1.0);
                if (v == 2u) return float2(1.0, 1.0);
                if (v == 3u) return float2(-1.0, -1.0);
                if (v == 4u) return float2(1.0, 1.0);
                return float2(-1.0, 1.0);
            }

            v2f vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                uint particleIndex = _H8ParasiteVisibleIndices[instanceId];
                float4 drawParams = _H8ParasiteDrawParams[0];
                ParasiteGpuParticleDTO particle;
                if (drawParams.w > 0.5)
                    particle = _H8ParasiteReadB[particleIndex];
                else
                    particle = _H8ParasiteReadA[particleIndex];

                float2 corner = QuadCorner(vertexId);
                float3 right = float3(UNITY_MATRIX_I_V[0][0], UNITY_MATRIX_I_V[1][0], UNITY_MATRIX_I_V[2][0]);
                float3 up = float3(UNITY_MATRIX_I_V[0][1], UNITY_MATRIX_I_V[1][1], UNITY_MATRIX_I_V[2][1]);
                float speed01 = saturate(length(particle.Velocity) * 0.08);
                float latch01 = (particle.Flags & 2u) != 0u ? 1.0 : 0.0;
                float size = max(_ParticleSize, 0.002) * lerp(0.65, 1.45, speed01);
                float3 world = drawParams.xyz + particle.Position + (right * corner.x + up * corner.y) * size;

                v2f output;
                output.pos = UnityWorldToClipPos(world);
                float heat = saturate(speed01 + latch01 * 0.6);
                output.color = lerp(_BaseColor, _HotColor, heat);
                output.color.a *= saturate(particle.Life01);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                return input.color;
            }
            ENDCG
        }
    }
}
