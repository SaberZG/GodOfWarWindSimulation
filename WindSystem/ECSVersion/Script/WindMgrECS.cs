using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Serialization;

public class WindMgrECS : MonoBehaviour
{
    private static WindMgrECS m_Instance;
    public static WindMgrECS Instance
    {
        get
        {
            return m_Instance;
        }
    }
    // ESC相关对象
    private World world;
    private EntityManager entityManager;
    
    /// <summary>
    /// 风向Volume中心对象及其偏移
    /// </summary>
    public Transform TargetTransform;
    /// <summary>
    /// 摄像机中心偏移
    /// </summary>
    public Vector3 CameraCenterOffset;
    /// <summary>
    /// 扩散力度
    /// </summary>
    [FormerlySerializedAs("DiffusionForce")] public float DiffusionVelocity;
    /// <summary>
    /// 平流力度
    /// </summary>
    [FormerlySerializedAs("AdvectionForce")] public float AdvectionVelocity;
    /// <summary>
    /// 整体强度
    /// </summary>
    public float OverallPower = 0.1f;
    /// <summary>
    /// 是否额外做多一次扩散模拟(哇我发现这个地方我忘了跟ComputeShader版本同步逻辑了，算了补不了)
    /// </summary>
    public bool MoreDiffusion = false;
    // public bool CPUWindUseGlobalWind = true;
    /// <summary>
    /// 全局风场大小和方向（没做，同上）
    /// </summary>
    public Vector3 GlobalAmbientWind = Vector3.zero;
    /// <summary>
    /// 采样噪声3D纹理（没做，同上）
    /// </summary>
    public Texture3D WindNoise;
    /// <summary>
    /// 噪声xyz大小额外控制（没做，同上）
    /// </summary>
    public Vector3 WindNoiseScale = Vector3.zero;
    /// <summary>
    /// 噪声UVW纹理偏移采样方向
    /// </summary>
    public Vector3 WindNoiseUVWDir = Vector3.zero;
    /// <summary>
    /// 噪声UVW纹理偏移采样偏移速度
    /// </summary>
    public Vector3 WindNoiseUVWSpeed = Vector3.zero;
    /// <summary>
    /// 噪声UVW纹理偏移采样权重（没做，同上）
    /// </summary>
    public Vector3 WindNoiseUVWScale = Vector3.zero;
    
    // 风场相关参数
    /// <summary>
    /// 每种发动机最多个数，超过则不加载
    /// </summary>
    public static int MAXMOTOR = 15;
    // 风场范围，单位米
    public static readonly int WindRangeX = 32;
    public static readonly int WindRangeY = 16;
    public static readonly int WindRangeZ = 32;

    // 计算参数
    private Vector3 m_VolumeSize;
    private Vector3 m_VolumeSizeMinusOne;
    private Vector3 m_HalfVolume;
    private Vector3 m_OffsetPos;
    private Vector3 m_LastOffsetPos;
    private Vector3 m_WindNoiseOffset = Vector3.zero;
    private Vector3 m_WindNoiseRcpTexSize;
    public float3 OffsetPos;
    public int3 VolumeSizeMinusOne;
    public int3 ShiftPos;
    
    // 原型
    private EntityArchetype arcWindData;
    private EntityArchetype arcMotorDirectional;
    private EntityArchetype arcMotorOmni;
    private EntityArchetype arcMotorVortex;
    private EntityArchetype arcMotorMoving;
    
    //缓存队列
    private NativeArray<Entity> motorDirectionalArray;
    private NativeArray<Entity> motorOmniArray;
    private NativeArray<Entity> motorVortexArray;
    private NativeArray<Entity> motorMovingArray;

    private bool UseColorArray1 = true;
    public NativeArray<Color> colorArray1;
    public NativeArray<Color> colorArray2;
    private JobHandle preFrameColorHandle;
    private bool hasPreFrameHandle = false;
    
