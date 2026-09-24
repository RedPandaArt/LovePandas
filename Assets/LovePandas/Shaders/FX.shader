// Неосвещённый прозрачный шейдер для фона, лучей, тени-пятна и частиц.
// Режим смешивания задаётся материалом: альфа (SrcAlpha/OneMinusSrcAlpha) или сложение (SrcAlpha/One).
Shader "LovePandas/FX"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        [Toggle] _ZWrite ("ZWrite", Float) = 0
        _Wind ("Wind (UV wobble for backdrop foliage)", Range(0, 0.02)) = 0
        _GroundLine ("Ground line (v below is still)", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Wind, _GroundLine;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Ветер для нарисованного фона: листва сверху и по бокам колышется, площадка внизу неподвижна.
                float2 uv = i.uv;
                if (_Wind > 0)
                {
                    float mask = smoothstep(_GroundLine, _GroundLine + 0.2, uv.y) * (0.45 + abs(uv.x - 0.5) * 1.3);
                    float t = _Time.y;
                    uv.x += (sin(t * 1.3 + uv.y * 11.0) + 0.5 * sin(t * 2.1 + uv.y * 23.0 + uv.x * 5.0)) * _Wind * mask;
                    uv.y += sin(t * 1.0 + uv.x * 9.0) * _Wind * 0.5 * mask;
                }
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor * i.color;
            }
            ENDHLSL
        }
    }
}
