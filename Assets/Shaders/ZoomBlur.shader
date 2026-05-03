Shader "Custom/TransitionSoftBlur"
{
    Properties
    {
        _ZoomBlurStrength ("Mist Strength", Range(0, 1)) = 0
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 0
        _TransitionDirection ("Transition Direction", Float) = 1
        _CloudScale ("Mist Scale", Range(1, 12)) = 4.5
        _CloudSoftness ("Mist Softness", Range(0.02, 0.6)) = 0.22
        _CloudWarp ("Mist Warp", Range(0, 0.3)) = 0.08
        _CloudTint ("Mist Highlight", Color) = (0.9, 0.94, 0.92, 1)
        _CloudShadowTint ("Mist Lowlight", Color) = (0.68, 0.74, 0.76, 1)
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
            Name "TransitionSoftBlur"
            ZWrite Off
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _ZoomBlurStrength;
            float _TransitionProgress;
            float _TransitionDirection;
            float _CloudScale;
            float _CloudSoftness;
            float _CloudWarp;
            half4 _CloudTint;
            half4 _CloudShadowTint;

            static const float CloudPi = 3.14159265;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.13;
                    amplitude *= 0.5;
                }

                return value;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Cheap passthrough while the fullscreen renderer feature is idle.
                float strength = saturate(_ZoomBlurStrength);
                float progress = saturate(_TransitionProgress);
                if (strength < 0.001 || progress <= 0.001)
                    return sceneColor;

                float transitionPeak = sin(progress * CloudPi);
                float coverAmount = smoothstep(0.02, 0.96, progress);
                float veil = saturate(coverAmount * strength);
                float blurRadius = lerp(1.0, 10.0, saturate(max(transitionPeak, coverAmount * 0.72))) * strength;
                float2 texel = blurRadius / max(_ScreenParams.xy, float2(1.0, 1.0));

                half3 blurredScene = sceneColor.rgb * 0.24;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1.0,  0.0)).rgb * 0.12;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0,  0.0)).rgb * 0.12;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 0.0,  1.0)).rgb * 0.12;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 0.0, -1.0)).rgb * 0.12;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1.0,  1.0)).rgb * 0.07;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0,  1.0)).rgb * 0.07;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2( 1.0, -1.0)).rgb * 0.07;
                blurredScene += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + texel * float2(-1.0, -1.0)).rgb * 0.07;

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float direction = _TransitionDirection >= 0.0 ? 1.0 : -1.0;
                float2 cloudUv = (uv - 0.5) * float2(aspect, 1.0) * _CloudScale;
                float2 drift = _Time.y * float2(0.038, 0.017) * direction;
                float2 warp = float2(
                    Fbm(cloudUv * 0.48 + drift + 11.4),
                    Fbm(cloudUv * 0.48 - drift + 43.9)
                ) - 0.5;
                float cloudNoise = Fbm(cloudUv + warp * (_CloudWarp * 8.0) + drift * 2.0);
                float cloudDetail = Fbm(cloudUv * 2.1 - drift + 6.7);
                float cloudBody = saturate(cloudNoise * 0.72 + cloudDetail * 0.28);
                float cloudMask = smoothstep(0.5 - _CloudSoftness, 0.5 + _CloudSoftness, cloudBody);
                cloudMask = lerp(cloudMask, 1.0, smoothstep(0.72, 1.0, coverAmount) * 0.94);

                half3 hazeColor = lerp(_CloudShadowTint.rgb, _CloudTint.rgb, saturate(cloudBody * 0.75 + 0.2));
                float hazeOpacity = saturate(veil * lerp(0.16, 1.0, coverAmount) * lerp(0.78, 1.08, cloudMask));
                half3 softenedScene = lerp(sceneColor.rgb, blurredScene, saturate((transitionPeak * 0.45 + coverAmount * 0.72) * strength));

                return half4(lerp(softenedScene, hazeColor, hazeOpacity), sceneColor.a);
            }
            ENDHLSL
        }
    }
}
