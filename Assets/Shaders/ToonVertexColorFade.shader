Shader "NewBattle/ToonVertexColorFade"
{
    // ToonVertexColor'in saydam kardesi. Yerel oyuncu cimene girdiginde govdesi
    // bu materyale gecer: "gizlendin" geri bildirimini sadece o oyuncu gorur.
    //
    // Ayri bir shader olmasinin sebebi, harmanlama (blend) durumunun materyalde
    // degil pass'te tanimlanmasi; tek shader ile opak ve saydam ayni anda olamaz.
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 0.62
        _Ambient ("Ambient Floor", Range(0,1)) = 0.55
        _ShadeBands ("Shade Bands", Range(1,5)) = 2
        _ShadeSoftness ("Band Softness", Range(0.001,0.4)) = 0.06
        _ShadowTint ("Shadow Tint", Color) = (0.62,0.68,0.85,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLitFade"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowTint;
                float _Alpha;
                float _Ambient;
                float _ShadeBands;
                float _ShadeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
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
                OUT.color = IN.color;
                return OUT;
            }

            // Isik hesabi ToonVertexColor ile BIREBIR ayni olmali. En ufak fark
            // (ornegin golge ornegini atlamak) karakterin saydamlasirken renk de
            // degistirmesine yol acar - istenen sadece saydamlik.
            half4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float3 normalWS = normalize(IN.normalWS);
                float4 albedo = IN.color * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float bands = max(1.0, _ShadeBands);
                float scaled = saturate(dot(normalWS, mainLight.direction)) * bands;
                float lower = floor(scaled);
                float f = scaled - lower;
                float lit = (lower + smoothstep(0.5 - _ShadeSoftness, 0.5 + _ShadeSoftness, f)) / bands;
                lit *= mainLight.shadowAttenuation;

                float3 shadowColor = albedo.rgb * _ShadowTint.rgb * _Ambient;
                float3 finalColor = lerp(shadowColor, albedo.rgb * mainLight.color, lit);

                return half4(finalColor, _Alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
