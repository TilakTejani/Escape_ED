Shader "EscapeED/ArrowPulsing"
{
    Properties
    {
        _MainColor   ("Base Color",   Color)  = (0.05, 0.05, 0.05, 1)
        _PulseColor  ("Pulse Color",  Color)  = (0, 0.8, 1, 1)
        _PulseSpeed  ("Pulse Speed",  Float)  = 2.0
        _MinArrowAlpha ("Min Arrow Alpha", Range(0,1)) = 0.08
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest  ("Z Test",  Float) = 4
        [Enum(Off, 0, On, 1)]                          _ZWrite ("Z Write", Float) = 0
        _QueueOffset ("Queue Offset", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+1" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        ZTest  [_ZTest]
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
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            float4 _MainColor;
            float4 _PulseColor;
            float  _PulseSpeed;
            float  _MinArrowAlpha;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv         = input.uv;
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float pulse      = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed + input.uv.x * 5.0);
                float4 finalColor = lerp(_MainColor, _PulseColor, pulse * 0.4);

                float3 cubeToCam = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);
                float  d         = dot(normalize(input.normalWS), cubeToCam);
                float  t         = saturate(d / 0.2 + 1.0);
                finalColor.a     = lerp(_MinArrowAlpha, 1.0, t);

                return finalColor;
            }
            ENDHLSL
        }
    }
}
