Shader "AeroStadium/Original Nature Energy"
{
    Properties
    {
        _Layer("Core, filament, leaf",Float)=0
        _Age("Effect age",Float)=0
        _ClipShield("Clip shield interior",Float)=0
        _ShieldCenter("Shield centre",Vector)=(0,0,0,0)
        _ShieldRadii("Shield radii",Vector)=(1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+16" }
        Pass
        {
            Name "NatureEnergy"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Layer, _Age, _ClipShield;
                float4 _ShieldCenter, _ShieldRadii;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS);
                o.uv=input.uv;o.color=input.color;return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                clip(i.color.a-.001);
                if(_ClipShield>.5)
                {
                    float3 p=(i.positionWS-_ShieldCenter.xyz)/max(_ShieldRadii.xyz,float3(.02,.02,.02));
                    clip(dot(p,p)-1.0001);
                }
                float alpha=i.color.a;float3 tint=i.color.rgb;
                float3 normal=SafeNormalize(i.normalWS),view=SafeNormalize(GetCameraPositionWS()-i.positionWS);
                float facing=abs(dot(normal,view));
                if(_Layer<.5)
                {
                    // Integer longitude frequencies keep the sphere seam continuous.
                    float longitude=i.uv.x*6.2831853,latitude=i.uv.y*3.14159265;
                    float flow=sin(longitude*5+sin(latitude*6-_Age*2.1)*1.1+_Age*3.4);
                    float fine=sin(longitude*11-latitude*7+_Age*5.2);
                    float veins=pow(saturate(flow*.5+.5),9)*sin(latitude);
                    float rim=pow(1-facing,2.4);
                    float heart=pow(facing,4);
                    tint=lerp(float3(.06,.28,.014),tint,.46+.20*fine);
                    tint+=float3(.62,.94,.16)*(veins*.65+heart*.6+rim*.24);
                    alpha*=.48+.25*facing+.14*rim;
                    return half4(tint*alpha*1.55,alpha);
                }
                if(_Layer<1.5)
                {
                    float edge=1-abs(i.uv.y*2-1);
                    alpha*=smoothstep(0,.8,edge)*sqrt(saturate(sin(i.uv.x*3.14159265)));
                    return half4(tint*alpha*1.7,alpha);
                }
                normal*=dot(normal,view)<0?-1:1;
                Light light=GetMainLight();
                float across=abs(i.uv.y*2-1);
                float midrib=1-smoothstep(.025,.075,across);
                float veins=(1-smoothstep(.02,.08,abs(frac(i.uv.x*5-across*1.4+.5)-.5)))*smoothstep(.08,.2,across);
                tint=lerp(tint,float3(.65,.86,.16),midrib*.75+veins*.2);
                tint*=.58+saturate(dot(normal,light.direction))*.42;
                return half4(tint*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
