Shader "Custom/Portal"
{
    Properties
    {
        _MainColor ("Main Purple", Color) = (0.45, 0.0, 1.0, 1)
        _GlowColor ("Glow Purple", Color) = (1.0, 0.25, 1.0, 1)
        _Speed ("Swirl Speed", Float) = 1.5
        _Strength ("Swirl Strength", Float) = 6.0
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
                float _Strength;
                float _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv - 0.5;
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);

                float swirl = sin(angle * _Strength - _Time.y * _Speed + dist * 12.0);

                float rings = sin(dist * 35.0 - _Time.y * _Speed * 3.0);

                float portal = smoothstep(0.2, 1.0, swirl * 0.5 + rings * 0.5);

                float edgeFade = 1.0 - smoothstep(0.35, 0.5, dist);

                float3 color = lerp(_MainColor.rgb, _GlowColor.rgb, portal);
                color *= _GlowStrength;

                float alpha = edgeFade * 0.85;

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}
