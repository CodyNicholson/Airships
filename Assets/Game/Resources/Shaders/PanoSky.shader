// An equirectangular panorama as the skybox. The image's center looks along +Z.
Shader "Airships/PanoSky"
{
    Properties
    {
        _MainTex ("Panorama", 2D) = "gray" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Yaw ("Yaw Radians", Float) = 0
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Yaw;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Computed per pixel: per-vertex direction would bend straight lines on the sky mesh.
                float3 direction = normalize(input.direction);
                float u = atan2(direction.x, direction.z) / (2.0 * PI) + 0.5 + _Yaw / (2.0 * PI);
                float v = asin(clamp(direction.y, -1.0, 1.0)) / PI + 0.5;

                // The seam at u = 0 would pick a blurred mip without explicit derivatives; these sheets have no mips.
                half4 color = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(u, v), 0);
                return half4(color.rgb * _Tint.rgb, 1);
            }
            ENDHLSL
        }
    }
}
