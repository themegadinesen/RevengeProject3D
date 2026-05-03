Shader "Custom/CloudDiveVolume"
{
    Properties
    {
        _TransitionProgress ("Transition Progress", Range(0, 1)) = 0
        _TransitionStrength ("Transition Strength", Range(0, 1)) = 0
        _TransitionDirection ("Transition Direction", Float) = 1
        _CloudDensity ("Cloud Density", Range(0.1, 6)) = 2.05
        _NoiseScale ("Noise Scale", Range(1, 16)) = 7.5
        _DiveSpeed ("Dive Speed", Range(0.1, 10)) = 4.2
        _CloudHighlight ("Cloud Highlight", Color) = (0.92, 0.96, 0.95, 1)
        _CloudLowlight ("Cloud Lowlight", Color) = (0.62, 0.7, 0.74, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CloudDiveVolume"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            float _TransitionProgress;
            float _TransitionStrength;
            float _TransitionDirection;
            float _CloudDensity;
            float _NoiseScale;
            float _DiveSpeed;
            half4 _CloudHighlight;
            half4 _CloudLowlight;

            static const float CloudPi = 3.14159265;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float Hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);

                float n000 = Hash31(i + float3(0, 0, 0));
                float n100 = Hash31(i + float3(1, 0, 0));
                float n010 = Hash31(i + float3(0, 1, 0));
                float n110 = Hash31(i + float3(1, 1, 0));
                float n001 = Hash31(i + float3(0, 0, 1));
                float n101 = Hash31(i + float3(1, 0, 1));
                float n011 = Hash31(i + float3(0, 1, 1));
                float n111 = Hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);
                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);
                return lerp(nxy0, nxy1, u.z);
            }

            float Fbm(float3 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += ValueNoise(p) * amplitude;
                    p = p * 2.07 + 19.19;
                    amplitude *= 0.5;
                }

                return value;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float strength = saturate(_TransitionStrength);
                if (strength <= 0.001)
                    discard;

                float3 cameraOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayEnd = input.positionOS;
                float3 ray = rayEnd - cameraOS;
                float rayLength = max(length(ray), 0.0001);
                float3 rayDirection = ray / rayLength;
                float progress = saturate(_TransitionProgress);
                float entryFade = smoothstep(0.02, 0.28, progress);
                float exitFade = 1.0 - smoothstep(0.88, 1.0, progress);
                float peak = sin(progress * CloudPi);
                float coverAmount = smoothstep(0.02, 0.96, progress);
                float densityBoost = max(peak, coverAmount * 0.85);
                float revealOpening = 1.0 - smoothstep(0.18, 0.78, strength);
                float direction = _TransitionDirection >= 0.0 ? 1.0 : -1.0;
                float3 diveOffset = float3(
                    _Time.y * 0.026,
                    -progress * _DiveSpeed * direction + _Time.y * 0.018,
                    _Time.y * 0.055
                );

                float alpha = 0.0;
                float litDensity = 0.0;
                float depthGlow = 0.0;
                const int StepCount = 28;
                float stepSize = rayLength / StepCount;

                [loop]
                for (int i = 0; i < StepCount; i++)
                {
                    float stepT = ((float)i + 0.5) / StepCount;
                    float3 samplePos = cameraOS + rayDirection * (stepT * rayLength);
                    float3 shear = float3(samplePos.x * 0.22, samplePos.z * 0.08, samplePos.y * 0.18);
                    float3 noisePos = samplePos * _NoiseScale + diveOffset + shear;
                    float baseNoise = Fbm(noisePos);
                    float detailNoise = Fbm(noisePos * 2.15 + 8.7);
                    float vaporNoise = Fbm(noisePos * 0.38 + float3(2.3, 9.1, 4.7));
                    float cloudShape = saturate(baseNoise * 0.58 + detailNoise * 0.28 + vaporNoise * 0.24);
                    float density = smoothstep(0.33, 0.78, cloudShape);

                    float radial = length(samplePos.xy);
                    float centerBody = 1.0 - smoothstep(0.38, 1.02, radial);
                    float edgeRush = smoothstep(0.2, 0.88, radial) * (1.0 - smoothstep(0.92, 1.22, radial));
                    float depthBand = smoothstep(0.04, 0.34, stepT) * (1.0 - smoothstep(0.78, 1.0, stepT));
                    float exitOpening = smoothstep(0.72, 1.0, progress) * revealOpening * (1.0 - smoothstep(0.0, 0.42, radial));
                    float diveEnvelope = saturate(centerBody * (0.88 - exitOpening * 0.52) + edgeRush * (0.25 + densityBoost * 0.58));
                    density *= diveEnvelope * depthBand * _CloudDensity * strength * (0.55 + entryFade * 0.42 + densityBoost * 1.05 + exitFade * 0.18);

                    float sampleAlpha = saturate(density * stepSize * 2.25);
                    alpha += (1.0 - alpha) * sampleAlpha;
                    litDensity += cloudShape * sampleAlpha;
                    depthGlow += depthBand * sampleAlpha;
                }

                alpha = saturate(alpha);
                float light = saturate(litDensity / max(alpha, 0.001));
                float silverLining = saturate(depthGlow / max(alpha, 0.001));
                half3 cloudColor = lerp(_CloudLowlight.rgb, _CloudHighlight.rgb, light);
                cloudColor = lerp(cloudColor, _CloudHighlight.rgb, silverLining * (0.08 + densityBoost * 0.26));
                alpha *= saturate(0.4 + entryFade * 0.72);
                alpha *= lerp(1.0, saturate(0.68 + exitFade * 0.32), revealOpening);
                return half4(cloudColor, alpha * 0.96);
            }
            ENDHLSL
        }
    }
}