    // ECS ECB
    private BeginInitializationEntityCommandBufferSystem _beginInitializationEntityCommandBufferSystem;

    // Wind Data Texture
    [HideInInspector]public Texture3D WindDataTexture;
    
    #region Shader IDs

    private readonly int WindDataBufferId = Shader.PropertyToID("WindDataBuffer");
    private readonly int WindRangeXId = Shader.PropertyToID("WindRangeX");
    private readonly int WindRangeYId = Shader.PropertyToID("WindRangeY");
    private readonly int WindRangeZId = Shader.PropertyToID("WindRangeZ");
    private readonly int OffsetPosId = Shader.PropertyToID("OffsetPos");

    private readonly int m_VolumePosOffsetId = Shader.PropertyToID("VolumePosOffset");
    private readonly int m_VolumeSizeId = Shader.PropertyToID("VolumeSize");
    private readonly int m_OverallPowerId = Shader.PropertyToID("OverallPower");

    #endregion
    private void Awake()
    {
        Application.targetFrameRate = 120;
        m_Instance = this;
        m_VolumeSize = new Vector3(WindRangeX, WindRangeY, WindRangeZ);
        m_VolumeSizeMinusOne = new Vector3(WindRangeX - 1, WindRangeY - 1, WindRangeZ - 1);
        m_HalfVolume = new Vector3(WindRangeX / 2, WindRangeY / 2, WindRangeZ / 2);
        VolumeSizeMinusOne = new int3(m_VolumeSizeMinusOne);
        m_OffsetPos = TargetTransform == null ? Vector3.zero : TargetTransform.position;
        m_OffsetPos += (TargetTransform == null ? Vector3.forward : TargetTransform.forward )* CameraCenterOffset.z;
        m_OffsetPos += (TargetTransform == null ? Vector3.right :  TargetTransform.right) * CameraCenterOffset.x;
        m_OffsetPos += (TargetTransform == null ? Vector3.up :  TargetTransform.up) * CameraCenterOffset.y;
        m_OffsetPos -= m_HalfVolume;
        OffsetPos = m_OffsetPos;
        m_LastOffsetPos = ConvertFloatPointToInt(m_OffsetPos);
        int count = WindRangeX * WindRangeY * WindRangeZ;
        colorArray1 = new NativeArray<Color>(count, Allocator.Persistent);
        colorArray2 = new NativeArray<Color>(count, Allocator.Persistent);
        
        Shader.SetGlobalVector(m_VolumeSizeId, m_VolumeSize);

        if (WindDataTexture == null)
        {
            WindDataTexture = new Texture3D(WindRangeX, WindRangeY, WindRangeZ, GraphicsFormat.R32G32B32A32_SFloat,
                TextureCreationFlags.None);
            Shader.SetGlobalTexture(WindDataBufferId, WindDataTexture);
        }
    }

    void Start()
    {
        // 初始化世界
        world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;
        _beginInitializationEntityCommandBufferSystem =
            world.GetExistingSystem<BeginInitializationEntityCommandBufferSystem>();
        InitWindPosDataEntities();
        InitWindMotors();
        Shader.SetGlobalInt(WindRangeXId, WindRangeX);
        Shader.SetGlobalInt(WindRangeYId, WindRangeY);
        Shader.SetGlobalInt(WindRangeZId, WindRangeZ);
    }
    /// <summary>
    /// 创建点位风力数据存储实体
    /// </summary>
    void InitWindPosDataEntities()
    {
        int entityNum = WindRangeX * WindRangeY * WindRangeZ;
        // 创建指定数量的风场位置风力节点
        arcWindData = entityManager.CreateArchetype(
            ComponentType.ReadWrite<CWindPosId>(),
            ComponentType.ReadWrite<CWindTempData1>(),
            ComponentType.ReadWrite<CWindTempData2>()
        );
        var windPosDataArray = entityManager.CreateEntity(arcWindData, entityNum, Allocator.TempJob);
        for (int i = 0; i < windPosDataArray.Length; i++)
        {
            Entity e = windPosDataArray[i];
            entityManager.SetComponentData(e, new CWindPosId{Id = i, Pos = WindFuncUtil.WindPosDataIdToPos(i)});
        }
        
        windPosDataArray.Dispose();
    }

