// BLACK FRIDAY — Stilize (toon) URP shader
// Yumuşak ışık rampası + renkli gölge tonu + stilize parlama + rim ışığı + fırça dokusu (triplanar) +
// zemine doğru hafif koyulaşma + yansıma probları + SSAO + Forward+ ek ışıklar ve gölgeler + ters kabuk dış hat.
Shader "BlackFriday/StylizedLit"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadeColor ("Shade Tint", Color) = (0.82,0.84,1,1)
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        _RampSmooth ("Ramp Smooth", Range(0.01,0.5)) = 0.16
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Metallic ("Metallic", Range(0,1)) = 0
        _SpecSize ("Spec Size", Range(0,1)) = 0.35
        _SpecStrength ("Spec Strength", Range(0,2)) = 0.5
        _RimColor ("Rim Color", Color) = (1,0.97,0.92,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.16
        _RimPower ("Rim Power", Range(0.5,8)) = 3.5
        _GradientStrength ("Ground Gradient", Range(0,1)) = 0.14
        _GradientHeight ("Gradient Height (m)", Float) = 1.8
        _NoiseTex ("Paint Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale", Float) = 0.45
        _NoiseStrength ("Noise Strength", Range(0,0.5)) = 0.07
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EnvStrength ("Env Reflection", Range(0,1)) = 1
        _OutlineColor ("Outline Color", Color) = (0.12,0.1,0.14,1)
        _OutlineWidth ("Outline Width (px)", Range(0,6)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _NoiseTex_ST;
            float4 _EmissionMap_ST;
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _RimColor;
            half4 _EmissionColor;
            half4 _OutlineColor;
            half _RampThreshold;
            half _RampSmooth;
            half _Smoothness;
            half _Metallic;
            half _SpecSize;
            half _SpecStrength;
            half _RimStrength;
            half _RimPower;
            half _GradientStrength;
            float _GradientHeight;
            float _NoiseScale;
            half _NoiseStrength;
            half _EnvStrength;
            half _OutlineWidth;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NoiseTex);     SAMPLER(sampler_NoiseTex);
        TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);
        ENDHLSL

        // ------------------------------------------------------------------ ana ışıklandırma
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes a)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_TRANSFER_INSTANCE_ID(a, o);
                VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Ramp(half ndl)
            {
                return smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl * 0.5h + 0.5h);
            }

            half3 StylizedLight(Light l, half3 n, half3 v, inout half3 spec)
            {
                half ndl = dot(n, l.direction);
                half atten = l.distanceAttenuation * l.shadowAttenuation;
                half r = Ramp(ndl) * atten;
                half3 h = SafeNormalize(l.direction + v);
                half nh = saturate(dot(n, h));
                half size = lerp(0.995h, 0.93h, _SpecSize);
                half s = smoothstep(size - 0.01h, size + 0.01h, nh) * r * _SpecStrength * _Smoothness;
                spec += l.color * s;
                return l.color * r;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;
                half3 n = normalize(i.normalWS);

                // fırça / boya dokusu (triplanar, dünya uzayı)
                float3 wp = i.positionWS * _NoiseScale;
                half3 bw = abs(n); bw /= max(bw.x + bw.y + bw.z, 1e-4h);
                half nz = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.xy).r * bw.z
                        + SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.xz).r * bw.y
                        + SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, wp.zy).r * bw.x;
                albedo *= 1.0h + (nz - 0.5h) * 2.0h * _NoiseStrength;

                // zemine yakın hafif koyulaşma (stilize temas gölgesi)
                half g = saturate(frac(i.positionWS.y / 5.3) * 5.3 / max(_GradientHeight, 0.01));
                albedo *= lerp(1.0h - _GradientStrength, 1.0h, g);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                half3 v = inputData.viewDirectionWS;

                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0h);

                half3 spec = 0;
                half3 ambient = SampleSH(n) * _ShadeColor.rgb * ao.indirectAmbientOcclusion;
                half3 direct = 0;

                Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
                mainLight.color *= ao.directAmbientOcclusion;
                direct += StylizedLight(mainLight, n, v, spec);

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(lightCount)
                    Light l = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                    direct += StylizedLight(l, n, v, spec);
                LIGHT_LOOP_END
                #endif

                half oneMinusMetal = 1.0h - _Metallic * 0.75h;
                half3 color = albedo * (ambient + direct) * oneMinusMetal;

                // yansıma (problar) — fresnel ile
                half3 r = reflect(-v, n);
                half nv = saturate(dot(n, v));
                half fres = pow(1.0h - nv, 4.0h);
                half3 env = GlossyEnvironmentReflection(r, inputData.positionWS, 1.0h - _Smoothness, ao.indirectAmbientOcclusion, inputData.normalizedScreenSpaceUV);
                half reflAmt = (lerp(0.04h, 0.9h, _Metallic) + fres * 0.6h) * _Smoothness * _EnvStrength;
                half3 envTint = lerp(half3(1, 1, 1), albedo, _Metallic);
                color += env * envTint * reflAmt;

                color += spec * lerp(half3(1, 1, 1), albedo, _Metallic);

                // rim ışığı (aydınlık tarafta daha belirgin)
                half rim = pow(1.0h - nv, _RimPower) * _RimStrength;
                color += rim * _RimColor.rgb * saturate(Luminance(direct + ambient) + 0.25h);

                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ dış hat (ters kabuk)
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="BFOutline" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex overt
            #pragma fragment ofrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct OA { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct OV { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            OV overt (OA a)
            {
                OV o;
                UNITY_SETUP_INSTANCE_ID(a);
                float4 cs = TransformObjectToHClip(a.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(a.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                nCS = len > 1e-5 ? nCS / len : float2(0, 0);
                cs.xy += nCS * (_OutlineWidth * 2.0 / _ScreenParams.xy) * cs.w;
                o.positionCS = cs;
                o.fogFactor = ComputeFogFactor(cs.z);
                return o;
            }

            half4 ofrag (OV i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ gölge yayma
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex svert
            #pragma fragment sfrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct SA { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 svert (SA a) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 posWS = TransformObjectToWorld(a.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(a.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, lightDir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return cs;
            }

            half4 sfrag () : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ derinlik
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex dvert
            #pragma fragment dfrag
            #pragma multi_compile_instancing
            struct DA { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 dvert (DA a) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(a); return TransformObjectToHClip(a.positionOS.xyz); }
            half dfrag () : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ derinlik + normal (SSAO)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex nvert
            #pragma fragment nfrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            struct NA { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct NV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            NV nvert (NA a)
            {
                NV o;
                UNITY_SETUP_INSTANCE_ID(a);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }
            half4 nfrag (NV i) : SV_Target
            {
                float3 normalWS = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normalWS);
                    float2 rem = saturate(oct * 0.5 + 0.5);
                    return half4(PackFloat2To888(rem), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
