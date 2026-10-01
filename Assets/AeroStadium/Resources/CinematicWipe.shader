Shader "AeroStadium/CinematicWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Dark stadium blue", Color) = (0.006, 0.016, 0.045, 1)
        _Accent ("Reveal edge", Color) = (1, 0.78, 0.33, 1)
        _Progress ("Progress", Range(0, 1)) = 0
        _Mode ("Mode", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _Accent;
                float _Progress;
                float _Mode;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float progress = saturate(_Progress);
                float metric;
                float boundary;
                if (_Mode < 0.5) { metric = abs(uv.y - 0.5); boundary = progress * 0.56 - 0.035; }
                else if (_Mode < 1.5) { metric = abs(uv.x - 0.5); boundary = progress * 0.56 - 0.035; }
                else { metric = distance(uv, float2(0.5, 0.5)); boundary = progress * 0.78 - 0.06; }
                half alpha = smoothstep(boundary - 0.006, boundary + 0.006, metric);
                half edge = 1.0h - smoothstep(0.006, 0.023, abs(metric - boundary));
                half4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 color = lerp(_Color.rgb, _Accent.rgb, edge * 0.82h);
                return half4(color * textureColor.rgb, alpha * _Color.a * textureColor.a * input.color.a);
            }
            ENDHLSL
        }
    }
}