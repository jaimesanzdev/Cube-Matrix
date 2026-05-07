Shader "Custom/Portal"
{
    Properties
    {
        _MainColor ("Main Purple", Color) = (0.45, 0.0, 1.0, 1)
        _GlowColor ("Glow Purple", Color) = (1.0, 0.25, 1.0, 1)
        _Speed ("Rotation Speed", Float) = 1.5
        _Twist ("Twist Amount", Float) = 5.0
        _RingScale ("Ring Scale", Float) = 18.0
        _GlowStrength ("Glow Strength", Float) = 2.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float4 _GlowColor;
                float _Speed;
                float _Twist;
                float _RingScale;
                float _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float2 Rotate(float2 p, float a)
            {
                float s = sin(a);
                float c = cos(a);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv - 0.5;
                float dist = length(uv);

                // Constant one-direction rotation
                float angle = _Time.y * _Speed;

                // More twist near the center
                float twistAmount = (1.0 - dist) * _Twist;
                float2 spunUV = Rotate(uv, angle + twistAmount);

                // Continuous flowing spiral pattern
                float spiral = frac(
                    atan2(spunUV.y, spunUV.x) / 6.28318 +
                    dist * _RingScale -
                    _Time.y * _Speed
                );

                float bands = smoothstep(0.25, 0.5, spiral) * 
                              (1.0 - smoothstep(0.5, 0.85, spiral));

                float centerGlow = 1.0 - smoothstep(0.0, 0.45, dist);
                float edgeFade = 1.0 - smoothstep(0.38, 0.5, dist);

                float portal = saturate(bands + centerGlow * 0.7);

                float3 color = lerp(_MainColor.rgb, _GlowColor.rgb, portal);
                color *= _GlowStrength;

                float alpha = edgeFade * 0.9;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}
