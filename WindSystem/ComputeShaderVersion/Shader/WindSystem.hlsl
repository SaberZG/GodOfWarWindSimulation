#ifndef WIND_SYSTEM_INCLUDED
#define WIND_SYSTEM_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Computer Shader
TEXTURE3D(WindVelocityData);
SAMPLER(sampler_WindVelocityData);
// ECS
TEXTURE3D(WindDataBuffer);
SAMPLER(sampler_WindDataBuffer);

TEXTURE3D(WindNoise);
SAMPLER(sampler_WindNoise);

uniform float OverallPower;
uniform float4 VolumeSize;
uniform float4 VolumePosOffset;
uniform float3 GlobalAmbientWind;
uniform float3 WindNoiseRcpTexSize;
uniform float3 WindNoiseOffset;
uniform float3 WindNoiseUVScale;
uniform float3 WindNoiseScale;

#define WindPIMax 2.513274122871834590768 // 0.8 * PI

// 这段逻辑大可瞎吉儿改，只要效果能满足表现就好
void GetWindSimulationFunc(float3 positionOS, float3 normalWS, inout float3 positionWS, out float3 windData)
{
    /// 采样计算好的全局风力
    float3 uvw = (positionWS - VolumePosOffset.xyz) / VolumeSize.xyz;
    
    // 这个是ComputeShader版本下要做的风场纹理采样。请原谅我把这个变量写的如此丑陋，导致还要这样注释来调试
    // windData = SAMPLE_TEXTURE3D_LOD(WindVelocityData, sampler_WindVelocityData, uvw, 0);
    // 这个是ECS版本的风场纹理采样
    windData = SAMPLE_TEXTURE3D_LOD(WindDataBuffer, sampler_WindDataBuffer, uvw, 0);
    
    // 当这个uvw在0~1范围内时，此处的分量就会为0，在这之外就会有一个长度
    // 可以用来限制wind volume外的风力信息，进行一个小范围的衰减，而不是直接切掉
    uvw = max(0, abs(uvw - 0.5) - 0.5);
    half uvwLen = length(uvw) * 10.0;
    half fadeDis = saturate(1 - uvwLen);
    float3 ambientWind = GlobalAmbientWind.xyz;
    float3 ambientWindUV = (positionWS + WindNoiseOffset) * WindNoiseRcpTexSize * WindNoiseUVScale;
    float3 windNoise = SAMPLE_TEXTURE3D_LOD(WindNoise, sampler_WindNoise, ambientWindUV, 0).xyz;
    windNoise = windNoise * 2.0 - 1.0;
    ambientWind += windNoise * WindNoiseScale;
    float3 windFinal = windData * fadeDis + ambientWind;
    windFinal *= OverallPower;
    float windStrength = length(windFinal);

    ///Todo : rad需要做限制，目前的Rad会处于周期变换
    float rad = clamp(windStrength * PI * 0.9, -WindPIMax, WindPIMax) / 2.0;
    float x, y;  //弯曲后,x为单位球在wind方向计量，y为grassUp方向计量
    sincos(rad, x, y);
    //得到wind与normalWS的正交向量
    float3 windDir = SafeNormalize(windFinal - dot(windFinal, normalWS) * normalWS);
    //offset表示grassUpWS这个位置的顶点，在风力作用下，会偏移到windedPos位置
    float3 windedPos = x * windDir + y * normalWS;
    // windedPos.y = max(windedPos, 0);
    float3 posOffset = (windedPos-normalWS) * positionOS.y;
    positionWS += posOffset;
    // positionWS -= 0.001;
}

#endif