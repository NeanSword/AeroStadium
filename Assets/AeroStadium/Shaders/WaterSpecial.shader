Shader "AeroStadium/Original Resonant and Hot Water"
{
    Properties
    {
        _Mode("Liquid, pressure wave, spray, steam", Float) = 0
        _Heat("Hot water", Float) = 0
        _Clock("Effect clock", Float) = 0
        _ClipShield("Clip the real shield interior", Float) = 0
        _ShieldCenter("Shield world centre", Vector) = (0,0,0,0)
        _ShieldRadii("Shield world radii", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+15" }
        Pass
        {
            Name "ResonantLiquidAndSteam"
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
                float _Mode, _Heat, _Clock, _ClipShield;
                float4 _ShieldCenter, _ShieldRadii;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 color : COLOR;
            };
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p)
            {
                float2 c=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            float Fbm(float2 p)
            { return Noise(p)*.57+Noise(p*2.04+13.2)*.28+Noise(p*4.13+5.7)*.15; }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world=TransformObjectToWorld(input.positionOS.xyz);
                float3 normal=TransformObjectToWorldNormal(input.normalOS);
                if (_Mode>1.5)
                {
                    // The packet centre remains in captured world space; only this small quad faces the camera.
                    float2 p=(input.uv-.5)*2;
                    float angle=input.color.g*.73;
                    float2 rotated=float2(cos(angle)*p.x-sin(angle)*p.y,sin(angle)*p.x+cos(angle)*p.y);
                    rotated.y*=input.color.a;
                    world+=UNITY_MATRIX_I_V._m00_m10_m20*rotated.x*input.color.r+
                           UNITY_MATRIX_I_V._m01_m11_m21*rotated.y*input.color.r;
                    normal=UNITY_MATRIX_I_V._m02_m12_m22;
                }
                output.positionCS=TransformWorldToHClip(world); output.positionWS=world;
                output.normalWS=normal; output.uv=input.uv; output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                if (input.color.b<.001) return 0;
                if (_ClipShield>.5)
                {
                    // This also removes the tiny chords between shell-conforming ring vertices and billboard edges.
                    float3 shell=(input.positionWS-_ShieldCenter.xyz)/max(_ShieldRadii.xyz,float3(.08,.08,.08));
                    clip(dot(shell,shell)-1.0003);
                }
                float t=_Clock, alpha;
                if (_Mode>2.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float seed=input.color.g;
                    float cloud=Fbm(p*2.8+float2(seed*.71,-t*.60));
                    float billow=Fbm(p*5.1+float2(-t*.23,seed*.35));
                    float silhouette=1-dot(p,p)+(.16*sin(p.x*6+seed)+cloud-.5)*.32;
                    float edge=smoothstep(-.03,.35,silhouette);
                    float detail=smoothstep(.20,.73,cloud*.78+billow*.22);
                    alpha=edge*detail*input.color.b;
                    float light=saturate(.35+cloud*.65+p.y*.12);
                    float3 steam=lerp(float3(.40,.53,.56),float3(.94,.98,.96),light);
                    return half4(steam*alpha,alpha);
                }
                if (_Mode>1.5)
                {
                    float2 p=(input.uv-.5)*2;
                    float d=dot(p,p), disc=smoothstep(1,.73,d);
                    float rim=pow(saturate(d),3), lens=exp(-dot(p-float2(-.28,.30),p-float2(-.28,.30))*36);
                    alpha=disc*(.58+rim*.23)*input.color.b;
                    float3 water=lerp(float3(.018,.30,.47),float3(.30,.76,.87),rim*.8);
                    water+=float3(.69,.94,.98)*lens*1.35;
                    return half4(water*alpha,alpha);
                }
                float3 view=SafeNormalize(GetCameraPositionWS()-input.positionWS);
                float3 normal=SafeNormalize(input.normalWS);
                normal*=dot(normal,view)<0 ? -1:1;
                Light light=GetMainLight();
                float3 halfVector=SafeNormalize(view+light.direction);
                float spec=pow(saturate(dot(normal,halfVector)),96);
                float fresnel=pow(1-saturate(dot(normal,view)),3);
                float2 flow=input.uv*float2(5,9)+float2(input.color.r*.37,-t*(2.7+_Heat*2));
                float broad=Fbm(flow), fine=Noise(flow*float2(2.2,1.3)+7.1);
                if (_Mode>.5)
                {
                    float across=abs(input.uv.x*2-1), edge=smoothstep(1,.52,across);
                    float threads=pow(saturate(.5+.5*sin(input.uv.y*55-t*24+input.color.r+fine*3)),8);
                    float3 ring=lerp(float3(.035,.36,.54),float3(.49,.86,.94),.34+broad*.36+threads*.22);
                    ring+=float3(.31,.52,.54)*spec;
                    alpha=edge*(.44+broad*.25+threads*.16)*input.color.b;
                    return half4(ring*alpha,alpha);
                }
                float ripples=pow(saturate(.5+.5*sin(input.uv.y*37+sin(input.uv.x*18+t*3)*.65-t*14)),12);
                float foam=pow(saturate(fine*.78+broad*.36),9)*_Heat;
                float depth=saturate(dot(normal,view));
                float3 water=lerp(float3(.008,.14,.28),float3(.025,.47,.64),.42+broad*.4);
                water=lerp(water,float3(.29,.77,.86),fresnel*.77+ripples*.16);
                water=lerp(water,float3(.73,.94,.92),foam*.66);
                water*=.73+saturate(dot(normal,light.direction))*.27;
                water+=float3(.73,.96,1)*spec*light.color*1.28;
                alpha=(.59+broad*.12+fresnel*.18+foam*.07)*input.color.b;
                return half4(water*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