    /// <summary>
    /// 初始化固定数量的风力发动机的实体
    /// </summary>
    void InitWindMotors()
    {
        InitWindMotorDirectional();
        InitWindMotorOmni();
        InitWindMotorVortex();
        InitWindMotorMoving();
    }

    /// <summary>
    /// 平行风发动机
    /// </summary>
    void InitWindMotorDirectional()
    {
        arcMotorDirectional = entityManager.CreateArchetype(
            ComponentType.ReadWrite<CWindMotorLifeTime>(),
            ComponentType.ReadWrite<CWindMotorDirectional>()
        );
        motorDirectionalArray = entityManager.CreateEntity(arcMotorDirectional, MAXMOTOR, Allocator.Persistent);
        for (int i = 0; i < motorDirectionalArray.Length; i++)
        {
            Entity e = motorDirectionalArray[i];
            entityManager.SetComponentData(e, new CWindMotorLifeTime{LeftTime = -1, IsUnused = true});
            entityManager.SetComponentData(e, new CWindMotorDirectional(){id = i});
        }
    }
    /// <summary>
    /// 径向风发动机
    /// </summary>
    void InitWindMotorOmni()
    {
        arcMotorOmni = entityManager.CreateArchetype(
            ComponentType.ReadWrite<CWindMotorLifeTime>(),
            ComponentType.ReadWrite<CWindMotorOmni>()
        );
        motorOmniArray = entityManager.CreateEntity(arcMotorOmni, MAXMOTOR, Allocator.Persistent);
        for (int i = 0; i < motorOmniArray.Length; i++)
        {
            Entity e = motorOmniArray[i];
            entityManager.SetComponentData(e, new CWindMotorLifeTime{LeftTime = -1, IsUnused = true});
            entityManager.SetComponentData(e, new CWindMotorOmni(){id = i});
        }
    }
    /// <summary>
    /// 漩涡风发动机
    /// </summary>
    void InitWindMotorVortex()
    {
        arcMotorVortex = entityManager.CreateArchetype(
            ComponentType.ReadWrite<CWindMotorLifeTime>(),
            ComponentType.ReadWrite<CWindMotorVortex>()
        );
        motorVortexArray = entityManager.CreateEntity(arcMotorVortex, MAXMOTOR, Allocator.Persistent);
        for (int i = 0; i < motorVortexArray.Length; i++)
        {
            Entity e = motorVortexArray[i];
            entityManager.SetComponentData(e, new CWindMotorLifeTime{LeftTime = -1, IsUnused = true});
            entityManager.SetComponentData(e, new CWindMotorVortex(){id = i});
        }
    }
    /// <summary>
    /// 移动风发动机
    /// </summary>
    void InitWindMotorMoving()
    {
        arcMotorMoving = entityManager.CreateArchetype(
            ComponentType.ReadWrite<CWindMotorLifeTime>(),
            ComponentType.ReadWrite<CWindMotorMoving>()
        );
        motorMovingArray = entityManager.CreateEntity(arcMotorMoving, MAXMOTOR, Allocator.Persistent);
        for (int i = 0; i < motorMovingArray.Length; i++)
        {
            Entity e = motorMovingArray[i];
            entityManager.SetComponentData(e, new CWindMotorLifeTime{LeftTime = -1, IsUnused = true});
            entityManager.SetComponentData(e, new CWindMotorMoving(){id = i});
        }
    }

