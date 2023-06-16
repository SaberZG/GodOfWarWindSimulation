using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(TransformSystemGroup))]
public partial class SWindSimulation : SystemBase
{
    //风力数据实体
    private EntityQuery tempDataEntityQuery;
    //风力发动机实体
    private EntityQuery motorDirectianalQuery;
    private EntityQuery motorOmniQuery;
    private EntityQuery motorVortexQuery;
    private EntityQuery motorMovingQuery;

    private JobHandle preSimulationHandle;
    private NativeArray<Color> preColorDataArray;
    private bool preHandleInit = false;

    protected override void OnCreate()
    {
        tempDataEntityQuery = GetEntityQuery(
            ComponentType.ReadOnly<CWindPosId>(),
            ComponentType.ReadWrite<CWindTempData1>(),
            ComponentType.ReadWrite<CWindTempData2>()
        );
        motorDirectianalQuery = GetEntityQuery(
            ComponentType.ReadWrite<CWindMotorDirectional>(),
            ComponentType.ReadWrite<CWindMotorLifeTime>()
        );
        motorOmniQuery = GetEntityQuery(
            ComponentType.ReadWrite<CWindMotorOmni>(),
            ComponentType.ReadWrite<CWindMotorLifeTime>()
        );
        motorVortexQuery = GetEntityQuery(
            ComponentType.ReadWrite<CWindMotorVortex>(),
            ComponentType.ReadWrite<CWindMotorLifeTime>()
        );
        motorMovingQuery = GetEntityQuery(
            ComponentType.ReadWrite<CWindMotorMoving>(),
            ComponentType.ReadWrite<CWindMotorLifeTime>()
        );
    }
    
    [BurstCompile]
    partial struct Job_CopyWindTempData2 : IJobEntity
    {
        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<float3> copyArray;

        public void Execute(in CWindTempData2 tempData2, in CWindPosId posId)
        {
            copyArray[posId.Id] = tempData2.Value;
        }
    }
    
    [BurstCompile]
    partial struct Job_ShiftWindData : IJobFor
    {
        [NoAlias] public int3 ShiftPos;
        [NoAlias] public int3 VolumeSizeMinusOne;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<float3> tempData1Array;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<float3> tempData2Array;
        
        public void Execute(int index)
        {
            int3 localPos = WindFuncUtil.WindPosDataIdToPos(index);
            int3 shiftPos = ShiftPos + localPos;
            int3 finalPos = math.max(math.min(shiftPos, VolumeSizeMinusOne), int3.zero);
            int finalIndex = WindFuncUtil.WindPosToPosDataId(finalPos);
            tempData1Array[index] = tempData2Array[finalIndex];
        }
    }
    
    /// <summary>
    /// 风力扩散模拟
    /// </summary>
    [BurstCompile]
    partial struct Job_UpdateDiffusion : IJobFor
    {
        [NoAlias] public int3 VolumeSizeMinusOne;
        [NoAlias] public int tempData1ArrayLength;
        [NoAlias] public float DiffusionVelocity;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<float3> tempData1Array;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<float3> tempData2Array;
        
