Shader "EntropyTag/TerritoryOverlay"
{
    Properties
    {
        _BaseMap("Territory Mask", 2D) = "white" {}
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
            Name "TerritoryOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(color.a - 0.05h);

                float2 patternUv = input.uv * 32.0;
                half patternMask;

                if (color.a < 0.38h)
                {
                    patternMask = step(0.72, frac((patternUv.x + patternUv.y) * 0.5));
                }
                else if (color.a < 0.63h)
                {
                    float2 dotUv = frac(patternUv * 0.5) - 0.5;
                    patternMask = step(length(dotUv), 0.16);
                }
                else
                {
                    half diagonalA = step(0.78, frac((patternUv.x + patternUv.y) * 0.45));
                    half diagonalB = step(0.78, frac((patternUv.x - patternUv.y) * 0.45));
                    patternMask = saturate(diagonalA + diagonalB);
                }

                color.rgb = lerp(color.rgb, color.rgb * 0.48h, patternMask);
                color.a = 1.0h;
                return color;
            }
            ENDHLSL
        }
    }
}
