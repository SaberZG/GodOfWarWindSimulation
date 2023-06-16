Shader "URPLearn/Grass"
{
    Properties
    {

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,0)
        _NoiseMap("WaveNoiseMap", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        // Universal Pipeline tag is required. If Universal render pipeline is not set in the graphics settings
        // this Subshader will fail. One can add a subshader below or fallback to Standard built-in to make this
        // material work with both Universal Render Pipeline and Builtin Unity Pipeline
        Tags{"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass. Shades all light in a single pass. GI + emission + Fog
        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}

            ZWrite On
            ZTest On
            Cull Off

            HLSLPROGRAM
            // Required to compile gles 2.0 with standard SRP library
            // All shaders must be compiled with HLSLcc and currently only gles is not using HLSLcc by default
            #pragma target 4.5
            
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/WindSystem/ComputeShaderVersion/Shader/WindSystem.hlsl"

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature _RECEIVE_SHADOWS_OFF
            #pragma multi_compile _ _ENABLE_WIND_SYSTEM _ENABLE_WIND_SYSTEM_FULL

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup


            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float2 uv           : TEXCOORD0;
                float4 positionCS   : SV_POSITION;
                float3 normalWS    : TEXCOORD1;
                float4 positionWS   : TEXCOORD2;
                float3 windData : TEXCOORD3;
            };

            #pragma vertex PassVertex
            #pragma fragment PassFragment

            float _Cutoff;
            half4 _BaseColor;
            
            TEXTURE2D_X(_BaseMap);
            SAMPLER(sampler_BaseMap);
            sampler2D _NoiseMap;

            float4x4 _TerrianLocalToWorld;
            float2 _GrassQuadSize;

            #define StormFront _StormParams.x
            #define StormMiddle _StormParams.y
            #define StormEnd _StormParams.z
            #define StormSlient _StormParams.w

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                struct GrassInfo{
                    float4x4 localToTerrian;
                    float4 texParams;
                };
                StructuredBuffer<GrassInfo> _GrassInfos;
            #endif

            void setup(){
            }

            Varyings PassVertex(Attributes input)
            {
                Varyings output;
                float2 uv = input.uv;
                float3 positionOS = input.positionOS;
                float3 normalOS = input.normalOS;
                uint instanceID = input.instanceID;
                positionOS.xy = positionOS.xy * _GrassQuadSize;

                float3 grassUpDir = float3(0,1,0);

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                    GrassInfo grassInfo = _GrassInfos[instanceID];

                    //将顶点和法线从Quad本地空间转换到Terrian本地空间
                    positionOS = mul(grassInfo.localToTerrian,float4(positionOS,1)).xyz;
                    normalOS = mul(grassInfo.localToTerrian,float4(normalOS,0)).xyz;
                    grassUpDir = mul(grassInfo.localToTerrian,float4(grassUpDir,0)).xyz;

                    //UV偏移缩放
                    uv = uv * grassInfo.texParams.xy + grassInfo.texParams.zw;

                #endif
                float4 positionWS = mul(_TerrianLocalToWorld,float4(positionOS,1));
                positionWS /= positionWS.w;
                grassUpDir = normalize(mul(_TerrianLocalToWorld,float4(grassUpDir,0)));
                
                // positionWS.xyz = applyWind(positionWS.xyz,grassUpDir,windDir,windStrength,localVertexHeight,instanceID);
                float3 windData;
                GetWindSimulationFunc(positionOS, grassUpDir, positionWS.xyz, windData);
                
                output.uv = uv;
                output.positionWS = positionWS;
                output.positionCS = mul(UNITY_MATRIX_VP,positionWS);
                output.normalWS = mul(unity_ObjectToWorld, float4(normalOS, 0.0 )).xyz;
                output.windData = windData;
                return output;
            }

            half4 PassFragment(Varyings input) : SV_Target
            {
                half4 diffuseColor = SAMPLE_TEXTURE2D_X(_BaseMap,sampler_BaseMap,input.uv);
                if(diffuseColor.a < _Cutoff){
                    discard;
                    return 0 ;
                }
                //计算光照和阴影，光照采用Lembert Diffuse.
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 lightColor = mainLight.color;
                float3 normalWS = input.normalWS;
                float4 color = float4(1,1,1,0);
                float minDotLN = 0.2;
                color.rgb = max(minDotLN,abs(dot(lightDir,normalWS))) * lightColor * diffuseColor.rgb * _BaseColor.rgb * mainLight.shadowAttenuation;
                return color;
            }

            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
