// Authored world-space material detail for the modern Elite Four arena.
// No native atlas, source texel addressing or vertex-color lighting is used.
Shader "AeroStadium/Modern Arena Surface"
{
    Properties
    {
        [MainColor] _BaseColor("Base color", Color) = (.5,.43,.3,1)
        _AccentColor("Mineral / metal accent", Color) = (.3,.24,.14,1)
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
        [Enum(Plain,0,VeinedStone,1,HonedStone,2,BrushedMetal,3,Water,4,Lava,5,MoltenSplash,6)] _SurfacePattern("Surface", Float) = 1
        _LavaFlowSpeed("Lava drift metres per second", Range(0,1)) = .35
        _LavaWaveHeight("Lava swell metres", Range(0,.6)) = .34
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .5
        _PatternScale("Mineral scale per metre", Float) = .5
        _DetailStrength("Subtle mineral variation", Range(0,.3)) = .06
        _Relief("Relief in metres", Range(0,.04)) = .01
        _GrainScale("Micrograin per metre", Float) = 36
        _RoughnessVariation("Micro roughness", Range(0,.2)) = .06
        _VeinStrength("Mineral vein contrast", Range(0,1)) = .24
        _VeinWidth("Mineral vein width", Range(.005,.2)) = .04
        _PanelJoints("Stone tessellation", Range(0,1)) = 0
        _TileSize("Stone size in metres", Float) = 1.35
        _JointWidth("Stone joint width in metres", Float) = .027
        _TileVariation("Stone color variation", Range(0,.15)) = .04
        _JointColor("Stone joint color", Color) = (.03,.025,.015,1)
        _BrushDirection("World brush axis", Vector) = (1,.12,.15,0)
        _OpacityFresnel("Glass edge opacity", Range(0,1)) = 0
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Write depth", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Faces", Float) = 2
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        // One shared layout across forward, shadow and depth passes preserves
        // SRP Batcher compatibility. Pattern choices are material values, not
        // shader keywords, so each palette swatch adds no feature variants.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _AccentColor, _EmissionColor, _JointColor, _BrushDirection;
            float _SurfacePattern, _Metallic, _Smoothness, _PatternScale;
            float _DetailStrength, _Relief, _GrainScale, _RoughnessVariation;
            float _VeinStrength, _VeinWidth, _PanelJoints, _TileSize;
            float _JointWidth, _TileVariation, _OpacityFresnel, _SrcBlend;
            float _DstBlend, _ZWrite, _Cull, _LavaFlowSpeed, _LavaWaveHeight;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 fogAndVertexLight : TEXCOORD2;
            half4 color : TEXCOORD3;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        struct ArenaSurface
        {
            half3 albedo;
            half smoothness;
            half occlusion;
            half metallic;
            half3 emission;
            float height;
        };

        float Hash3(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float Noise3(float3 p)
        {
            float3 cell = floor(p);
            float3 f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = lerp(Hash3(cell), Hash3(cell + float3(1,0,0)), f.x);
            float b = lerp(Hash3(cell + float3(0,1,0)), Hash3(cell + float3(1,1,0)), f.x);
            float c = lerp(Hash3(cell + float3(0,0,1)), Hash3(cell + float3(1,0,1)), f.x);
            float d = lerp(Hash3(cell + float3(0,1,1)), Hash3(cell + float3(1,1,1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        float2 SurfacePlane(float3 p, half3 normal)
        {
            float3 n = abs(normal);
            if (n.y >= n.x && n.y >= n.z) return p.xz;
            return n.x > n.z ? p.zy : p.xy;
        }

        // Continuous liquid: flowing thermal bands and filtered small ripples,
        // with a glossy dielectric surface that catches the courtyard sun.
        void LavaSurface(float3 positionWS, inout ArenaSurface surface)
        {
            float2 drift = float2(.91,.42) * (_Time.y * _LavaFlowSpeed);
            float2 p = (positionWS.xz - drift) * max(_PatternScale, .01);
            float broad = Noise3(float3(p * .58, _Time.y * .07));
            float curl = Noise3(float3(p * 1.13 + 7.3, _Time.y * .10));
            float2 flowCurl = float2(curl - .5, broad - .5) * 1.5;
            float band = .5 + .5 * sin((p.y + flowCurl.y) * 2.5 + (p.x + flowCurl.x) * .74 + broad * 4.2);
            float heat = saturate(.08 + broad * .45 + band * .48);
            surface.albedo = lerp(_BaseColor.rgb, _AccentColor.rgb, heat);
            surface.emission = lerp(half3(.72,.048,.001), half3(1.35,.245,.008), heat);
            surface.metallic = saturate(_Metallic);
            surface.smoothness = clamp(_Smoothness - band * .025, .8, .96);
            surface.occlusion = 1;
            float phaseA = p.x * 4.3 + p.y * 1.6 + curl * 1.4;
            float phaseB = p.y * 6.7 - p.x * 2.1 + broad * 1.8;
            float fadeA = saturate(1 - fwidth(phaseA) / PI);
            float fadeB = saturate(1 - fwidth(phaseB) / PI);
            surface.height = sin(phaseA) * .46 * fadeA + sin(phaseB) * .24 * fadeB;
        }
        // A continuous, slightly irregular stone mosaic. Shared Voronoi edges
        // avoid the uniform printed checkerboard of an atlas. Joint filtering
        // and micrograin fading prevent distant sparkling in the battle camera.
        void StoneJoints(float2 plane, out float joint, out float stoneTint)
        {
            float2 p = plane / max(_TileSize, .1);
            float2 cell = floor(p);
            float2 f = frac(p);
            float nearest = 100, second = 100;
            float tint = .5;
            [unroll] for (int y = -1; y <= 1; y++)
            {
                [unroll] for (int x = -1; x <= 1; x++)
                {
                    float2 neighbour = float2(x,y);
                    float2 seed = cell + neighbour;
                    float2 random = float2(Hash3(float3(seed, 3.1)), Hash3(float3(seed, 19.7)));
                    float2 delta = neighbour + lerp(float2(.5,.5), random, .62) - f;
                    float distanceSq = dot(delta, delta);
                    if (distanceSq < nearest)
                    {
                        second = nearest;
                        nearest = distanceSq;
                        tint = Hash3(float3(seed, 41.2));
                    }
                    else second = min(second, distanceSq);
                }
            }
            float edgeDistance = (sqrt(second) - sqrt(nearest)) * .5;
            float width = _JointWidth / max(_TileSize, .1);
            float filter = max(fwidth(edgeDistance), .0001);
            joint = (1 - smoothstep(width, width + filter, edgeDistance)) * _PanelJoints;
            stoneTint = (tint - .5) * _TileVariation * _PanelJoints;
        }

        ArenaSurface EvaluateSurface(float3 positionWS, half3 normalWS)
        {
            ArenaSurface surface;
            surface.albedo = _BaseColor.rgb;
            surface.smoothness = _Smoothness;
            surface.occlusion = 1;
            surface.metallic = saturate(_Metallic);
            surface.emission = _EmissionColor.rgb;
            surface.height = 0;
            if (_SurfacePattern > 5.5) return surface;
            if (_SurfacePattern < .5) return surface;
            if (_SurfacePattern > 4.5)
            {
                LavaSurface(positionWS, surface);
                return surface;
            }

            float3 p = positionWS * max(_PatternScale, .01);
            float broad = Noise3(p * .73);
            float middle = Noise3(p * 2.07 + float3(5.7, 13.1, 2.3));
            float footprint = max(length(ddx(positionWS)), length(ddy(positionWS)));
            float grainFade = saturate(1 - footprint * max(_GrainScale, 1) * .65);
            float grain = (Noise3(positionWS * max(_GrainScale, 1)) - .5) * grainFade;

            if (_SurfacePattern < 1.5)
            {
                float flow = dot(p, float3(.46, .21, .28)) + (broad - .5) * 1.35 + (middle - .5) * .28;
                float wave = abs(sin(flow * TWO_PI));
                float edgeFilter = max(fwidth(wave) * 1.2, .006);
                float vein = 1 - smoothstep(_VeinWidth, _VeinWidth + edgeFilter, wave);
                float branchWave = abs(sin((flow * 1.91 + broad * .31) * TWO_PI));
                float branchVein = 1 - smoothstep(_VeinWidth * .32,
                    _VeinWidth * .32 + max(fwidth(branchWave), .005), branchWave);
                surface.albedo = lerp(surface.albedo, _AccentColor.rgb,
                    saturate((vein + branchVein * .2) * _VeinStrength));
                surface.albedo *= 1 + (broad - .5) * _DetailStrength;
                // Mineral color veins are inside the stone, not engraved lines.
                surface.height = grain * .045;
                surface.smoothness -= (middle - .5) * _RoughnessVariation + grain * .025;
            }
            else if (_SurfacePattern < 2.5)
            {
                float mineral = broad * .65 + middle * .35;
                surface.albedo = lerp(surface.albedo, _AccentColor.rgb, mineral * _VeinStrength);
                surface.albedo *= 1 + ((mineral - .5) * .7 + grain * .3) * _DetailStrength;
                surface.height = (middle - .5) * .24 + grain * .10;
                surface.smoothness -= (mineral - .5 + grain * .35) * _RoughnessVariation;
            }
            else if (_SurfacePattern < 3.5)
            {
                float3 axis = _BrushDirection.xyz;
                axis *= rsqrt(max(dot(axis, axis), .0001));
                float phase = dot(positionWS, axis) * max(_GrainScale, 1) * TWO_PI + broad * 1.3;
                float brushFade = saturate(1 - fwidth(phase) / PI);
                float brush = (sin(phase) * .34 + sin(phase * 1.43) * .16) * brushFade;
                surface.albedo = lerp(surface.albedo, _AccentColor.rgb, broad * _DetailStrength);
                surface.albedo *= 1 + brush * _DetailStrength;
                surface.smoothness -= brush * _RoughnessVariation + (middle - .5) * .025;
                surface.height = brush * .13;
            }
            else
            {
                float waveA = sin(dot(positionWS.xz, float2(.75,.31)) * 2.1 + _Time.y * .52);
                float waveB = sin(dot(positionWS.xz, float2(-.26,.61)) * 3.2 - _Time.y * .37);
                float ripple = waveA * .62 + waveB * .38;
                surface.albedo = lerp(surface.albedo, _AccentColor.rgb, (ripple * .5 + .5) * _DetailStrength);
                surface.height = ripple;
                surface.smoothness -= abs(ripple) * _RoughnessVariation;
            }

            if (_PanelJoints > .001)
            {
                float joint, tint;
                StoneJoints(SurfacePlane(positionWS, normalWS), joint, tint);
                surface.albedo = lerp(surface.albedo * (1 + tint), _JointColor.rgb, joint);
                surface.smoothness = lerp(surface.smoothness, .23, joint);
                surface.occlusion = 1 - joint * .15;
                surface.height -= joint * .7;
            }
            surface.smoothness = clamp(surface.smoothness, .05, .97);
            return surface;
        }

        half3 SurfaceNormal(float3 positionWS, half3 normalWS, float height)
        {
            float3 n = normalize(normalWS);
            float3 dx = ddx(positionWS), dy = ddy(positionWS);
            float3 r1 = cross(dy, n), r2 = cross(n, dx);
            float determinant = dot(dx, r1);
            float3 gradient = (ddx(height) * r1 + ddy(height) * r2) * sign(determinant);
            return normalize(n - gradient * (_Relief / max(abs(determinant), .0000001)));
        }

        Varyings ArenaVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
            if (_SurfacePattern > 4.5 && _SurfacePattern < 5.5)
            {
                float a = dot(p.positionWS.xz, float2(.43,.18)) - _Time.y * .61;
                float b = dot(p.positionWS.xz, float2(-.17,.36)) + _Time.y * .47;
                float c = dot(p.positionWS.xz, float2(1.25,.74)) - _Time.y * 1.32;
                p.positionWS.y += (sin(a) * .56 + sin(b) * .28 + sin(c) * .16) * _LavaWaveHeight;
                p.positionCS = TransformWorldToHClip(p.positionWS);
                output.normalWS = normalize(float3(-_LavaWaveHeight * (.2408 * cos(a) - .0476 * cos(b) + .20 * cos(c)),
                    1, -_LavaWaveHeight * (.1008 * cos(a) + .1008 * cos(b) + .1184 * cos(c))));
            }
            else output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionCS = p.positionCS;
            output.positionWS = p.positionWS;
            output.color = input.color;
            output.fogAndVertexLight = half4(ComputeFogFactor(p.positionCS.z),
                VertexLighting(p.positionWS, output.normalWS));
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ArenaVertex
            #pragma fragment ArenaFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            half4 ArenaFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 geometricNormal = normalize(input.normalWS);
                ArenaSurface authored = EvaluateSurface(input.positionWS, geometricNormal);
                if (_SurfacePattern > 5.5)
                {
                    authored.albedo *= input.color.rgb;
                    authored.emission *= input.color.rgb;
                }
                half3 normal = SurfaceNormal(input.positionWS, geometricNormal, authored.height);
                half3 viewDirection = GetWorldSpaceNormalizeViewDir(input.positionWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = authored.albedo;
                surface.metallic = authored.metallic;
                surface.specular = half3(.04,.04,.04);
                surface.smoothness = authored.smoothness;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = authored.occlusion;
                surface.emission = authored.emission;
                surface.alpha = saturate(_BaseColor.a + Pow4(1 - saturate(dot(normal, viewDirection))) * _OpacityFresnel);

                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.positionCS = input.positionCS;
                data.normalWS = normal;
                data.viewDirectionWS = viewDirection;
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    data.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                data.fogCoord = input.fogAndVertexLight.x;
                data.vertexLighting = input.fogAndVertexLight.yzw;
                data.bakedGI = SampleSH(normal);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);

                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ArenaShadowVertex
            #pragma fragment ArenaShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ArenaShadowVertex(Attributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - p);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, direction)));
                return output;
            }

            half4 ArenaShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ArenaVertex
            #pragma fragment ArenaDepthFragment
            #pragma multi_compile_instancing

            half4 ArenaDepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ArenaVertex
            #pragma fragment ArenaNormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            half4 ArenaNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                ArenaSurface authored = EvaluateSurface(input.positionWS, n);
                float3 normalWS = SurfaceNormal(input.positionWS, n, authored.height);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(normalWS) * .5 + .5);
                    return half4(PackFloat2To888(oct), 0);
                #else
                    return half4(normalWS, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

