Shader "Unlit/ScreenDepthRimLightDebug"
{
    Properties
    {
        _BaseMap("BaseMap", 2D) = "white"{}
        _BaseColor("BaseColor", Color) = (1, 1, 1, 1)
        _OffsetMul("OffsetMul", Float) = 0
        _Threshold("Threshold", Float) = 0
        _FresnelMask("Fresne lMask", Float) = 0
    }
    SubShader
    {
        Pass
        {
            Tags{"LightMode" = "UniversalForward"}
            HLSLPROGRAM
            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature _SPECULAR_SETUP
            #pragma shader_feature _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex DebugVertex
            #pragma fragment DebugFragment
            // 顶点着色器输入结构体
            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                // float4 vertexColor : COLOR;
            };
            // 片源着色器输入结构体
            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionVS : TEXCOORD1;
                float4 positionNDC : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                // float4 vertexColor : TEXCOORD5;

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD6;
                #endif
            };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _OffsetMul;
                half _Threshold;
                half _FresnelMask;
            CBUFFER_END

            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            float4 TransformHClipToViewPortPos(float4 positionCS)
            {
                float4 o = positionCS * 0.5f;
                o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
                o.zw = positionCS.zw;
                return o / o.w;
            }

            DepthVaryings DebugVertex(DepthAttributes v)
            {
                DepthVaryings o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;
                o.positionVS = vertexInput.positionVS;
                o.positionNDC = vertexInput.positionNDC;
                o.normalWS = normalInput.normalWS;
                o.viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
                o.uv = v.uv;
                return o;
            }

            half4 DebugFragment(DepthVaryings i) : SV_TARGET
            {
                 // 基本方向数据
                half3 viewDirWS = normalize(i.viewDirWS);
                half3 normalWS = normalize(i.normalWS);
                half3 viewDirVS = TransformWorldToViewDir(viewDirWS);
                half3 positionVS = i.positionVS;
                half3 normalVS = TransformWorldToViewDir(normalWS);
                float3 samplePositionVS = float3(positionVS.xy + normalVS.xy * _OffsetMul, positionVS.z); // 保持z不变（CS.w = -VS.z）
                float4 samplePositionCS = TransformWViewToHClip(samplePositionVS); // input.positionCS不是真正的CS 而是SV_Position屏幕坐标
                float4 samplePositionVP = TransformHClipToViewPortPos(samplePositionCS);

                float depth = i.positionNDC.z / i.positionNDC.w;
                float linearEyeDepth = LinearEyeDepth(depth, _ZBufferParams); // 离相机越近越小
                float offsetDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, samplePositionVP).r; // _CameraDepthTexture.r = input.positionNDC.z / input.positionNDC.w
                float linearEyeOffsetDepth = LinearEyeDepth(offsetDepth, _ZBufferParams);
                float one_depth = Linear01Depth(depth, _ZBufferParams);
                float depthDiff = linearEyeOffsetDepth - linearEyeDepth;
                float rimIntensity = step(_Threshold, depthDiff);

                float rimRatio = 1 - saturate(dot(viewDirWS, normalWS));
                rimRatio = pow(rimRatio, exp2(lerp(4.0, 0.0, _FresnelMask)));
                // return rimRatio;
                rimIntensity = lerp(0, rimIntensity, rimRatio);

                return lerp(float4(0, 0, 0, 1), float4(1, 1, 1, 1), rimIntensity);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard srp library
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        
    }
}