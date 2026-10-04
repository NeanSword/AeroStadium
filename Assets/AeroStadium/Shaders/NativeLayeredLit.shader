// Native atlases, UV controls and color values are preserved. Lighting and
// masked emission are an explicit URP adaptation, not the source GPU program.
Shader "AeroStadium/NativeLayeredLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Couleur", 2D) = "white" {}
        [MainColor] _BaseColor("Teinte", Color) = (1,1,1,1)
        _BaseUv("Décalage natif couleur", Vector) = (0,0,0,0)
        _LayerMap("Iris / couche", 2D) = "white" {}
        _LayerUv("Décalage natif couche", Vector) = (0,0,0,0)
        _LayerEnabled("Couche active", Float) = 0
        _LayerUvChannel("Canal UV de couche", Float) = 0
        _Layer1OverLerpValue("Intensité de couche", Float) = 1
        _Layer1UVTranslateU("Translation de couche U", Float) = 0
        _Layer1UVTranslateV("Translation de couche V", Float) = 0
        _ConstantColor("Multiplicateur natif animé", Color) = (1,1,1,1)
        _ConstantColorValue("Multiplicateur natif actif", Float) = 1
        _FixMultiplierColor("Multiplicateur de présentation", Color) = (1,1,1,1)
        _EmissionMaskTex("Masque lumineux natif", 2D) = "black" {}
        _EmissionMaskUse("Masque lumineux actif", Float) = 0
        _EmissionMaskVal("Intensité lumineuse animée", Float) = 1
        _EmissionScale("Échelle lumineuse", Float) = 1
        _SwitchEmissionMaskTexUV("UV lumineux de couche", Float) = 0
        [Normal] _NormalMap("Relief", 2D) = "bump" {}
        _NormalEnabled("Relief actif", Float) = 0
        _Smoothness("Brillance", Range(0,1)) = .18
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces", Float) = 2
        [Enum(None,0,RGBA,15)] _ColorMask("Écriture couleur", Float) = 15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            ColorMask [_ColorMask]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_LayerMap); SAMPLER(sampler_LayerMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_EmissionMaskTex); SAMPLER(sampler_EmissionMaskTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _LayerMap_ST, _NormalMap_ST, _BaseColor, _BaseUv, _LayerUv;
                float4 _EmissionMaskTex_ST, _ConstantColor, _FixMultiplierColor;
                float _LayerEnabled, _LayerUvChannel, _Layer1OverLerpValue, _Layer1UVTranslateU, _Layer1UVTranslateV, _NormalEnabled, _Smoothness, _Cull;
                float _ConstantColorValue, _EmissionMaskUse, _EmissionMaskVal, _EmissionScale, _SwitchEmissionMaskTexUV;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD2; float2 layerUv : TEXCOORD3; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                half4 tangentWS : TEXCOORD4;
                float2 layerUv : TEXCOORD5;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = half4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv;
                output.layerUv = input.layerUv;
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Native atlas origins are in transformed texture space. The
                // animated ST offset already includes the source texture scale.
                float2 colorUv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw + _BaseUv.xy;
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, colorUv);
                float2 layerSource = lerp(input.uv,input.layerUv,step(.5,_LayerUvChannel));
                float2 layerUv = layerSource * _LayerMap_ST.xy + _LayerMap_ST.zw + _LayerUv.xy + float2(_Layer1UVTranslateU,_Layer1UVTranslateV);
                half3 layer = SAMPLE_TEXTURE2D(_LayerMap, sampler_LayerMap, layerUv).rgb;
                half3 albedo = lerp(color.rgb, lerp(layer, color.rgb, color.a), saturate(_LayerEnabled * _Layer1OverLerpValue)) * _BaseColor.rgb;
                albedo *= lerp(half3(1,1,1), _ConstantColor.rgb, saturate(_ConstantColorValue)) * _FixMultiplierColor.rgb;
                float2 emissionSource = lerp(input.uv, layerSource, step(.5,_SwitchEmissionMaskTexUV));
                float2 emissionUv = emissionSource * _EmissionMaskTex_ST.xy + _EmissionMaskTex_ST.zw;
                half emissionMask = SAMPLE_TEXTURE2D(_EmissionMaskTex, sampler_EmissionMaskTex, emissionUv).r;
                half emission = saturate(emissionMask * _EmissionMaskUse * _EmissionMaskVal * _EmissionScale);
                SurfaceData surface = (SurfaceData)0;
                // A masked source texel keeps its color when unlit. Removing its
                // diffuse share avoids doubling the color under arena lights.
                surface.albedo = albedo * (1 - emission);
                surface.emission = albedo * emission;
                surface.alpha = 1;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = _Smoothness;
                float2 normalUv = input.uv * _NormalMap_ST.xy + _NormalMap_ST.zw;
                half3 normalTs = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUv), 1);
                surface.normalTS = normalize(lerp(half3(0,0,1), normalTs, saturate(_NormalEnabled)));
                surface.occlusion = 1;
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                half3 normal = normalize(input.normalWS);
                half3 tangent = normalize(input.tangentWS.xyz);
                half3 bitangent = cross(normal, tangent) * input.tangentWS.w;
                data.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surface.normalTS, half3x3(tangent, bitangent, normal)));
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(data.normalWS);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                half4 result = UniversalFragmentPBR(data, surface);
                result.rgb = MixFog(result.rgb, input.fog);
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
