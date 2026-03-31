Shader "EscapeED/ArrowPulsing"
{
    Properties
    {
        _MainColor ("Base Color", Color) = (0.05, 0.05, 0.05, 1) // Deep Matte Black
        _PulseColor ("Pulse Color", Color) = (0, 0.8, 1, 1)      // Neon Cyan
        _PulseSpeed ("Pulse Speed", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        LOD 100

        // Professional Decal Trick: 
        // This ensures the arrow sits PERECTLY on the cube without flickering (Z-fighting)
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _MainColor;
            float4 _PulseColor;
            float _PulseSpeed;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Create a sleek flowing pulse animation along the path
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + input.uv.x * 5.0);
                float4 finalColor = lerp(_MainColor, _PulseColor, pulse * 0.4);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}
