Shader "Custom/PortalClip"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _ClipY ("Clip Y", Float) = 0.0
        _Clipping ("Clipping", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _ClipY;
                float _Clipping;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_Clipping > 0.5 && IN.worldPos.y < _ClipY)
                    discard;

                // basic lambert lighting
                Light mainLight = GetMainLight();
                float3 normal = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
                float lighting = saturate(dot(normal, mainLight.direction)) * 0.8 + 0.2;

                return half4(_BaseColor.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}