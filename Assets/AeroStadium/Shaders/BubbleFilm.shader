Shader "AeroStadium/Original Bubble Film"
{
    Properties { _Surface("Bubble film or surface pops", Float) = 0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+16" }
        Pass
        {
            Name "OriginalBubbleFilm"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Surface;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                if (_Surface<.5 || input.color.a>.5)
                {
                    float2 p=(input.uv-.5)*2;
                    if (_Surface>.5) p.y*=1.45;
                    world+=UNITY_MATRIX_I_V._m00_m10_m20*p.x*input.color.r
                        + UNITY_MATRIX_I_V._m01_m11_m21*p.y*input.color.r;
                }
                output.positionCS=TransformWorldToHClip(world);
                output.uv=input.uv; output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if (input.color.b<.001) return 0;
                if (_Surface>.5 && input.color.a<.5)
                {
                    float edge=sin(saturate(input.uv.y)*3.14159);
                    float broken=.66+.34*sin(input.uv.x*81+input.color.g*3);
                    float alpha=edge*broken*input.color.b;
                    return half4(float3(.45,.88,1)*alpha,alpha);
                }
                float2 p=(input.uv-.5)*2;
                float r2=dot(p,p); clip(1-r2);
                float z=sqrt(saturate(1-r2));
                float rim=pow(1-z,3.2);
                float edge=1-smoothstep(.94,1,r2);
                float highlight=exp(-dot(p-float2(-.28,.39),p-float2(-.28,.39))*100);
                float crescent=pow(saturate(dot(normalize(float3(p,z)),normalize(float3(-.55,.68,.48)))),24);
                float lower=exp(-dot(p-float2(.40,-.49),p-float2(.40,-.49))*110);
                float phase=input.color.g;
                float3 film=lerp(float3(.035,.42,.71),float3(.48,.87,1),rim);
                film+=float3(.13,.07,.16)*(.5+.5*sin(atan2(p.y,p.x)*3+phase))*rim;
                film+=float3(.73,.93,1)*(highlight*.8+crescent*.48+lower*.23);
                // Almost clear centers and thin luminous rims retain a hollow soap-film reading.
                float alpha=(.045+rim*.52+highlight*.52+crescent*.25+lower*.16)*input.color.b*edge;
                if (_Surface>.5) alpha=(.23+rim*.38+highlight*.37)*input.color.b*edge;
                alpha=saturate(alpha);
                return half4(film*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
