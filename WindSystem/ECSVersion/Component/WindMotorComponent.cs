using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct CWindMotorLifeTime : IComponentData
{
    /// <summary>
    /// 发动机创建时间
    /// </summary>
    public float CreateTime;
    
    /// <summary>
    /// 发动机是否循环播放，循环播放的话就不会销毁，持续有效
    /// </summary>
    public bool Loop;
    
    /// <summary>
    /// 发动机生命周期
    /// </summary>
    public float LifeTime;
    
    /// <summary>
    /// 生命周期内的剩余时间
    /// </summary>
    public float LeftTime;
    
    /// <summary>
    /// 用于标记此发动机是否需要回收
    /// </summary>
    public bool IsUnused;
}

[Serializable]
public struct CWindMotorDirectional : IComponentData
{
    public int id;
    public float3 PosWS;
    public float Radius;
    public float RadiusSq;
    public float Velocity;
    public float3 VelocityDir;
}

[Serializable]
public struct CWindMotorOmni : IComponentData
{
    public int id;
    public float3 PosWS;
    public float Radius;
    public float RadiusSq;
    public float Velocity;
}

[Serializable]
public struct CWindMotorVortex : IComponentData
{
    public int id;
    public float3 PosWS;
    public float3 Axis;
    public float Radius;
    public float RadiusSq;
    public float Velocity;
}

[Serializable]
public struct CWindMotorMoving : IComponentData
{
    public int id;
    public float3 PrePosWS;
    public float3 PosWS;
    public float MoveLen;
    public float3 MoveDir;
    public float Radius;
    public float RadiusSq;
    public float Velocity;
}