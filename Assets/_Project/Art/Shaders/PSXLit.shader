// Shader URP no estilo PlayStation 1:
// - Vertex snapping: posições arredondadas para a grade da resolução interna (tremido/jitter).
// - Affine texture mapping: UV sem correção de perspectiva (texturas "nadam" em polígonos grandes).
// - Iluminação Gouraud por vértice (luz principal + ambiente), sem sombras, como no hardware original.
// Os parâmetros de resolução e intensidade afim são globais, publicados por PsxRenderTextureSetup.
Shader "GameYunit/PSX Lit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Textura", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff (0 = desligado)", Range(0, 1)) = 0
        _SnapMultiplier ("Multiplicador da grade de snap", Range(0.25, 2)) = 1
        [Toggle(_PSX_UNLIT)] _Unlit ("Sem iluminação", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            float _SnapMultiplier;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Globais (fora do CBUFFER): xy = resolução interna; zero desliga o snap.
        float4 _PsxSnapResolution;
        float _PsxAffineStrength;

        float4 PsxSnap(float4 positionCS)
        {
            float2 grid = _PsxSnapResolution.xy * 0.5 * _SnapMultiplier;
            if (grid.x > 0.0 && positionCS.w > 0.0)
            {
                positionCS.xy = round(positionCS.xy / positionCS.w * grid) / grid * positionCS.w;
            }

            return positionCS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _PSX_UNLIT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // xy = uv * w, z = w. Dividir no fragment desfaz a correção de perspectiva (efeito afim).
                float3 affineUV : TEXCOORD0;
                half3 lighting : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = PsxSnap(positions.positionCS);

                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                float w = lerp(1.0, output.positionCS.w, saturate(_PsxAffineStrength));
                output.affineUV = float3(uv * w, w);

                #if defined(_PSX_UNLIT)
                output.lighting = half3(1.0, 1.0, 1.0);
                #else
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                Light mainLight = GetMainLight();
                half3 diffuse = mainLight.color * saturate(dot(normalWS, mainLight.direction));
                output.lighting = diffuse + SampleSH(normalWS);
                #endif

                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.affineUV.xy / input.affineUV.z;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                clip(color.a - _Cutoff);

                half3 rgb = MixFog(color.rgb * input.lighting, input.fogFactor);
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }

        // Necessário para depth prepass/depth priming do URP; usa o mesmo snap para não haver z-fighting.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = PsxSnap(TransformObjectToHClip(input.positionOS.xyz));
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