        public void Execute(int index)
        {
            int3 localPos = WindFuncUtil.WindPosDataIdToPos(index);

            float3 velocity = tempData1Array[index];
            
            float3 xr = float3.zero;
            float3 xl = float3.zero;
            float3 yr = float3.zero;
            float3 yl = float3.zero;
            float3 zr = float3.zero;
            float3 zl = float3.zero;
            // x
            if(localPos.x < VolumeSizeMinusOne.x)
            {
                int3 gtID = localPos + new int3(1, 0, 0);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    xr = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(VolumeSizeMinusOne.x, localPos.y, localPos.z);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    xr = tempData1Array[targetId];
                }
            }
            if(localPos.x > 0)
            {
                int3 gtID = localPos + new int3(-1, 0, 0);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    xl = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(0, localPos.y, localPos.z);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    xl = tempData1Array[targetId];
                }
            }
            // y
            if(localPos.y < VolumeSizeMinusOne.y)
            {
                int3 gtID = localPos + new int3(0, 1, 0);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    yr = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(localPos.x, VolumeSizeMinusOne.y, localPos.z);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    yr = tempData1Array[targetId];
                }
            }
            if(localPos.y > 0)
            {
                int3 gtID = localPos + new int3(0, -1, 0);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    yl = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(localPos.x, 0, localPos.z);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    yl = tempData1Array[targetId];
                }
            }
            // z
            if(localPos.z < VolumeSizeMinusOne.z)
            {
                int3 gtID = localPos + new int3(0, 0, 1);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    zr = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(localPos.x, localPos.y, VolumeSizeMinusOne.z);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    zr = tempData1Array[targetId];
                }
            }
            if(localPos.z > 0)
            {
                int3 gtID = localPos + new int3(0, 0, -1);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    zl = tempData1Array[targetId];
                }
            }
            else
            {
                int3 gtID = new int3(localPos.x, localPos.y, 0);
                int targetId = WindFuncUtil.WindPosToPosDataId(gtID);
                if (tempData1ArrayLength >= targetId)
                {
                    zl = tempData1Array[targetId];
                }
            }

            float3 finalData = xr + xl + yr + yl + zr + zl - velocity * 6;
            finalData = finalData * DiffusionVelocity + velocity;
            tempData2Array[index] = finalData;
        }
    }

    [BurstCompile]
    partial struct Job_UpdateWindDirectionalMotor : IJobEntity
    {
        [NoAlias] public float DeltaTime;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<CWindMotorDirectional> copyArray;

        public void Execute(ref CWindMotorDirectional motor, ref CWindMotorLifeTime lifeTime)
        {
            bool updateVelocity = WindFuncUtil.UpdateWindMotorLifeTime(DeltaTime, ref lifeTime);
            if (updateVelocity)
            {
                // todo 通过ECS AnimationCurve 更新radius和force重新缓存
                motor.RadiusSq = motor.Radius * motor.Radius;
            }
            else
            {
                motor.Velocity = 0;
            }

            copyArray[motor.id] = motor;
        }
    }
    [BurstCompile]
    partial struct Job_UpdateWindOmniMotor : IJobEntity
    {
        [NoAlias] public float DeltaTime;
        [NoAlias][NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<CWindMotorOmni> copyArray;

        public void Execute(ref CWindMotorOmni motor, ref CWindMotorLifeTime lifeTime)
        {
            bool updateVelocity = WindFuncUtil.UpdateWindMotorLifeTime(DeltaTime, ref lifeTime);
            if (updateVelocity)
            {
                // todo 通过ECS AnimationCurve 更新radius和force重新缓存
                motor.RadiusSq = motor.Radius * motor.Radius;
            }
            else
            {
                motor.Velocity = 0;
            }

            copyArray[motor.id] = motor;
        }
    }
    [BurstCompile]
    partial struct Job_UpdateWindVortexMotor : IJobEntity
    {
        [NoAlias] public float DeltaTime;
        [NoAlias][NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<CWindMotorVortex> copyArray;

        public void Execute(ref CWindMotorVortex motor, ref CWindMotorLifeTime lifeTime)
        {
            bool updateVelocity = WindFuncUtil.UpdateWindMotorLifeTime(DeltaTime, ref lifeTime);
            if (updateVelocity)
            {
                // todo 通过ECS AnimationCurve 更新radius和force重新缓存
                motor.RadiusSq = motor.Radius * motor.Radius;
            }
            else
            {
                motor.Velocity = 0;
            }

            copyArray[motor.id] = motor;
        }
    }
    [BurstCompile]
    partial struct Job_UpdateWindMovingMotor : IJobEntity
    {
        [NoAlias] public float DeltaTime;
        [NoAlias][NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<CWindMotorMoving> copyArray;

        public void Execute(ref CWindMotorMoving motor, ref CWindMotorLifeTime lifeTime)
        {
            bool updateVelocity = WindFuncUtil.UpdateWindMotorLifeTime(DeltaTime, ref lifeTime);
            if (updateVelocity)
            {
                // todo 通过ECS AnimationCurve 更新radius和force重新缓存
                motor.RadiusSq = motor.Radius * motor.Radius;
            }
            else
            {
                motor.Velocity = 0;
            }

            copyArray[motor.id] = motor;
        }
    }
    
    [BurstCompile]
    partial struct Job_UpdateWindPosForce : IJobFor
    {
        [NoAlias] public float3 OffsetPos;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<CWindMotorDirectional> motorDirectionalArray;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<CWindMotorOmni> motorOmniArray;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<CWindMotorVortex> motorVortexArray;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<CWindMotorMoving> motorMovingArray;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float3> tempData1Array;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly]
        public NativeArray<float3> tempData2Array;
        public void Execute(int index)
        {
            //累加当前位置下的风力大小
            float3 windVelocity = tempData2Array[index];
            float3 posWS = WindFuncUtil.WindPosDataIdToPos(index) + OffsetPos;
            for (int i = 0; i < motorDirectionalArray.Length; i++)
            {
                WindFuncUtil.ApplyMotorDirectional(posWS, motorDirectionalArray[i], ref windVelocity);
            }
            for (int i = 0; i < motorOmniArray.Length; i++)
            {
                WindFuncUtil.ApplyMotorOmni(posWS, motorOmniArray[i], ref windVelocity);
            }
            for (int i = 0; i < motorVortexArray.Length; i++)
            {
                WindFuncUtil.ApplyMotorVortex(posWS, motorVortexArray[i], ref windVelocity);
            }
            for (int i = 0; i < motorMovingArray.Length; i++)
            {
                WindFuncUtil.ApplyMotorMoving(posWS, motorMovingArray[i], ref windVelocity);
            }
            // 将计算后的风力数据写道temp1中
            tempData1Array[index] = windVelocity;
        }
    }
    
    // 初始化原子加法数据
    [BurstCompile]
    partial struct Job_InitBufferInt : IJobFor
    {
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<int> initArray1;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<int> initArray2;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<int> initArray3;
        [NoAlias] public int defaultNum;
        
        public void Execute(int index)
        {
            initArray1[index] = defaultNum;
            initArray2[index] = defaultNum;
            initArray3[index] = defaultNum;
        }
    }
    
    [BurstCompile]
    partial struct Job_CopyBufferIntToFloat3 : IJobFor
    {
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputX;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputY;
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputZ;
        [NoAlias] [NativeDisableParallelForRestriction] [WriteOnly]
        public NativeArray<float3> dataOutput;

        public void Execute(int index)
        {
            float x = WindFuncUtil.PackIntToFloat(dataInputX[index]);
            float y = WindFuncUtil.PackIntToFloat(dataInputY[index]);
            float z = WindFuncUtil.PackIntToFloat(dataInputZ[index]);
            dataOutput[index] = new float3(x, y, z);
        }
    }

    /// <summary>
    /// 平流模拟，这个方法用于模拟风从当前位置依照风力方向扩散到其他位置
    /// </summary>
    [BurstCompile]
    partial struct Job_UpdateAdvectionWindData : IJobFor
    {
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<float3> dataInput;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayX;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayY;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayZ;

        [NoAlias] public float AdvectionVelocity;
        [NoAlias] public int3 VolumeSizeMinusSize;
        public void Execute(int index)
        {
            int3 pos = WindFuncUtil.WindPosDataIdToPos(index);
            float3 targetData = dataInput[index];
            float3 advectionData = targetData * AdvectionVelocity;

            float3 offsetNeb = math.frac(advectionData);
            float3 offsetOri = 1.0f - offsetNeb;

            int3 moveCell = (int3) (math.floor(advectionData + pos));
            if (math.all(moveCell >= 0 & moveCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetOri.x * offsetOri.y * offsetOri.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(moveCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }

            int3 tempCell = moveCell + new int3(1, 0, 0);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetNeb.x * offsetOri.y * offsetOri.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(0, 1, 0);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetOri.x * offsetNeb.y * offsetOri.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(1, 1, 0);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetNeb.x * offsetNeb.y * offsetOri.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(0, 0, 1);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetOri.x * offsetOri.y * offsetNeb.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(1, 0, 1);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetNeb.x * offsetOri.y * offsetNeb.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(0, 1, 1);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetOri.x * offsetNeb.y * offsetNeb.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
            
            tempCell = moveCell + new int3(1, 1, 1);
            if (math.all(tempCell >= 0 & tempCell <= VolumeSizeMinusSize))
            {
                float3 adData = offsetNeb.x * offsetNeb.y * offsetNeb.z * targetData;
                int id = WindFuncUtil.WindPosToPosDataId(tempCell);
                int3 adDataInt = WindFuncUtil.PackFloatToInt(adData);
                Interlocked.Add(ref interlockArrayX.GetRef(id), adDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id), adDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id), adDataInt.z);
            }
        }
    }
    
    /// <summary>
    /// 反向平流模拟。用于模拟其他位置的风力扩散到目标风格子
    /// 平流模拟和反向平流模拟本来我有个更好的方案，碍于时间问题懒得继续深究下去，未来如果有机会再完善好了（咕咕咕咕咕
    /// </summary>
    [BurstCompile]
    partial struct Job_UpdateReverseAdvectionWindData : IJobFor
    {
        [NoAlias] [NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<float3> dataInput;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayX;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayY;
        [NoAlias] [NativeDisableParallelForRestriction]
        public NativeArray<int> interlockArrayZ;

        [NoAlias] public float AdvectionVelocity;
        [NoAlias] public int3 VolumeSizeMinusSize;

        public void Execute(int index)
        {
            float3 tempData1 = dataInput[index];
            int3 pos = WindFuncUtil.WindPosDataIdToPos(index);
            float3 advectionData = tempData1 * -AdvectionVelocity;
            float3 offsetNeb = math.frac(advectionData);
            float3 offsetOri = 1.0f - offsetNeb;
            
            float3 targetData = float3.zero;
            float3 targetDataX1 = float3.zero;
            float3 targetDataY1 = float3.zero;
            float3 targetDataX1Y1 = float3.zero;
            float3 targetDataZ1 = float3.zero;
            float3 targetDataX1Z1 = float3.zero;
            float3 targetDataY1Z1 = float3.zero;
            float3 targetDataX1Y1Z1 = float3.zero;

            int3 moveCell = (int3) math.floor(advectionData + pos);
            int id0 = WindFuncUtil.WindPosToPosDataId(math.clamp(moveCell, int3.zero, VolumeSizeMinusSize));
            targetData = dataInput[id0];
            targetData *= offsetOri.x * offsetOri.y * offsetOri.z;

            int3 tempPos1 = moveCell + new int3(1, 0, 0);
            int id1 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos1, int3.zero, VolumeSizeMinusSize));
            targetDataX1 = dataInput[id1];
            targetDataX1 *= offsetNeb.x * offsetOri.y * offsetOri.z;
            
            int3 tempPos2 = moveCell + new int3(0, 1, 0);
            int id2 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos2, int3.zero, VolumeSizeMinusSize));
            targetDataY1 = dataInput[id2];
            targetDataY1 *= offsetOri.x * offsetNeb.y * offsetOri.z;
            
            int3 tempPos3 = moveCell + new int3(1, 1, 0);
            int id3 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos3, int3.zero, VolumeSizeMinusSize));
            targetDataX1Y1 = dataInput[id3];
            targetDataX1Y1 *= offsetNeb.x * offsetNeb.y * offsetOri.z;
            
            int3 tempPos4 = moveCell + new int3(0, 0, 1);
            int id4 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos4, int3.zero, VolumeSizeMinusSize));
            targetDataZ1 = dataInput[id4];
            targetDataZ1 *= offsetOri.x * offsetOri.y * offsetNeb.z;
            
            int3 tempPos5 = moveCell + new int3(1, 0, 1);
            int id5 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos5, int3.zero, VolumeSizeMinusSize));
            targetDataX1Z1 = dataInput[id5];
            targetDataX1Z1 *= offsetNeb.x * offsetOri.y * offsetNeb.z;
            
            int3 tempPos6 = moveCell + new int3(0, 1, 1);
            int id6 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos6, int3.zero, VolumeSizeMinusSize));
            targetDataY1Z1 = dataInput[id6];
            targetDataY1Z1 *= offsetOri.x * offsetNeb.y * offsetNeb.z;
            
            int3 tempPos7 = moveCell + new int3(1, 1, 1);
            int id7 = WindFuncUtil.WindPosToPosDataId(math.clamp(tempPos7, int3.zero, VolumeSizeMinusSize));
            targetDataX1Y1Z1 = dataInput[id7];
            targetDataX1Y1Z1 *= offsetNeb.x * offsetNeb.y * offsetNeb.z;

            if (math.all(moveCell >= 0 & moveCell <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetData);
                Interlocked.Add(ref interlockArrayX.GetRef(id0), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id0), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id0), -targetDataInt.z);
            }
            
            if (math.all(tempPos1 >= 0 & tempPos1 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataX1);
                Interlocked.Add(ref interlockArrayX.GetRef(id1), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id1), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id1), -targetDataInt.z);
            }
            
            if (math.all(tempPos2 >= 0 & tempPos2 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataY1);
                Interlocked.Add(ref interlockArrayX.GetRef(id2), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id2), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id2), -targetDataInt.z);
            }
            
            if (math.all(tempPos3 >= 0 & tempPos3 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataX1Y1);
                Interlocked.Add(ref interlockArrayX.GetRef(id3), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id3), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id3), -targetDataInt.z);
            }
            
            if (math.all(tempPos4 >= 0 & tempPos4 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataZ1);
                Interlocked.Add(ref interlockArrayX.GetRef(id4), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id4), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id4), -targetDataInt.z);
            }
            
            if (math.all(tempPos5 >= 0 & tempPos5 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataX1Z1);
                Interlocked.Add(ref interlockArrayX.GetRef(id5), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id5), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id5), -targetDataInt.z);
            }
            
            if (math.all(tempPos6 >= 0 & tempPos6 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataY1Z1);
                Interlocked.Add(ref interlockArrayX.GetRef(id6), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id6), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id6), -targetDataInt.z);
            }
            
            if (math.all(tempPos7 >= 0 & tempPos7 <= VolumeSizeMinusSize))
            {
                int3 targetDataInt = WindFuncUtil.PackFloatToInt(targetDataX1Y1Z1);
                Interlocked.Add(ref interlockArrayX.GetRef(id7), -targetDataInt.x);
                Interlocked.Add(ref interlockArrayY.GetRef(id7), -targetDataInt.y);
                Interlocked.Add(ref interlockArrayZ.GetRef(id7), -targetDataInt.z);
            }

            float3 cellData = targetData + targetDataX1 + targetDataY1 + targetDataX1Y1 + targetDataZ1
                              + targetDataX1Z1 + targetDataY1Z1 + targetDataX1Y1Z1;
            int3 cellDataInt = WindFuncUtil.PackFloatToInt(cellData);
            Interlocked.Add(ref interlockArrayX.GetRef(index), cellDataInt.x);
            Interlocked.Add(ref interlockArrayY.GetRef(index), cellDataInt.y);
            Interlocked.Add(ref interlockArrayZ.GetRef(index), cellDataInt.z);
        }
    }
    
    [BurstCompile]
    partial struct Job_UpdateToFinalData : IJobEntity
    {
        [NoAlias][NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputX;
        [NoAlias][NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputY;
        [NoAlias][NativeDisableParallelForRestriction] [ReadOnly] 
        public NativeArray<int> dataInputZ;
        [NoAlias][NativeDisableParallelForRestriction] [WriteOnly] 
        public NativeArray<Color> colorOutput;

        public void Execute(ref CWindTempData2 tempData2, in CWindPosId posId)
        {
            float x = WindFuncUtil.PackIntToFloat(dataInputX[posId.Id]);
            float y = WindFuncUtil.PackIntToFloat(dataInputY[posId.Id]);
            float z = WindFuncUtil.PackIntToFloat(dataInputZ[posId.Id]);
            colorOutput[posId.Id] = new Color(x, y, z, 1.0f);
            tempData2.Value = new float3(x, y, z);
        }
    }
    
    protected override void OnUpdate()
    {
        if (WindMgrECS.Instance == null)
        {
            return;
        }
        
        WindMgrECS.Instance.UpdateColorToTexture();
        
        // 模拟当前帧风力 start
        int3 VolumeSizeMinusOne = WindMgrECS.Instance.VolumeSizeMinusOne;
        WindMgrECS.Instance.UpdateTargetPosition();
        int3 ShiftPos = WindMgrECS.Instance.ShiftPos;
        int Count = WindMgrECS.WindRangeX * WindMgrECS.WindRangeY * WindMgrECS.WindRangeZ;
        float DeltaTime = Time.DeltaTime;
        float3 OffsetPos = WindMgrECS.Instance.OffsetPos;
        int MAXMOTOR = WindMgrECS.MAXMOTOR;
        float DiffusionVelocity = WindMgrECS.Instance.DiffusionVelocity * DeltaTime;
        float AdvectionVelocity = WindMgrECS.Instance.AdvectionVelocity * DeltaTime;
        NativeArray<float3> tempData1Array = new NativeArray<float3>(Count, Allocator.TempJob);
        NativeArray<float3> tempData2Array = new NativeArray<float3>(Count, Allocator.TempJob);
        NativeArray<Color> colorDataArray = WindMgrECS.Instance.GetColorArray();
        
        // 第一步 更新帧变化后的位置偏移
        Job_CopyWindTempData2 jobCopyWindTempData2 = new Job_CopyWindTempData2
        {
            copyArray = tempData2Array
        };
        var copyHandle1 = jobCopyWindTempData2.ScheduleParallel(tempDataEntityQuery, Dependency);

        Job_ShiftWindData jobShiftWindData = new Job_ShiftWindData
        {
            ShiftPos = ShiftPos,
            VolumeSizeMinusOne = VolumeSizeMinusOne,
            tempData1Array = tempData1Array,
            tempData2Array = tempData2Array
        };
        var shiftHandle = jobShiftWindData.ScheduleParallel(Count, 2048, copyHandle1);
        
        // 第二部 处理扩散模拟
        Job_UpdateDiffusion jobUpdateDiffusion = new Job_UpdateDiffusion()
        {
            VolumeSizeMinusOne = VolumeSizeMinusOne,
            tempData1ArrayLength = Count,
            DiffusionVelocity = DiffusionVelocity,
            tempData1Array = tempData1Array,
            tempData2Array = tempData2Array
        };
        var diffusionHandle = jobUpdateDiffusion.ScheduleParallel(Count, 2048, shiftHandle);
        
        // 第三步 计算当前帧的风力发动机数据，并计算出所有记录的点的风力情况
        NativeArray<CWindMotorDirectional> directionalWindArray = new NativeArray<CWindMotorDirectional>(MAXMOTOR, Allocator.TempJob);
        NativeArray<CWindMotorOmni> omniWindArray = new NativeArray<CWindMotorOmni>(MAXMOTOR, Allocator.TempJob);
        NativeArray<CWindMotorVortex> vortexWindArray = new NativeArray<CWindMotorVortex>(MAXMOTOR, Allocator.TempJob);
        NativeArray<CWindMotorMoving> movingWindArray = new NativeArray<CWindMotorMoving>(MAXMOTOR, Allocator.TempJob);
        
        Job_UpdateWindDirectionalMotor jobUpdateWindDirectionalMotor = new Job_UpdateWindDirectionalMotor()
        {
            DeltaTime = DeltaTime,
            copyArray = directionalWindArray
        };
        var copyDirectionHandle = jobUpdateWindDirectionalMotor.ScheduleParallel(motorDirectianalQuery, diffusionHandle);
        
        Job_UpdateWindOmniMotor jobUpdateWindOmniMotor = new Job_UpdateWindOmniMotor()
        {
            DeltaTime = DeltaTime,
            copyArray = omniWindArray
        };
        var copyOmniHandle = jobUpdateWindOmniMotor.ScheduleParallel(motorOmniQuery, copyDirectionHandle);

        Job_UpdateWindVortexMotor jobUpdateWindVortexMotor = new Job_UpdateWindVortexMotor()
        {
            DeltaTime = DeltaTime,
            copyArray = vortexWindArray
        };
        var copyVortexHandle = jobUpdateWindVortexMotor.ScheduleParallel(motorVortexQuery, copyOmniHandle);

        Job_UpdateWindMovingMotor jobUpdateWindMovingMotor = new Job_UpdateWindMovingMotor()
        {
            DeltaTime = DeltaTime,
            copyArray = movingWindArray
        };
        var copyMovingHandle = jobUpdateWindMovingMotor.ScheduleParallel(motorMovingQuery, copyVortexHandle);
        
        // 计算标记点的风力，将数据从temp2移动到temp1，并根据发动机的风力平移或覆盖风力数据
        Job_UpdateWindPosForce jobUpdateWindPosForce = new Job_UpdateWindPosForce()
        {
            OffsetPos = OffsetPos,
            motorDirectionalArray = directionalWindArray,
            motorOmniArray = omniWindArray,
            motorVortexArray = vortexWindArray,
            motorMovingArray = movingWindArray,
            tempData1Array = tempData1Array,
            tempData2Array = tempData2Array
        };
        var updateWindHandle = jobUpdateWindPosForce.ScheduleParallel(Count, 2048, copyMovingHandle);
        
        // 第四步 解算平流模拟和逆向平流模拟，需要通过两个job实现数据交换
        // 数据从temp1到temp2(平流模拟)再从temp2到final(逆向平流模拟后输出)
        // 为了做并行写入和原子计算,先拷贝当前的temp1数据,准备3份空的int类型临时列表用于存放最终结果
        NativeArray<int> interlockedArrayX = new NativeArray<int>(Count, Allocator.TempJob);
        NativeArray<int> interlockedArrayY = new NativeArray<int>(Count, Allocator.TempJob);
        NativeArray<int> interlockedArrayZ = new NativeArray<int>(Count, Allocator.TempJob);
        Job_InitBufferInt jobInitBufferInt = new Job_InitBufferInt()
        {
            initArray1 = interlockedArrayX,
            initArray2 = interlockedArrayY,
            initArray3 = interlockedArrayZ,
            defaultNum = WindFuncUtil.PackFloatToInt(0f)
        };
        var bufferHandle = jobInitBufferInt.ScheduleParallel(Count, 2048, updateWindHandle);
        
        //解算平流模拟
        Job_UpdateAdvectionWindData jobUpdateAdvectionWindData = new Job_UpdateAdvectionWindData()
        {
            dataInput = tempData1Array,
            interlockArrayX = interlockedArrayX,
            interlockArrayY = interlockedArrayY,
            interlockArrayZ = interlockedArrayZ,
            AdvectionVelocity = AdvectionVelocity,
            VolumeSizeMinusSize = VolumeSizeMinusOne
        };
        var advectionHandle = jobUpdateAdvectionWindData.ScheduleParallel(Count, 2048, bufferHandle);
        
        // 同步临时数据
        Job_CopyBufferIntToFloat3 jobCopyBufferIntToFloat3 = new Job_CopyBufferIntToFloat3()
        {
            dataInputX = interlockedArrayX,
            dataInputY = interlockedArrayY,
            dataInputZ = interlockedArrayZ,
            dataOutput = tempData1Array
        };
        var copyTempDataHandle = jobCopyBufferIntToFloat3.ScheduleParallel(Count, 2048, advectionHandle);
        
        // 解算逆向平流模拟
        Job_UpdateReverseAdvectionWindData jobUpdateReverseAdvectionWindData = new Job_UpdateReverseAdvectionWindData()
        {
            dataInput = tempData1Array,
            interlockArrayX = interlockedArrayX,
            interlockArrayY = interlockedArrayY,
            interlockArrayZ = interlockedArrayZ,
            AdvectionVelocity = AdvectionVelocity,
            VolumeSizeMinusSize = VolumeSizeMinusOne
        };
        var reverseAdvectionHandle = jobUpdateReverseAdvectionWindData.ScheduleParallel(Count, 2048, copyTempDataHandle);
        
        // 完成逆向平流模拟后,将数据写回temp2中作为最终数据,并同事写一份转化成颜色的数据写入纹理中
        Job_UpdateToFinalData jobUpdateToFinalData = new Job_UpdateToFinalData()
        {
            dataInputX = interlockedArrayX,
            dataInputY = interlockedArrayY,
            dataInputZ = interlockedArrayZ,
            colorOutput = colorDataArray
        };
        var finalUpdateHandle = jobUpdateToFinalData.ScheduleParallel(tempDataEntityQuery, reverseAdvectionHandle);

        Dependency = finalUpdateHandle;
        
        WindMgrECS.Instance.StoreLastFrameColorHandle(finalUpdateHandle);
        
        // 回收临时列表
        tempData1Array.Dispose(Dependency);
        tempData2Array.Dispose(Dependency);
        
        directionalWindArray.Dispose(Dependency);
        omniWindArray.Dispose(Dependency);
        vortexWindArray.Dispose(Dependency);
        movingWindArray.Dispose(Dependency);
        
        interlockedArrayX.Dispose(Dependency);
        interlockedArrayY.Dispose(Dependency);
        interlockedArrayZ.Dispose(Dependency);
    }
}
