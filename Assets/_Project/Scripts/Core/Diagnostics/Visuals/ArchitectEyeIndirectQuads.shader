Shader "Hidden/Hecton8/Diagnostics/ArchitectEyeIndirectQuads"
{
    Properties
    {
        _H8EyeGlyphAtlas ("Glyph Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct ArchitectEyeQuadInstance
            {
                float4 CenterHalfX;
                float4 AxisYHalfY;
                float4 Color;
                float4 UvMode;
                float4 Aux;
            };

            StructuredBuffer<ArchitectEyeQuadInstance> _H8EyeQuads;
            sampler2D _H8EyeGlyphAtlas;

            struct appdata
            {
                float3 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                ArchitectEyeQuadInstance q = _H8EyeQuads[instanceID];
                float mode = q.UvMode.w;
                float2 uvMin = q.UvMode.xy;
                float2 uvMax = float2(q.UvMode.z, q.Aux.w);

                float4 positionCS;
                if (mode > 0.5f && mode < 1.5f)
                {
                    float2 halfSize = float2(q.CenterHalfX.w, q.AxisYHalfY.w);
                    float2 positionXY = q.CenterHalfX.xy + v.vertex.xy * halfSize;
                    positionCS = float4(positionXY, q.CenterHalfX.z, 1.0f);
                }
                else
                {
                    float3 axisX;
                    float3 axisY;
                    if (mode > 1.5f)
                    {
                        axisX = q.Aux.xyz;
                        axisY = q.AxisYHalfY.xyz;
                    }
                    else
                    {
                        axisX = normalize(float3(UNITY_MATRIX_I_V._m00, UNITY_MATRIX_I_V._m01, UNITY_MATRIX_I_V._m02));
                        axisY = normalize(float3(UNITY_MATRIX_I_V._m10, UNITY_MATRIX_I_V._m11, UNITY_MATRIX_I_V._m12));
                    }

                    float3 positionWS =
                        q.CenterHalfX.xyz +
                        axisX * (v.vertex.x * q.CenterHalfX.w) +
                        axisY * (v.vertex.y * q.AxisYHalfY.w);
                    positionCS = UnityWorldToClipPos(positionWS);
                }

                v2f o;
                o.positionCS = positionCS;
                o.uv = lerp(uvMin, uvMax, v.uv);
                o.color = q.Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half glyph = tex2D(_H8EyeGlyphAtlas, i.uv).a;
                half alpha = i.color.a * max(glyph, 0.35h);
                return half4(i.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
