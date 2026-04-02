Shader "EscapeED/ArrowPulsing"
{
    Properties
    {
        _MainColor ("Base Color", Color) = (0.05, 0.05, 0.05, 1)
        _PulseColor ("Pulse Color", Color) = (0, 0.8, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        // Per-face alphas: index 0=up 1=down 2=left 3=right 4=forward 5=back
        _FaceAlphas0 ("Face Alphas (up,down,left,right)", Vector) = (1,1,1,1)
        _FaceAlphas1 ("Face Alphas (fwd,back,_,_)", Vector) = (1,1,0,0)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Z Test", Float) = 4
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 0
        _QueueOffset ("Queue Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        ZTest [_ZTest]
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
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1; // x = face index (0-5)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  faceIndex  : TEXCOORD1;
            };

            float4 _MainColor;
            float4 _PulseColor;
            float  _PulseSpeed;
            float4 _FaceAlphas0; // up, down, left, right
            float4 _FaceAlphas1; // forward, back, unused, unused

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = input.uv;
                output.faceIndex  = input.uv2.x;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + input.uv.x * 5.0);
                float4 finalColor = lerp(_MainColor, _PulseColor, pulse * 0.4);

                // Look up the alpha for this vertex's cube face
                int fi = (int)round(input.faceIndex);
                float faceAlpha;
                if      (fi == 0) faceAlpha = _FaceAlphas0.x;
                else if (fi == 1) faceAlpha = _FaceAlphas0.y;
                else if (fi == 2) faceAlpha = _FaceAlphas0.z;
                else if (fi == 3) faceAlpha = _FaceAlphas0.w;
                else if (fi == 4) faceAlpha = _FaceAlphas1.x;
                else              faceAlpha = _FaceAlphas1.y;

                finalColor.a = faceAlpha;
                return finalColor;
            }
            ENDHLSL
        }
    }
}
