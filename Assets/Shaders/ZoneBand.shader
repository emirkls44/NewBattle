Shader "NewBattle/ZoneBand"
{
    // Daralan alanin sinir seridi. Isiktan etkilenmez: serit bir yuzey degil,
    // bir isarettir - gunes acisina gore koyulasirsa oyuncu onu arazinin bir
    // parcasi sanir.
    //
    // Kenar yumusakligi kose renginin alfasindan geliyor; boylece tek bir
    // mesh ile keskin cekirdek + silinen kenarlar elde ediliyor ve doku
    // gerekmiyor.
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 0.42, 0.40, 1)
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
            Name "ZoneBandUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            // ZWrite kapali ama ZTest varsayilan (LEqual): serit onunde duran
            // agac ve kayalarin ARKASINDA kalir. Ekranin uzerine boyanmasi
            // isteseydik ZTest Always yazardik; istemiyoruz, cunku o zaman
            // serit nesnelerin uzerinden geciyormus gibi gorunur.
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return _BaseColor * input.color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