    /// <summary>
    /// 更新观察对象的位置变化
    /// </summary>
    public void UpdateTargetPosition()
    {
        m_OffsetPos = TargetTransform == null ? Vector3.zero : TargetTransform.position;
        m_OffsetPos += (TargetTransform == null ? Vector3.forward : TargetTransform.forward )* CameraCenterOffset.z;
        m_OffsetPos += (TargetTransform == null ? Vector3.right :  TargetTransform.right) * CameraCenterOffset.x;
        m_OffsetPos += (TargetTransform == null ? Vector3.up :  TargetTransform.up) * CameraCenterOffset.y;
        m_OffsetPos -= m_HalfVolume;
        OffsetPos = m_OffsetPos;
        m_WindNoiseOffset += WindNoiseUVWDir + WindNoiseUVWSpeed * Time.deltaTime;
        Vector3 cellPos = ConvertFloatPointToInt(m_OffsetPos);
        Vector3 m_shiftPos = cellPos - m_LastOffsetPos;
        m_LastOffsetPos = cellPos;
        ShiftPos = new int3((int)(m_shiftPos.x), (int)(m_shiftPos.y), (int)(m_shiftPos.z));
        Shader.SetGlobalVector(OffsetPosId, m_OffsetPos);
        Shader.SetGlobalVector(m_VolumePosOffsetId, m_OffsetPos);
        Shader.SetGlobalFloat(m_OverallPowerId, OverallPower);
    }

    Vector3 ConvertFloatPointToInt(Vector3 v)
    {
        Vector3 o;
        o.x = v.x < 0 ? Mathf.Ceil(v.x) : Mathf.Floor(v.x);
        o.y = v.y < 0 ? Mathf.Ceil(v.y) : Mathf.Floor(v.y);
        o.z = v.z < 0 ? Mathf.Ceil(v.z) : Mathf.Floor(v.z);
        return o;
    }

    /// <summary>
    /// 将风力发动机游戏对象变换成ECS的实体
    /// </summary>
    /// <param name="motor"></param>
    public void TransferMotorToECSEntity(WindMotor motor)
    {
        switch (motor.MotorType)
        {
            case MotorType.Directional:
                UpdateWindMotorDirectional(motor);
                break;
            case MotorType.Omni:
                UpdateWindMotorOmni(motor);
                break;
            case MotorType.Vortex:
                UpdateWindMotorVortex(motor);
                break;
            case MotorType.Moving:
                UpdateWindMotorMoving(motor);
                break;
        }
    }

