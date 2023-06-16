Shader "VolumeDebug/WindVolumeDebug"
{
    Properties
    {
        _Alpha ("Alpha", float) = 0.02
        _StepSize ("Step Size", float) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "LightMode"="UniversalForward"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VolumeDebugVertex
            #pragma fragment VolumeDebugFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 最大光线追踪样本数
            #define MAX_STEP_COUNT 128

            // 允许的浮点数误差
            #define EPSILON 0.00001f
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 objectVertex : TEXCOORD0;
                float3 vectorToSurface : TEXCOORD1;
            };

            uniform sampler3D _VolumeBuffer1;
            // float4 _VolumeBuffer1_ST;
            float _Alpha;
            float _StepSize;
            
            Varyings VolumeDebugVertex (Attributes v)
            {
                Varyings o;

                // 对象空间中的顶点将成为光线追踪的起点
                o.objectVertex = v.positionOS;

                // 计算世界空间中从摄像机到顶点的矢量
                float3 positionWS = TransformObjectToWorld(v.positionOS);
                o.vectorToSurface = positionWS - _WorldSpaceCameraPos;
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            float4 BlendUnder(float4 color, float4 newColor)
            {
                color.rgb += (1.0 - color.a) * newColor.a * newColor.rgb;
                color.a += (1.0 - color.a) * newColor.a;
                return color;
            }

            half4 VolumeDebugFragment(Varyings i) : SV_Target
            {
                // 开始在对象的正面进行光线追踪
                float3 rayOrigin = i.objectVertex;

                // 使用摄像机到对象表面的矢量获取射线方向
                float3 rayDirection = mul(unity_WorldToObject, float4(normalize(i.vectorToSurface), 1));

                float4 color = float4(0, 0, 0, 0);
                float3 samplePosition = rayOrigin;

                // 穿过对象空间进行光线追踪
                for (int i = 0; i < MAX_STEP_COUNT; i++)
                {
                    // 仅在单位立方体边界内累积颜色
                    if(max(abs(samplePosition.x), max(abs(samplePosition.y), abs(samplePosition.z))) < 0.5f + EPSILON)
                    {
                        float4 sampledColor = tex3D(_VolumeBuffer1, samplePosition + float3(0.5f, 0.5f, 0.5f));
                        sampledColor.a *= _Alpha;
                        color = BlendUnder(color, sampledColor);
                        samplePosition += rayDirection * _StepSize;
                    }
                }

                return color;
            }
            ENDHLSL
        }
    }
}
