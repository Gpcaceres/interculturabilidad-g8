// Partículas de aire: luz aditiva que se dibuja sobre los tubos que las contienen.
// La profundidad se acerca a la cámara _Adelanto metros; así no las tapa la pared del
// bronquio, pero sí lo que esté claramente delante (lóbulos opacos, menú, manos).
Shader "Atlas/Aire aditivo"
{
    Properties
    {
        _BaseMap("Textura", 2D) = "white" {}
        _Adelanto("Adelanto (m)", Float) = 0.025
        _Intensidad("Intensidad", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Adelanto;
                float _Intensidad;
            CBUFFER_END

            struct Entrada
            {
                float4 pos : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Interpolado
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Interpolado vert(Entrada v)
            {
                Interpolado o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 vista = TransformWorldToView(TransformObjectToWorld(v.pos.xyz));
                o.pos = TransformWViewToHClip(vista);
                vista.z = min(vista.z + _Adelanto, -0.02);      // la cámara mira hacia -Z
                float4 cerca = TransformWViewToHClip(vista);
                o.pos.z = cerca.z / cerca.w * o.pos.w;
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Interpolado i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color;
                return half4(c.rgb * _Intensidad, c.a);
            }
            ENDHLSL
        }
    }
}
