using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct CWindPosId : IComponentData
{
    public int Id;
    public int3 Pos;
}

[Serializable]
public struct CWindTempData1 : IComponentData
{
    public float3 Value;
}

[Serializable]
public struct CWindTempData2 : IComponentData
{
    public float3 Value;
}