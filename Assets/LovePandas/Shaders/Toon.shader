// Аниме-тушь для персонажей: две ступени света, тёплая тень, подсветка по краю, обводка.
// Нарисованная текстура (_BaseMap) умножается на цвет — под модели с hand-painted текстурами.
Shader "LovePandas/Toon"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.62, 0.52, 0.72, 1)
        _ShadeStep ("Shade Step", Range(-1,1)) = 0.05
        _ShadeSoftness ("Shade Softness", Range(0.001,0.5)) = 0.06
        _RimColor ("Rim Color", Color) = (1, 0.93, 0.75, 1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim Strength", Range(0,2)) = 0.55
        _OutlineColor ("Outline Color", Color) = (0.18, 0.1, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0,0.05)) = 0.012
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadeColor, _RimColor, _OutlineColor;
            half _ShadeStep, _ShadeSoftness, _RimPower, _RimStrength, _OutlineWidth;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewWS : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 v = normalize(i.viewWS);
                half ndl = dot(n, light.direction);
                half lit = smoothstep(_ShadeStep - _ShadeSoftness, _ShadeStep + _ShadeSoftness, ndl);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 shade = albedo.rgb * _ShadeColor.rgb;
                half3 col = lerp(shade, albedo.rgb * light.color, lit);
                col += albedo.rgb * SampleSH(n) * 0.35;

                half rim = pow(saturate(1 - dot(n, v)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim * (0.35 + 0.65 * lit);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Обводка: раздутая по нормалям оболочка, видны только задние грани.
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(posWS + nWS * _OutlineWidth);
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
