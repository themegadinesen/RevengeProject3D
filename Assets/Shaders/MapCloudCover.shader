Shader "Custom/ProceduralMapCloudCover"
{
    Properties
    {
        _Opacity ("Opacity", Range(0, 1)) = 0.32
        _Coverage ("Coverage", Range(0, 1)) = 0.48
        _Softness ("Softness", Range(0.01, 0.5)) = 0.22
        _NoiseScale ("Noise Scale", Range(0.2, 12)) = 2.8
        _DetailScale ("Detail Scale", Range(0.5, 8)) = 3.2
        _Drift ("Drift", Vector) = (0.018, 0.009, 0, 0)
        _CloudTint ("Cloud Tint", Color) = (0.92, 0.96, 0.95, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.58, 0.66, 0.72, 1)
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
            Name "ProceduralMapCloudCover"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            float _Opacity;
            float _Coverage;
            float _Softness;
            float _NoiseScale;
            float _DetailScale;
            float4 _Drift;
            half4 _CloudTint;
            half4 _ShadowTint;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.vertex.xyz);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.vertex.xyz);
                return output;
            }

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
                    p = p * 2.04 + float2(17.17, 31.31);
                    amplitude *= 0.5;
                }

                return value;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv - 0.5;
                float2 wind = _Time.y * _Drift.xy;
                float worldNoiseScale = max(_NoiseScale, 0.001) * 0.035;
                float2 baseUv = input.positionWS.xy * worldNoiseScale + wind;

                float2 warp = float2(
                    Fbm(baseUv * 0.55 + 12.7),
                    Fbm(baseUv * 0.55 + 51.3)
                ) - 0.5;

                float broad = Fbm(baseUv + warp * 0.95);
                float detail = Fbm(baseUv * _DetailScale + warp * 2.1 + 8.4);
                float wisps = Fbm(baseUv * (_DetailScale * 2.4) - warp * 1.3 + 19.1);
                float cloud = saturate(broad * 0.7 + detail * 0.24 + wisps * 0.1);

                float threshold = lerp(0.78, 0.34, saturate(_Coverage));
                float mask = smoothstep(threshold, threshold + _Softness, cloud);
                float feather = 1.0 - smoothstep(0.62, 0.92, length(centeredUv));
                mask *= saturate(feather * 1.25);

                float light = saturate(detail * 0.75 + broad * 0.45);
                half3 color = lerp(_ShadowTint.rgb, _CloudTint.rgb, light);
                color = lerp(color, _CloudTint.rgb, smoothstep(0.62, 0.95, cloud) * 0.25);

                return half4(color, mask * _Opacity);
            }
            ENDHLSL
        }
    }
}
