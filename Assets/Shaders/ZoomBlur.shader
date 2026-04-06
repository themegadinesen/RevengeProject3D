Shader "Custom/ZoomBlur"
{
    Properties
    {
        _ZoomBlurStrength ("Blur Strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ZoomBlur"
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ZoomBlurStrength;

            #define SAMPLES 10

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // No blur — cheap passthrough (runs every frame when not transitioning).
                if (_ZoomBlurStrength < 0.001)
                    return SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Direction from screen center to this pixel.
                float2 dir = uv - 0.5;

                half4 color = 0;
                for (int i = 0; i < SAMPLES; i++)
                {
                    float t = (float)i / (float)(SAMPLES - 1);
                    // Push samples outward from pixel — radial "zoom rush" streak.
                    float2 sampleUV = uv + dir * t * _ZoomBlurStrength;
                    color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, sampleUV);
                }

                return color * (1.0 / SAMPLES);
            }
            ENDHLSL
        }
    }
}