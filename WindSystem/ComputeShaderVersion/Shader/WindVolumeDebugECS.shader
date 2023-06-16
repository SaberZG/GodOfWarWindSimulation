Shader "VolumeDebug/WindVolumeDebugECS"
{
    Properties
    {

    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "LightMode"="UniversalForward"}

        Pass
        {
            HLSLPROGRAM
            #pragma vertex VolumeDebugVertex
            #pragma fragment VolumeDebugFragment
            #define FXDPT_SIZE (float)(1 << 12)
            #define FXDPT_SIZE_R 1.0 / (float)(1 << 12)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            uniform int WindRangeX;
			uniform int WindRangeY;
			uniform int WindRangeZ;
            TEXTURE3D(WindDataBuffer);
            SAMPLER(sampler_WindDataBuffer);
			uniform float3 OffsetPos;
            
            Varyings VolumeDebugVertex (Attributes v)
            {
                Varyings o;
                
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 VolumeDebugFragment(Varyings i) : SV_Target
            {
                float3 uvw = (i.positionWS - OffsetPos.xyz) / float3(WindRangeX, WindRangeY, WindRangeZ);
                float3 windVelocity = SAMPLE_TEXTURE3D(WindDataBuffer, sampler_WindDataBuffer, uvw);
                return half4(windVelocity, 0.0);
            }
            ENDHLSL
        }
    }
}