    void UpdateWindMotorDirectional(WindMotor motor)
    {
        for (int i = 0; i < motorDirectionalArray.Length; i++)
        {
            Entity e = motorDirectionalArray[i];
            var lifeTime = entityManager.GetComponentData<CWindMotorLifeTime>(e);
            if (lifeTime.IsUnused)
            {
                // 从这个地方开始转换需要插入的实体组件，可以避免产生Sync Point影响性能，下同
                var ecb = _beginInitializationEntityCommandBufferSystem.CreateCommandBuffer();
                ecb.SetComponent(e, new CWindMotorDirectional()
                {
                    id = i,
                    PosWS = motor.transform.position,
                    Radius = motor.Radius,
                    Velocity = motor.Force,
                    VelocityDir = motor.transform.forward
                });
                ecb.SetComponent(e, new CWindMotorLifeTime()
                {
                    CreateTime = Time.time,
                    Loop = motor.Loop,
                    LifeTime = motor.LifeTime,
                    LeftTime = motor.LifeTime,
                    IsUnused = false
                });
                break;
            }
        }
    }
    void UpdateWindMotorOmni(WindMotor motor)
    {
        for (int i = 0; i < motorOmniArray.Length; i++)
        {
            Entity e = motorOmniArray[i];
            var lifeTime = entityManager.GetComponentData<CWindMotorLifeTime>(e);
            if (lifeTime.IsUnused)
            {
                var ecb = _beginInitializationEntityCommandBufferSystem.CreateCommandBuffer();
                ecb.SetComponent(e, new CWindMotorOmni()
                {
                    id = i,
                    PosWS = motor.transform.position,
                    Radius = motor.Radius,
                    Velocity = motor.Force
                });
                ecb.SetComponent(e, new CWindMotorLifeTime()
                {
                    CreateTime = Time.time,
                    Loop = motor.Loop,
                    LifeTime = motor.LifeTime,
                    LeftTime = motor.LifeTime,
                    IsUnused = false
                });
                break;
            }
        }
    }
    void UpdateWindMotorVortex(WindMotor motor)
    {
        for (int i = 0; i < motorVortexArray.Length; i++)
        {
            Entity e = motorVortexArray[i];
            var lifeTime = entityManager.GetComponentData<CWindMotorLifeTime>(e);
            if (lifeTime.IsUnused)
            {
                var ecb = _beginInitializationEntityCommandBufferSystem.CreateCommandBuffer();
                ecb.SetComponent(e, new CWindMotorVortex()
                {
                    id = i,
                    PosWS = motor.transform.position,
                    Axis = motor.Asix,
                    Radius = motor.Radius,
                    Velocity = motor.Force
                });
                ecb.SetComponent(e, new CWindMotorLifeTime()
                {
                    CreateTime = Time.time,
                    Loop = motor.Loop,
                    LifeTime = motor.LifeTime,
                    LeftTime = motor.LifeTime,
                    IsUnused = false
                });
                break;
            }
        }
    }
    void UpdateWindMotorMoving(WindMotor motor)
    {
        for (int i = 0; i < motorMovingArray.Length; i++)
        {
            Entity e = motorMovingArray[i];
            var lifeTime = entityManager.GetComponentData<CWindMotorLifeTime>(e);
            if (lifeTime.IsUnused)
            {
                var ecb = _beginInitializationEntityCommandBufferSystem.CreateCommandBuffer();
                ecb.SetComponent(e, new CWindMotorMoving()
                {
                    id = i,
                    PrePosWS = motor.transform.position,
                    PosWS = motor.transform.position,
                    MoveLen = motor.MoveLength,
                    Radius = motor.Radius,
                    Velocity = motor.Force
                });
                ecb.SetComponent(e, new CWindMotorLifeTime()
                {
                    CreateTime = Time.time,
                    Loop = motor.Loop,
                    LifeTime = motor.LifeTime,
                    LeftTime = motor.LifeTime,
                    IsUnused = false
                });
                break;
            }
        }
    }
    
    public NativeArray<Color> GetColorArray()
    {
        UseColorArray1 = !UseColorArray1;
        return UseColorArray1 ? colorArray1 : colorArray2;
    }

    // 存储最后一帧的js句柄
    public void StoreLastFrameColorHandle(JobHandle jobhandle)
    {
        preFrameColorHandle = jobhandle;
        hasPreFrameHandle = true;
    }

    /// <summary>
    /// 将颜色写入Texture（性能大头，暂时没有找到好的方案）
    /// 使用上一帧的数据，也可以避免等待多线程完成而Sync Point
    /// </summary>
    public void UpdateColorToTexture()
    {
        if (hasPreFrameHandle)
        {
            preFrameColorHandle.Complete();
            var colorData = UseColorArray1 ? colorArray1 : colorArray2;
            WindDataTexture.SetPixelData(colorData, 0, 0);
            WindDataTexture.Apply();
            hasPreFrameHandle = false;
        }
    }
    private void OnDestroy()
    {
        if (hasPreFrameHandle)
        {
            preFrameColorHandle.Complete();
            hasPreFrameHandle = false;
        }
        motorDirectionalArray.Dispose();
        motorOmniArray.Dispose();
        motorVortexArray.Dispose();
        motorMovingArray.Dispose();
        colorArray1.Dispose();
        colorArray2.Dispose();
        if (WindDataTexture != null)
        {
            DestroyImmediate(WindDataTexture);
            WindDataTexture = null;
        }
    }
}
