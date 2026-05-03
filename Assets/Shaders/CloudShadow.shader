Shader "Custom/CloudShadow"
{
    Properties
    {
        [MainTexture] _MainTex ("Cloud Shadow Texture", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.22
        _ShadowColor ("Shadow Color", Color) = (0.52, 0.56, 0.58, 1)
        _ScrollSpeed ("Scroll Speed", Float) = 0.025
        _ScrollDirection ("Scroll Direction", Vector) = (1, 0.35, 0, 0)
        _Tiling ("Tiling", Vector) = (2, 2, 0, 0)
        _UVOffset ("UV Offset", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+25"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudShadow"
            Tags { "LightMode" = "UniversalForward" }

            // Multiplicative blending: white leaves the scene unchanged, darker values tint it like a shadow.
            Blend DstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Opacity;
                half4 _ShadowColor;
                half _ScrollSpeed;
                float4 _ScrollDirection;
                float4 _Tiling;
                float4 _UVOffset;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 direction = _ScrollDirection.xy;
                float directionLength = max(length(direction), 0.0001);
                direction /= directionLength;

                float2 shadowUv = input.uv * max(_Tiling.xy, float2(0.001, 0.001));
                shadowUv += _UVOffset.xy + direction * (_ScrollSpeed * _Time.y);

                half4 shadowSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUv);
                half luminanceMask = dot(shadowSample.rgb, half3(0.299, 0.587, 0.114));
                half textureMask = shadowSample.a < 0.999 ? shadowSample.a : luminanceMask;
                textureMask = saturate(textureMask);
                half shadowMask = saturate(textureMask * _Opacity * _ShadowColor.a);

                half3 multiplier = lerp(half3(1.0, 1.0, 1.0), saturate(_ShadowColor.rgb), shadowMask);
                return half4(multiplier, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DecalScreenSpaceProjector"
            Tags { "LightMode" = "DecalScreenSpaceProjector" }

            // URP DecalProjector-compatible pass. This uses normal alpha blending because decals
            // are composited by the decal renderer rather than drawn as a simple overlay mesh.
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Front

            HLSLPROGRAM
            #pragma target 2.5
            #pragma vertex DecalVert
            #pragma fragment DecalFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct DecalAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DecalVaryings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Opacity;
                half4 _ShadowColor;
                half _ScrollSpeed;
                float4 _ScrollDirection;
                float4 _Tiling;
                float4 _UVOffset;
            CBUFFER_END

            DecalVaryings DecalVert(DecalAttributes input)
            {
                DecalVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DecalFrag(DecalVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 positionCS = input.positionHCS.xy;

#if UNITY_REVERSED_Z
                float depth = LoadSceneDepth(positionCS);
#else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, LoadSceneDepth(positionCS));
#endif

                float2 positionSS = positionCS * _ScreenSize.zw;
                float3 positionWS = ComputeWorldSpacePosition(positionSS, depth, UNITY_MATRIX_I_VP);
                float3 positionDS = TransformWorldToObject(positionWS);
                positionDS *= float3(1.0, -1.0, 1.0);

                float maxDecalAxis = max(max(abs(positionDS.x), abs(positionDS.y)), abs(positionDS.z));
                clip(0.5 - maxDecalAxis);

                float2 direction = _ScrollDirection.xy;
                float directionLength = max(length(direction), 0.0001);
                direction /= directionLength;

                float2 shadowUv = (positionDS.xz + float2(0.5, 0.5)) * max(_Tiling.xy, float2(0.001, 0.001));
                shadowUv += _UVOffset.xy + direction * (_ScrollSpeed * _Time.y);

                half4 shadowSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUv);
                half luminanceMask = dot(shadowSample.rgb, half3(0.299, 0.587, 0.114));
                half textureMask = shadowSample.a < 0.999 ? shadowSample.a : luminanceMask;
                textureMask = saturate(textureMask);
                half shadowAlpha = saturate(textureMask * _Opacity * _ShadowColor.a);

                return half4(saturate(_ShadowColor.rgb), shadowAlpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DBufferProjector"
            Tags { "LightMode" = "DBufferProjector" }

            Blend 0 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            Blend 1 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            Blend 2 SrcAlpha OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Front

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DBufferVert
            #pragma fragment DBufferFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct DBufferAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DBufferVaryings
            {
                float4 positionHCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Opacity;
                half4 _ShadowColor;
                half _ScrollSpeed;
                float4 _ScrollDirection;
                float4 _Tiling;
                float4 _UVOffset;
            CBUFFER_END

            DBufferVaryings DBufferVert(DBufferAttributes input)
            {
                DBufferVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            void DBufferFrag(DBufferVaryings input, OUTPUT_DBUFFER(outDBuffer))
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 positionCS = input.positionHCS.xy;

#if UNITY_REVERSED_Z
                float depth = LoadSceneDepth(positionCS);
#else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, LoadSceneDepth(positionCS));
#endif

                float2 positionSS = positionCS * _ScreenSize.zw;
                float3 positionWS = ComputeWorldSpacePosition(positionSS, depth, UNITY_MATRIX_I_VP);
                float3 positionDS = TransformWorldToObject(positionWS);
                positionDS *= float3(1.0, -1.0, 1.0);

                float maxDecalAxis = max(max(abs(positionDS.x), abs(positionDS.y)), abs(positionDS.z));
                clip(0.5 - maxDecalAxis);

                float2 direction = _ScrollDirection.xy;
                float directionLength = max(length(direction), 0.0001);
                direction /= directionLength;

                float2 shadowUv = (positionDS.xz + float2(0.5, 0.5)) * max(_Tiling.xy, float2(0.001, 0.001));
                shadowUv += _UVOffset.xy + direction * (_ScrollSpeed * _Time.y);

                half4 shadowSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, shadowUv);
                half luminanceMask = dot(shadowSample.rgb, half3(0.299, 0.587, 0.114));
                half textureMask = shadowSample.a < 0.999 ? shadowSample.a : luminanceMask;
                textureMask = saturate(textureMask);
                half shadowAlpha = saturate(textureMask * _Opacity * _ShadowColor.a);

                DecalSurfaceData surfaceData;
                ZERO_INITIALIZE(DecalSurfaceData, surfaceData);
                surfaceData.baseColor = half4(saturate(_ShadowColor.rgb), shadowAlpha);
                surfaceData.normalWS = half4(0.0, 0.0, 0.0, 1.0);
                surfaceData.occlusion = 1.0;
                surfaceData.MAOSAlpha = 1.0;

                ENCODE_INTO_DBUFFER(surfaceData, outDBuffer);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
