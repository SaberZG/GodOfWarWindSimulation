#ifndef WIND_SIMULATION_INCLUDED
#define WIND_SIMULATION_INCLUDED

#define FXDPT_SIZE (float)(1 << 12)
#define FXDPT_SIZE_R 1.0 / (float)(1 << 12)
#define BlurWeightN 0.04f
#define BlurWeightC 0.4f
#define StorgeLimit 0.0001f

struct MotorDirectional
{
    float3 position;
    float radiusSq;
    float3 force;
};
struct MotorOmni
{
    float3 position;
    float radiusSq;
    float force;
};
struct MotorVortex
{
    float3 position;
    float3 axis;
    float radiusSq;
    float force;
};
struct MotorMoving
{
    float3 prePosition;
    float moveLen;
    float3 moveDir;
    float radiusSq;
    float force;
};

void AtomicAdd(uniform RWTexture3D<int> rwTex, uniform uint3 rwTexSize, in uint3 coord, in float value)
{
    if(all(coord < rwTexSize))
    {
        InterlockedAdd(rwTex[coord], (int)(value * FXDPT_SIZE));
    }
}

float LengthSq(float3 dir)
{
    return dot(dir, dir);
}

float DistanceSq(float3 pos1, float3 pos2)
{
    float3 dir = pos1 - pos2;
    return LengthSq(dir);
}

float PackIntToFloat(int i)
{
    return (float)(i * FXDPT_SIZE_R);
}
int PackFloatToInt(float f)
{
    return (int)(f * FXDPT_SIZE);
}

float3 PackIntToFloat(int3 i)
{
    return (float3)(i * FXDPT_SIZE_R);
}
int3 PackFloatToInt(float3 f)
{
    return (int3)(f * FXDPT_SIZE);
}

void ApplyMotorDirectional(float3 cellPosWS, MotorDirectional motor, inout float3 velocityWS)
{
    float distanceSq = DistanceSq(cellPosWS, motor.position);
    if (distanceSq < motor.radiusSq)
    {
        velocityWS += motor.force;
    }
}

void ApplyMotorOmni(float3 cellPosWS, MotorOmni motor, inout float3 velocityWS)
{
    float3 dir = cellPosWS - motor.position;
    float distanceSq = LengthSq(dir);
    if (distanceSq < motor.radiusSq)
    {
        velocityWS += dir * motor.force * min(rsqrt(distanceSq), 5);
    }
}

void ApplyMotorVortex(float3 cellPosWS, MotorVortex motor, inout float3 velocityWS)
{
    float3 dir = cellPosWS - motor.position;
    float distanceSq = LengthSq(dir);
    if (distanceSq < motor.radiusSq)
    {
        velocityWS += motor.force * cross(motor.axis, dir * rsqrt(distanceSq));
    }
}

void ApplyMotorMoving(float3 cellPosWS, MotorMoving motor, inout float3 velocityWS)
{
    float3 dirPre = cellPosWS - motor.prePosition;
    float moveLen = dot(dirPre, motor.moveDir);
    moveLen = min(max(0, moveLen), motor.moveLen);
    float3 curPos = moveLen * motor.moveDir + motor.prePosition;
    float3 dirCur = cellPosWS - curPos;
    float distanceSq = LengthSq(dirCur);
    if(distanceSq < motor.radiusSq)
    {
        float3 blowDir = rsqrt(distanceSq) * dirCur + motor.moveDir;
        blowDir = normalize(blowDir);
        velocityWS += blowDir * motor.force;
    }
}

#endif
