Shader "NewBattle/SoftToon"
{
    // Oyunun ortak "yumusak" gorunumu: harita, karakter, loot ve Meshy prop'lari
    // ayni isik modeliyle cizilsin diye tek shader.
    //
    // Gercekci URP/Lit'ten farki:
    //   - Isik-golge gecisi genis ve yumusak (sert bant yok, fotografik de degil)
    //   - Golgeler siyaha degil, hafif maviye kayar
    //   - Kenarlarda ince bir parlama (rim): oyuncak / vinil hissi
    //   - Kucuk, yumusak bir parlama noktasi (tam metal/cam yansimasi yok)
    //
    // Renk kaynagi: _BaseMap x _BaseColor, istenirse vertex rengiyle carpilir
    // (_VertexColorStrength = 1). Boylece hem dokulu Meshy modelleri hem de
    // vertex renkli prosedurel modeller ayni shader'i kullanir.
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1,1,1,1)
        _VertexColorStrength ("Vertex Color", Range(0,1)) = 0

        [Header(Isik)]
        _ShadeThreshold ("Shade Threshold", Range(-1,1)) = -0.05
        _ShadeSoftness ("Shade Softness", Range(0.01,1)) = 0.45
        _ShadowTint ("Shadow Tint", Color) = (0.74,0.78,0.92,1)
        _Ambient ("Shadow Brightness", Range(0,1)) = 0.72
        _ShadowStrength ("Cast Shadow Strength", Range(0,1)) = 0.8

        [Header(Parlama)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.16
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _Gloss ("Soft Highlight", Range(0,1)) = 0.1
        _GlossSize ("Highlight Size", Range(2,128)) = 24

        // PlayerVisibility cimendeki yerel oyuncuyu yari saydam cizerken URP
        // Lit'in ayni adli ozelliklerini yaziyor. Burada da olmalari, o gecisin
        // bu shader'da da calismasini sagliyor. Varsayilan: opak.
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // SRP Batcher: butun materyal ozellikleri tek CBUFFER'da olmali.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ShadowTint;
            float4 _RimColor;
            float _VertexColorStrength;
            float _ShadeThreshold;
            float _ShadeSoftness;
            float _Ambient;
            float _ShadowStrength;
            float _RimStrength;
            float _RimPower;
            float _Gloss;
            float _GlossSize;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                OUT.color = IN.color;
                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 normalWS = normalize(IN.normalWS);
                float3 viewWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                albedo.rgb *= lerp(float3(1, 1, 1), IN.color.rgb, _VertexColorStrength);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Genis, yumusak terminator: isik yuzeyin uzerinde "akar",
                // bantlara bolunmez.
                float ndotl = dot(normalWS, mainLight.direction);
                float lit = smoothstep(_ShadeThreshold - _ShadeSoftness, _ShadeThreshold + _ShadeSoftness, ndotl);

                // Dusen golge de tam siyaha gitmesin.
                lit *= lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

                float3 shadowColor = albedo.rgb * _ShadowTint.rgb * _Ambient;
                float3 litColor = albedo.rgb * mainLight.color;
                float3 color = lerp(shadowColor, litColor, lit);

                // Kenar parlamasi: aydinlik tarafta biraz daha guclu.
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewWS)), _RimPower);
                color += _RimColor.rgb * fresnel * _RimStrength * (0.35 + 0.65 * lit);

                // Yumusak parlama noktasi (Blinn-Phong, dusuk yogunluk).
                float3 halfDir = normalize(mainLight.direction + viewWS);
                float highlight = pow(saturate(dot(normalWS, halfDir)), _GlossSize) * _Gloss * lit;
                color += mainLight.color * highlight;

                color = MixFog(color, IN.fogFactor);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVertex(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                OUT.positionCS = positionCS;
                return OUT;
            }

            half4 ShadowFragment(ShadowVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVertex(DepthAttributes IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 DepthFragment(DepthVaryings IN) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
