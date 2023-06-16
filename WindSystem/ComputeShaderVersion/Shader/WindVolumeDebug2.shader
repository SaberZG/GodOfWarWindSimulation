Shader "VolumeDebug/WindVolumeDebug2"
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

            uniform Texture3D<int> WindBufferChannelR2;
            uniform Texture3D<int> WindBufferChannelG2;
            uniform Texture3D<int> WindBufferChannelB2;
            TEXTURE3D(WindVelocityData);
            SAMPLER(sampler_WindVelocityData);
            uniform float4 VolumeSize;
            uniform float4 VolumePosOffset;
            
            Varyings VolumeDebugVertex (Attributes v)
            {
                Varyings o;
                
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 VolumeDebugFragment(Varyings i) : SV_Target
            {
                float3 uvw = (i.positionWS - VolumePosOffset.xyz) / VolumeSize.xyz;
                // return half4(uvw, 0.0);
                int3 uvwInt = round(i.positionWS  - 0.51f - VolumePosOffset.xyz);
                // float volumeColorR = (float)(WindBufferChannelR2[uvwInt].r * FXDPT_SIZE_R);
                // float volumeColorG = (float)(WindBufferChannelG2[uvwInt].r * FXDPT_SIZE_R);
                // float volumeColorB = (float)(WindBufferChannelB2[uvwInt].r * FXDPT_SIZE_R);
                // return half4(volumeColorR, volumeColorG, volumeColorB, 0.0);
                float3 windVelocity = SAMPLE_TEXTURE3D(WindVelocityData, sampler_WindVelocityData, uvw);
                return half4(windVelocity, 0.0);
            }
            ENDHLSL
        }
    }
}
