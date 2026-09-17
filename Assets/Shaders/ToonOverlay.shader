Shader "NewBattle/ToonOverlay"
{
    // Zemine yatan gosterge katmani: oyuncu cemberleri, ayak izleri, drop halkasi.
    // Isiktan etkilenmez (unlit) - gece/golge farketmeksizin ayni okunakliliki korur.
    // Derinlige yazmaz, boylece ust uste binen halkalar birbirini kirpmaz.
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Fill ("Radial Fill", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Overlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings Vertex(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 Fragment(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);

                // Halka mesh'i uv.x'e 0..1 arasinda aciyi yazar. Drop kutusunun
                // "3 saniye bekle" gostergesi bu sayede tek float ile doluyor.
                clip(fill - IN.uv.x + 0.0001);

                return half4(IN.color.rgb * tint.rgb, IN.color.a * tint.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
