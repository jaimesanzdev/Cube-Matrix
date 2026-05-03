Shader "Custom/MenuScreenPanel_URP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.55, 0.65, 0.95, 1)
        _GridColor ("Grid Color", Color) = (0.75, 0.85, 1.0, 1)
        _ScanColor ("Scan Color", Color) = (0.95, 0.98, 1.0, 1)
        _EdgeColor ("Edge Color", Color) = (0.18, 0.22, 0.35, 1)

        _Brightness ("Brightness", Range(0, 3)) = 1.0
        _GridScale ("Grid Scale", Range(4, 200)) = 28
        _GridThickness ("Grid Thickness", Range(0.001, 0.2)) = 0.045

        _ScanPosition ("Scan Position", Range(0, 1)) = 0
        _ScanWidth ("Scan Width", Range(0.001, 0.5)) = 0.08
        _ScanIntensity ("Scan Intensity", Range(0, 2)) = 0.25

        _EdgeDarkness ("Edge Darkness", Range(0, 2)) = 0.55
        _VignetteStrength ("Vignette Strength", Range(0, 2)) = 0.35

        _FlickerStrength ("Flicker Strength", Range(0, 0.2)) = 0.025
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.4
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

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
                float4 _BaseColor;
                float4 _GridColor;
                float4 _ScanColor;
                float4 _EdgeColor;

                float _Brightness;
                float _GridScale;
                float _GridThickness;

                float _ScanPosition;
                float _ScanWidth;
                float _ScanIntensity;

                float _EdgeDarkness;
                float _VignetteStrength;

                float _FlickerStrength;
                float _PulseSpeed;
                float _PulseAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float t = _Time.y;

                float3 col = _BaseColor.rgb;

                float2 gridUV = uv * _GridScale;
                float2 cell = abs(frac(gridUV) - 0.5);
                float gridLineMask = 1.0 - smoothstep(0.5 - _GridThickness, 0.5, max(cell.x, cell.y));
                col = lerp(col, _GridColor.rgb, gridLineMask * 0.35);

                float scanMask = 1.0 - smoothstep(0.0, _ScanWidth, abs(uv.y - _ScanPosition));
                col += _ScanColor.rgb * (scanMask * _ScanIntensity);

                float pulse = 1.0 + sin(t * _PulseSpeed) * _PulseAmount;

                float flicker = (hash21(float2(floor(t * 24.0), 17.0)) - 0.5) * 2.0;
                float flickerMul = 1.0 + flicker * _FlickerStrength;

                float2 centered = uv * 2.0 - 1.0;
                float edge = saturate(max(abs(centered.x), abs(centered.y)));
                float edgeMask = smoothstep(0.55, 1.0, edge);
                col = lerp(col, _EdgeColor.rgb, edgeMask * _EdgeDarkness);

                float vignette = 1.0 - dot(centered, centered) * 0.25;
                vignette = lerp(1.0 - _VignetteStrength, 1.0, saturate(vignette));

                col *= _Brightness * pulse * flickerMul * vignette;

                return half4(saturate(col), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}