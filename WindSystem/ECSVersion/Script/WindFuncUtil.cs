using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class WindFuncUtil
{
    #region 牺牲精度的原子计算转换

    private static readonly float FXDPT_SIZE = (float) (1 << 12);
    private static readonly float FXDPT_SIZE_R = 1.0f / (float) (1 << 12);
    public static float PackIntToFloat(int i)
    {
        return (float)(i * FXDPT_SIZE_R);
    }
    public static int PackFloatToInt(float f)
    {
        return (int)(f * FXDPT_SIZE);
    }

    public static float3 PackIntToFloat(int3 i)
    {
        return new float3(i.x * FXDPT_SIZE_R, i.y * FXDPT_SIZE_R, i.z * FXDPT_SIZE_R);
    }
    public static int3 PackFloatToInt(float3 f)
    {
        return (int3)(f * FXDPT_SIZE);
    }
    
    #endregion
    
    public static int3 WindPosDataIdToPos(int id)
    {
        int3 pos = int3.zero;
        pos.z = id / (WindMgrECS.WindRangeX * WindMgrECS.WindRangeY);
        pos.y = (id - pos.z * WindMgrECS.WindRangeX * WindMgrECS.WindRangeY) / WindMgrECS.WindRangeX;
        pos.x = id - pos.y * WindMgrECS.WindRangeX - pos.z * WindMgrECS.WindRangeX * WindMgrECS.WindRangeY;
        return pos;
    }
    public static int WindPosToPosDataId(int3 pos)
    {
        int id = pos.x + pos.y * WindMgrECS.WindRangeX + pos.z * WindMgrECS.WindRangeX * WindMgrECS.WindRangeY;
        return id;
    }

    public static bool UpdateWindMotorLifeTime(float DeltaTime, ref CWindMotorLifeTime lifeTime)
    {
        // 排除无效发动机
        if (lifeTime.Loop || lifeTime.LeftTime > 0)
        {
            // 对于生命周期循环的，剩余时间小于0后重置剩余时间
            if (lifeTime.Loop && lifeTime.LeftTime <= 0)
            {
                lifeTime.LeftTime = lifeTime.LifeTime;
            }
            
            //更新风力
            if (!lifeTime.Loop && lifeTime.LeftTime <= 0)
            {
                lifeTime.IsUnused = true;
                return false;
            }

            lifeTime.LeftTime -= DeltaTime;
            return true;
        }

        lifeTime.IsUnused = true;
        return false;
    }
    
    public static float LengthSq(float3 dir)
    {
        return math.dot(dir, dir);
    }

    public static float DistanceSq(float3 pos1, float3 pos2)
    {
        float3 dir = pos1 - pos2;
        return LengthSq(dir);
    }

    public static float RsqrtClamp(float n)
    {
        return math.min(math.rsqrt(n), 5);
    }
    
    public static void ApplyMotorDirectional(float3 cellPosWS, CWindMotorDirectional motor, ref float3 velocity)
    {
        float distanceSq = DistanceSq(cellPosWS, motor.PosWS);
        if (distanceSq < motor.RadiusSq)
        {
            velocity += motor.Velocity * motor.VelocityDir;
        }
    }

    public static void ApplyMotorOmni(float3 cellPosWS, CWindMotorOmni motor, ref float3 velocity)
    {
        float3 dir = cellPosWS - motor.PosWS;
        float distanceSq = LengthSq(dir);
        if (distanceSq < motor.RadiusSq)
        {
            velocity += dir * motor.Velocity * RsqrtClamp(distanceSq);
        }
    }

    public static void ApplyMotorVortex(float3 cellPosWS, CWindMotorVortex motor, ref float3 velocity)
    {
        float3 dir = cellPosWS - motor.PosWS;
        float distanceSq = LengthSq(dir);
        if (distanceSq < motor.Radius)
        {
            velocity += motor.Velocity * math.cross(motor.Axis, dir * RsqrtClamp(distanceSq));
        }
    }

    public static void ApplyMotorMoving(float3 cellPosWS, CWindMotorMoving motor, ref float3 velocity)
    {
        float3 dirPre = cellPosWS - motor.PrePosWS;
        float moveLen = math.dot(dirPre, motor.MoveDir);
        moveLen = math.min(math.max(0, moveLen), motor.MoveLen);
        float3 curPos = moveLen * motor.MoveDir + motor.PrePosWS;
        float3 dirCur = cellPosWS - curPos;
        float distanceSq = LengthSq(dirCur);
        if(distanceSq < motor.RadiusSq)
        {
            float3 blowDir = RsqrtClamp(distanceSq) * dirCur + motor.MoveDir;
            blowDir = math.normalize(blowDir);
            velocity += blowDir * motor.Velocity;
        }
    }
}
