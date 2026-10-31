// Painted gradient sky with a soft sun, used as the scene skybox.
Shader "Airships/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.1, 0.4, 0.8, 1)
        _HorizonColor ("Horizon", Color) = (0.7, 0.9, 1, 1)
        _BelowColor ("Below", Color) = (0.6, 0.7, 0.85, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.8, 1)
        _SunDirection ("Sun Direction", Vector) = (0.4, 0.6, 0.5, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BelowColor;
                half4 _SunColor;
                float4 _SunDirection;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                output.direction = positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half up = saturate(direction.y);
                half down = saturate(-direction.y * 3.0);
                half3 color = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, 0.55));
                color = lerp(color, _BelowColor.rgb, down);

                half sun = saturate(dot(direction, normalize(_SunDirection.xyz)));
                color += _SunColor.rgb * (pow(sun, 900.0) * 3.0 + pow(sun, 24.0) * 0.28);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
