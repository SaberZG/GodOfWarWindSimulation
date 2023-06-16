using System;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;
using System.Net.Http.Headers;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class WindManager : MonoBehaviour
{
    private static WindManager m_Instance;
    public static WindManager Instance
    {
        get
        {
            return m_Instance;
        }
    }
    /// <summary>
    /// 风向Volume中心对象及其偏移
    /// </summary>
    public Transform TargetTransform;
    public Vector3 CameraCenterOffset;
    public float DiffusionForce;
    public float AdvectionForce;
    public float OverallPower = 0.1f;
    public bool MoreDiffusion = false;
    public bool CPUWindUseGlobalWind = true;
    public Texture3D WindNoise;
    public Vector3 GlobalAmbientWind = Vector3.zero;
    public Vector3 WindNoiseUVDir = Vector3.zero;
    public Vector3 WindNoiseUVSpeed = Vector3.zero;
    public Vector3 WindNoiseUVScale = Vector3.zero;
    public Vector3 WindNoiseScale = Vector3.zero;

    // 操作CS（当初肯定是喝了假的生啤酒，其实这些CS可以写在一个文件里，搞得如此丑陋。。。懒得优化了）
    public ComputeShader ShiftPosCS;
    public ComputeShader DiffusionCS;
    public ComputeShader MotorsSpeedCS;
    public ComputeShader AdvectionCS;
    public ComputeShader BufferExchangeCS;
    public ComputeShader MergeChannelCS;
    
    private static int MAXMOTOR = 10;
    // Volume Size
    private static int m_WindBrandX = 32;
    private static int m_WindBrandY = 16;
    private static int m_WindBrandZ = 32;
    // ShaderId
    private static int m_ShiftPosId = Shader.PropertyToID("ShiftPos");
    private static int m_ShiftPosXId = Shader.PropertyToID("ShiftPosX");
    private static int m_ShiftPosYId = Shader.PropertyToID("ShiftPosY");
    private static int m_ShiftPosZId = Shader.PropertyToID("ShiftPosZ");
    private static int m_VolumePosOffsetId = Shader.PropertyToID("VolumePosOffset");
    private static int m_VolumeSizeId = Shader.PropertyToID("VolumeSize");
    private static int m_VolumeSizeXId = Shader.PropertyToID("VolumeSizeX");
    private static int m_VolumeSizeYId = Shader.PropertyToID("VolumeSizeY");
    private static int m_VolumeSizeMinusOneId = Shader.PropertyToID("VolumeSizeMinusOne");
    private static int m_DiffusionForceId = Shader.PropertyToID("DiffusionForce");
    private static int m_AdvectionForceId = Shader.PropertyToID("AdvectionForce");
    private static int m_WindNoiseId = Shader.PropertyToID("WindNoise");
    private static int m_OverallPowerId = Shader.PropertyToID("OverallPower");
    private static int m_GlobalAmbientWindId = Shader.PropertyToID("GlobalAmbientWind");
    private static int m_WindNoiseRcpTexSizeId = Shader.PropertyToID("WindNoiseRcpTexSize");
    private static int m_WindNoiseOffsetId = Shader.PropertyToID("WindNoiseOffset");
    private static int m_WindNoiseUVScaleId = Shader.PropertyToID("WindNoiseUVScale");
    private static int m_WindNoiseScaleId = Shader.PropertyToID("WindNoiseScale");
    
    private static int m_DirectionalMotorBufferId = Shader.PropertyToID("DirectionalMotorBuffer");
    private static int m_DirectionalMotorBufferCountId = Shader.PropertyToID("DirectionalMotorBufferCount");
    private static int m_OmniMotorBufferId = Shader.PropertyToID("OmniMotorBuffer");
    private static int m_OmniMotorBufferCountId = Shader.PropertyToID("OmniMotorBufferCount");
    private static int m_VortexMotorBufferId = Shader.PropertyToID("VortexMotorBuffer");
    private static int m_MovingMotorBufferId = Shader.PropertyToID("MovingMotorBuffer");
    private static int m_VortexMotorBufferCountId = Shader.PropertyToID("VortexMotorBufferCount");
    private static int m_MovingMotorBufferCountId = Shader.PropertyToID("MovingMotorBufferCount");
    private static int m_WindBufferInputID = Shader.PropertyToID("WindBufferInput");
    private static int m_WindBufferInputXID = Shader.PropertyToID("WindBufferInputX");
    private static int m_WindBufferInputYID = Shader.PropertyToID("WindBufferInputY");
    private static int m_WindBufferInputZID = Shader.PropertyToID("WindBufferInputZ");
    private static int m_WindBufferOutputID = Shader.PropertyToID("WindBufferOutput");
    private static int m_WindBufferOutputXID = Shader.PropertyToID("WindBufferOutputX");
    private static int m_WindBufferOutputYID = Shader.PropertyToID("WindBufferOutputY");
    private static int m_WindBufferOutputZID = Shader.PropertyToID("WindBufferOutputZ");
    private static int m_WindBufferTargetID = Shader.PropertyToID("WindBufferTarget");
    // Shader Key
    private static int m_WindBufferChannelR1ID = Shader.PropertyToID("WindBufferChannelR1");
    private static int m_WindBufferChannelR2ID = Shader.PropertyToID("WindBufferChannelR2");
    private static int m_WindBufferChannelG1ID = Shader.PropertyToID("WindBufferChannelG1");
    private static int m_WindBufferChannelG2ID = Shader.PropertyToID("WindBufferChannelG2");
    private static int m_WindBufferChannelB1ID = Shader.PropertyToID("WindBufferChannelB1");
    private static int m_WindBufferChannelB2ID = Shader.PropertyToID("WindBufferChannelB2");
    
    private static int m_WindVelocityDataID = Shader.PropertyToID("WindVelocityData");
    private static int m_WindDataForCPUBufferId = Shader.PropertyToID("WindDataForCPUBuffer");
    
    private Vector3 m_VolumeSize;
    private Vector3 m_VolumeSizeMinusOne;
    private Vector3 m_HalfVolume;
    private Vector3 m_OffsetPos;
    private Vector3 m_LastOffsetPos;
    private Vector3 m_WindNoiseOffset = Vector3.zero;
    private Vector3 m_WindNoiseRcpTexSize;
    // kernels
    private int m_ShiftPosKernel;
    private int m_DiffusionKernel;
    private int m_MotorSpeedKernel;
    private int m_AdvectionKernel;
    private int m_BufferExchangeKernel;
    private int m_ClearBufferKernel;
    private int m_ReverseAdvectionKernel;
    private int m_MergeChannelKernel;
    // rt settings
    private RenderTextureDescriptor WindChannelDescriptor = new RenderTextureDescriptor();
    private RenderTexture m_WindBufferChannelR1;
    private RenderTexture m_WindBufferChannelR2;
    private RenderTexture m_WindBufferChannelG1;
    private RenderTexture m_WindBufferChannelG2;
    private RenderTexture m_WindBufferChannelB1;
    private RenderTexture m_WindBufferChannelB2;
    
    private RenderTextureDescriptor WindVelocityDataDescriptor = new RenderTextureDescriptor();
    private RenderTexture m_WindVelocityData;

    private ComputeBuffer m_DirectionalMotorBuffer;
    private ComputeBuffer m_OmniMotorBuffer;
    private ComputeBuffer m_VortexMotorBuffer;
    private ComputeBuffer m_MovingMotorBuffer;
    private ComputeBuffer m_WindDataForCPUBuffer;
    private List<WindMotor> m_MotorList = new List<WindMotor>();
    private List<MotorDirectional> m_DirectionalMotorList = new List<MotorDirectional>(MAXMOTOR);
    private List<MotorOmni> m_OmniMotorList = new List<MotorOmni>(MAXMOTOR);
    private List<MotorVortex> m_VortexMotorList = new List<MotorVortex>(MAXMOTOR);
    private List<MotorMoving> m_MovingMotorList = new List<MotorMoving>(MAXMOTOR);
    private Vector3[] m_WindDataForCPU;
    private void Awake()
    {
        Application.targetFrameRate = 60;
        m_Instance = this;
        m_VolumeSize = new Vector3(m_WindBrandX, m_WindBrandY, m_WindBrandZ);
        m_VolumeSizeMinusOne = new Vector3(m_WindBrandX - 1, m_WindBrandY - 1, m_WindBrandZ - 1);
        m_HalfVolume = new Vector3(m_WindBrandX / 2, m_WindBrandY / 2, m_WindBrandZ / 2);
        
        m_OffsetPos = TargetTransform == null ? Vector3.zero : TargetTransform.position;
        m_OffsetPos += (TargetTransform == null ? Vector3.forward : TargetTransform.forward )* CameraCenterOffset.z;
        m_OffsetPos += (TargetTransform == null ? Vector3.right :  TargetTransform.right) * CameraCenterOffset.x;
        m_OffsetPos += (TargetTransform == null ? Vector3.up :  TargetTransform.up) * CameraCenterOffset.y;
        m_OffsetPos -= m_HalfVolume;
        
        m_LastOffsetPos = ConvertFloatPointToInt(m_OffsetPos);
        
        // CS Kernel初始化
        if (ShiftPosCS != null)
        {
            m_ShiftPosKernel = ShiftPosCS.FindKernel("CSMain");
        }
        if (DiffusionCS != null)
        {
            m_DiffusionKernel = DiffusionCS.FindKernel("CSMain");
        }
        if (MotorsSpeedCS != null)
        {
            m_MotorSpeedKernel = MotorsSpeedCS.FindKernel("WindVolumeRenderMotorCS");
        }
        if (AdvectionCS != null)
        {
            m_AdvectionKernel = AdvectionCS.FindKernel("CSMain");
            m_ReverseAdvectionKernel = AdvectionCS.FindKernel("CSMain2");
        }
        if (BufferExchangeCS != null)
        {
            m_BufferExchangeKernel = BufferExchangeCS.FindKernel("CSMain");
            m_ClearBufferKernel = BufferExchangeCS.FindKernel("CSMain2");
        }
        if (MergeChannelCS != null)
        {
            m_MergeChannelKernel = MergeChannelCS.FindKernel("CSMain");
        }
        
        WindChannelDescriptor.enableRandomWrite = true;
        WindChannelDescriptor.width = m_WindBrandX;
        WindChannelDescriptor.height = m_WindBrandY;
        WindChannelDescriptor.dimension = TextureDimension.Tex3D;
        WindChannelDescriptor.volumeDepth = m_WindBrandZ;
        WindChannelDescriptor.colorFormat = RenderTextureFormat.RInt;
        WindChannelDescriptor.graphicsFormat = GraphicsFormat.R32_SInt;
        WindChannelDescriptor.msaaSamples = 1;

        CreateRenderTexture(ref m_WindBufferChannelR1, ref WindChannelDescriptor, "WindBufferChannelR1");
        CreateRenderTexture(ref m_WindBufferChannelR2, ref WindChannelDescriptor, "WindBufferChannelR2");
        CreateRenderTexture(ref m_WindBufferChannelG1, ref WindChannelDescriptor, "WindBufferChannelG1");
        CreateRenderTexture(ref m_WindBufferChannelG2, ref WindChannelDescriptor, "WindBufferChannelG2");
        CreateRenderTexture(ref m_WindBufferChannelB1, ref WindChannelDescriptor, "WindBufferChannelB1");
        CreateRenderTexture(ref m_WindBufferChannelB2, ref WindChannelDescriptor, "WindBufferChannelB2");
        
        WindVelocityDataDescriptor.enableRandomWrite = true;
        WindVelocityDataDescriptor.width = m_WindBrandX;
        WindVelocityDataDescriptor.height = m_WindBrandY;
        WindVelocityDataDescriptor.dimension = TextureDimension.Tex3D;
        WindVelocityDataDescriptor.volumeDepth = m_WindBrandZ;
        WindVelocityDataDescriptor.colorFormat = RenderTextureFormat.ARGBFloat;;
        WindVelocityDataDescriptor.graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat;
        WindVelocityDataDescriptor.msaaSamples = 1;
        CreateRenderTexture(ref m_WindVelocityData, ref WindVelocityDataDescriptor, "WindVelocityData");
        
        Shader.SetGlobalVector(m_VolumeSizeId, m_VolumeSize);
        m_WindNoiseRcpTexSize = new Vector3(1.0f / 64.0f, 1.0f / 32.0f, 1.0f / 64.0f);
        Shader.SetGlobalVector(m_WindNoiseRcpTexSizeId, m_WindNoiseRcpTexSize);
        Shader.SetGlobalTexture(m_WindBufferChannelR1ID, m_WindBufferChannelR1);
        Shader.SetGlobalTexture(m_WindBufferChannelR2ID, m_WindBufferChannelR2);
        Shader.SetGlobalTexture(m_WindBufferChannelG1ID, m_WindBufferChannelG1);
        Shader.SetGlobalTexture(m_WindBufferChannelG2ID, m_WindBufferChannelG2);
        Shader.SetGlobalTexture(m_WindBufferChannelB1ID, m_WindBufferChannelB1);
        Shader.SetGlobalTexture(m_WindBufferChannelB2ID, m_WindBufferChannelB2);
        
        Shader.SetGlobalTexture(m_WindVelocityDataID, m_WindVelocityData);
        Shader.SetGlobalTexture(m_WindNoiseId, WindNoise);

        int totalNum = m_WindBrandX * m_WindBrandY * m_WindBrandZ;
        m_WindDataForCPU = new Vector3[totalNum];
        for (int i = 0; i < totalNum; i++)
        {
            m_WindDataForCPU[i] = Vector3.zero;
        }

        m_DirectionalMotorBuffer = new ComputeBuffer(MAXMOTOR, 28);
        m_OmniMotorBuffer = new ComputeBuffer(MAXMOTOR, 20);
        m_VortexMotorBuffer = new ComputeBuffer(MAXMOTOR, 32);
        m_MovingMotorBuffer = new ComputeBuffer(MAXMOTOR, 36);
        m_WindDataForCPUBuffer = new ComputeBuffer(totalNum, 12);
    }
    private void CreateRenderTexture(ref RenderTexture rt, ref RenderTextureDescriptor desc, string name)
    {
        if (rt == null)
        {
            rt = RenderTexture.GetTemporary(desc);
            rt.filterMode = FilterMode.Bilinear;
            rt.name = name;
        }
    }
    
    public void DoRenderWindVolume()
    {
        UpdateTargetPosition();
        DoShiftPos(1);
        DoDiffusion(2);
        DoRenderWindVelocityData(1);
        DoAdvection(2);
        DoMergeChannel(1);
    }

    void UpdateTargetPosition()
    {
        m_OffsetPos = TargetTransform == null ? Vector3.zero : TargetTransform.position;
        m_OffsetPos += (TargetTransform == null ? Vector3.forward : TargetTransform.forward )* CameraCenterOffset.z;
        m_OffsetPos += (TargetTransform == null ? Vector3.right :  TargetTransform.right) * CameraCenterOffset.x;
        m_OffsetPos += (TargetTransform == null ? Vector3.up :  TargetTransform.up) * CameraCenterOffset.y;
        m_OffsetPos -= m_HalfVolume;
        Shader.SetGlobalFloat(m_OverallPowerId, OverallPower);
        Shader.SetGlobalVector(m_VolumePosOffsetId, m_OffsetPos);
        Shader.SetGlobalVector(m_GlobalAmbientWindId, GlobalAmbientWind);
        m_WindNoiseOffset += WindNoiseUVDir + WindNoiseUVSpeed * Time.deltaTime;
        Shader.SetGlobalVector(m_WindNoiseOffsetId, m_WindNoiseOffset);
        Shader.SetGlobalVector(m_WindNoiseUVScaleId, WindNoiseUVScale);
        Shader.SetGlobalVector(m_WindNoiseScaleId, WindNoiseScale);
    }
    /// <summary>
    /// 做锁定对象移动时，所有风力数据的平移
    /// </summary>
    /// <param name="form">用于Debug ComputeShader的输出的图片，后面都一样</param>
    void DoShiftPos(int form)
    {
        if (ShiftPosCS != null)
        {
            var formRTR = form == 1 ? m_WindBufferChannelR1 : m_WindBufferChannelR2;
            var formRTG = form == 1 ? m_WindBufferChannelG1 : m_WindBufferChannelG2;
            var formRTB = form == 1 ? m_WindBufferChannelB1 : m_WindBufferChannelB2;
            var toRTR = form == 1 ? m_WindBufferChannelR2 : m_WindBufferChannelR1;
            var toRTG = form == 1 ? m_WindBufferChannelG2 : m_WindBufferChannelG1;
            var toRTB = form == 1 ? m_WindBufferChannelB2 : m_WindBufferChannelB1;
            Vector3 cellPos = ConvertFloatPointToInt(m_OffsetPos);
            Vector3 shiftPos = cellPos - m_LastOffsetPos;
            ShiftPosCS.SetVector(m_VolumeSizeMinusOneId, m_VolumeSizeMinusOne);
            ShiftPosCS.SetInt(m_ShiftPosXId, (int)(shiftPos.x));
            ShiftPosCS.SetInt(m_ShiftPosYId, (int)(shiftPos.y));
            ShiftPosCS.SetInt(m_ShiftPosZId, (int)(shiftPos.z));
            // 这里如果打包了Int发过去会导致同步不了，神秘bug，不知道为什么
            // ShiftPosCS.SetVector(m_ShiftPosId, new Vector4(shiftPos.x, shiftPos.y, shiftPos.z, 0));
            
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferInputXID, formRTR);
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferInputYID, formRTG);
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferInputZID, formRTB);
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferOutputXID, toRTR);
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferOutputYID, toRTG);
            ShiftPosCS.SetTexture(m_ShiftPosKernel, m_WindBufferOutputZID, toRTB);
            
            ShiftPosCS.Dispatch(m_ShiftPosKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            m_LastOffsetPos = cellPos;
        }
    }
    
    /// <summary>
    /// 处理扩散模拟
    /// </summary>
    /// <param name="form"></param>
    void DoDiffusion(int form)
    {
        if (DiffusionCS != null)
        {
            var formRTR = form == 1 ? m_WindBufferChannelR1 : m_WindBufferChannelR2;
            var formRTG = form == 1 ? m_WindBufferChannelG1 : m_WindBufferChannelG2;
            var formRTB = form == 1 ? m_WindBufferChannelB1 : m_WindBufferChannelB2;
            var toRTR = form == 1 ? m_WindBufferChannelR2 : m_WindBufferChannelR1;
            var toRTG = form == 1 ? m_WindBufferChannelG2 : m_WindBufferChannelG1;
            var toRTB = form == 1 ? m_WindBufferChannelB2 : m_WindBufferChannelB1;
            
            DiffusionCS.SetVector(m_VolumeSizeMinusOneId, m_VolumeSizeMinusOne);
            DiffusionCS.SetFloat(m_DiffusionForceId, DiffusionForce);
            // Do Channel R
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTR);
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTR);
            DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do Channel G
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTG);
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTG);
            DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do Channel B
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTB);
            DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTB);
            DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);

            if (MoreDiffusion)
            {
                // Do Channel R
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, toRTR);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, formRTR);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
                // Do Channel G
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, toRTG);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, formRTG);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
                // Do Channel B
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, toRTB);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, formRTB);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
                
                // Do Channel R
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTR);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTR);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
                // Do Channel G
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTG);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTG);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
                // Do Channel B
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferInputID, formRTB);
                DiffusionCS.SetTexture(m_DiffusionKernel, m_WindBufferOutputID, toRTB);
                DiffusionCS.Dispatch(m_DiffusionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            }
        }
    }
    
    /// <summary>
    /// 根据已有的风力发动机信息，渲染风力信息到纹理中
    /// </summary>
    /// <param name="form"></param>
    void DoRenderWindVelocityData(int form)
    {
        if (MotorsSpeedCS != null && BufferExchangeCS != null)
        {
            m_DirectionalMotorList.Clear();
            m_OmniMotorList.Clear();
            m_VortexMotorList.Clear();
            m_MovingMotorList.Clear();

            int directionalMotorCount = 0;
            int omniMotorCount = 0;
            int vortexMotorCount = 0;
            int movingMotorCount = 0;
            foreach (WindMotor motor in m_MotorList)
            {
                motor.UpdateWindMotor();
                switch (motor.MotorType)
                {
                    case MotorType.Directional:
                        if (directionalMotorCount < MAXMOTOR)
                        {
                            m_DirectionalMotorList.Add(motor.motorDirectional);
                            directionalMotorCount++;
                        }
                        break;
                    case MotorType.Omni:
                        if (omniMotorCount < MAXMOTOR)
                        {
                            m_OmniMotorList.Add(motor.motorOmni);
                            omniMotorCount++;
                        }
                        break;
                    case MotorType.Vortex:
                        if (vortexMotorCount < MAXMOTOR)
                        {
                            m_VortexMotorList.Add(motor.motorVortex);
                            vortexMotorCount++;
                        }
                        break;
                    case MotorType.Moving:
                        if (movingMotorCount < MAXMOTOR)
                        {
                            m_MovingMotorList.Add(motor.motorMoving);
                            movingMotorCount++;
                        }
                        break;
                }
            }
            // 往列表数据中插入空的发动机数据
            if (directionalMotorCount < MAXMOTOR)
            {
                MotorDirectional motor = WindMotor.GetEmptyMotorDirectional();
                for (int i = directionalMotorCount; i < MAXMOTOR; i++)
                {
                    m_DirectionalMotorList.Add(motor);
                }
            }
            if (omniMotorCount < MAXMOTOR)
            {
                MotorOmni motor = WindMotor.GetEmptyMotorOmni();
                for (int i = omniMotorCount; i < MAXMOTOR; i++)
                {
                    m_OmniMotorList.Add(motor);
                }
            }
            if (vortexMotorCount < MAXMOTOR)
            {
                MotorVortex motor = WindMotor.GetEmptyMotorVortex();
                for (int i = vortexMotorCount; i < MAXMOTOR; i++)
                {
                    m_VortexMotorList.Add(motor);
                }
            }
            if (movingMotorCount < MAXMOTOR)
            {
                MotorMoving motor = WindMotor.GetEmptyMotorMoving();
                for (int i = movingMotorCount; i < MAXMOTOR; i++)
                {
                    m_MovingMotorList.Add(motor);
                }
            }
            m_DirectionalMotorBuffer.SetData(m_DirectionalMotorList);
            MotorsSpeedCS.SetBuffer(m_MotorSpeedKernel, m_DirectionalMotorBufferId, m_DirectionalMotorBuffer);
            m_OmniMotorBuffer.SetData(m_OmniMotorList);
            MotorsSpeedCS.SetBuffer(m_MotorSpeedKernel, m_OmniMotorBufferId, m_OmniMotorBuffer);
            m_VortexMotorBuffer.SetData(m_VortexMotorList);
            MotorsSpeedCS.SetBuffer(m_MotorSpeedKernel, m_VortexMotorBufferId, m_VortexMotorBuffer);
            m_MovingMotorBuffer.SetData(m_MovingMotorList);
            MotorsSpeedCS.SetBuffer(m_MotorSpeedKernel, m_MovingMotorBufferId, m_MovingMotorBuffer);

            MotorsSpeedCS.SetFloat(m_DirectionalMotorBufferCountId, directionalMotorCount);
            MotorsSpeedCS.SetFloat(m_OmniMotorBufferCountId, omniMotorCount);
            MotorsSpeedCS.SetFloat(m_VortexMotorBufferCountId, vortexMotorCount);
            MotorsSpeedCS.SetFloat(m_MovingMotorBufferCountId, movingMotorCount);
            MotorsSpeedCS.SetVector(m_VolumePosOffsetId, m_OffsetPos);
            
            var formRTR = form == 1 ? m_WindBufferChannelR1 : m_WindBufferChannelR2;
            var formRTG = form == 1 ? m_WindBufferChannelG1 : m_WindBufferChannelG2;
            var formRTB = form == 1 ? m_WindBufferChannelB1 : m_WindBufferChannelB2;
            var toRTR = form == 1 ? m_WindBufferChannelR2 : m_WindBufferChannelR1;
            var toRTG = form == 1 ? m_WindBufferChannelG2 : m_WindBufferChannelG1;
            var toRTB = form == 1 ? m_WindBufferChannelB2 : m_WindBufferChannelB1;
            
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferInputXID, formRTR);
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferInputYID, formRTG);
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferInputZID, formRTB);
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferOutputXID, toRTR);
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferOutputYID, toRTG);
            MotorsSpeedCS.SetTexture(m_MotorSpeedKernel, m_WindBufferOutputZID, toRTB);
            MotorsSpeedCS.Dispatch(m_MotorSpeedKernel, m_WindBrandX / 8, m_WindBrandY / 8, m_WindBrandZ);
            // 清除旧Buffer
            BufferExchangeCS.SetTexture(m_ClearBufferKernel, m_WindBufferOutputXID, formRTR);
            BufferExchangeCS.SetTexture(m_ClearBufferKernel, m_WindBufferOutputYID, formRTG);
            BufferExchangeCS.SetTexture(m_ClearBufferKernel, m_WindBufferOutputZID, formRTB);
            BufferExchangeCS.Dispatch(m_ClearBufferKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
        }
    }
    
    /// <summary>
    /// 处理平流模拟， 模拟风力扩散表现，分帧逐通道处理
    /// </summary>
    /// <param name="form"></param>
    void DoAdvection(int form)
    {
        if (AdvectionCS != null && BufferExchangeCS != null)
        {
            var formRTR = form == 1 ? m_WindBufferChannelR1 : m_WindBufferChannelR2;
            var formRTG = form == 1 ? m_WindBufferChannelG1 : m_WindBufferChannelG2;
            var formRTB = form == 1 ? m_WindBufferChannelB1 : m_WindBufferChannelB2;
            var toRTR = form == 1 ? m_WindBufferChannelR2 : m_WindBufferChannelR1;
            var toRTG = form == 1 ? m_WindBufferChannelG2 : m_WindBufferChannelG1;
            var toRTB = form == 1 ? m_WindBufferChannelB2 : m_WindBufferChannelB1;
            AdvectionCS.SetVector(m_VolumeSizeMinusOneId, m_VolumeSizeMinusOne);
            AdvectionCS.SetFloat(m_AdvectionForceId, AdvectionForce);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferInputXID, formRTR);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferInputYID, formRTG);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferInputZID, formRTB);
            // Do ChannelR
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferTargetID, formRTR);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferOutputID, toRTR);
            AdvectionCS.Dispatch(m_AdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do ChannelG
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferTargetID, formRTG);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferOutputID, toRTG);
            AdvectionCS.Dispatch(m_AdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do ChannelB
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferTargetID, formRTB);
            AdvectionCS.SetTexture(m_AdvectionKernel, m_WindBufferOutputID, toRTB);
            AdvectionCS.Dispatch(m_AdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Exchange Buffer
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferInputXID, toRTR);
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferInputYID, toRTG);
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferInputZID, toRTB);
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferOutputXID, formRTR);
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferOutputYID, formRTG);
            BufferExchangeCS.SetTexture(m_BufferExchangeKernel, m_WindBufferOutputZID, formRTB);
            BufferExchangeCS.Dispatch(m_BufferExchangeKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do reverse Advection
            AdvectionCS.SetVector(m_VolumeSizeMinusOneId, m_VolumeSizeMinusOne);
            AdvectionCS.SetFloat(m_AdvectionForceId, AdvectionForce);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferInputXID, formRTR);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferInputYID, formRTG);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferInputZID, formRTB);
            // Do ChannelR
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferTargetID, formRTR);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferOutputID, toRTR);
            AdvectionCS.Dispatch(m_ReverseAdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do ChannelG
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferTargetID, formRTG);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferOutputID, toRTG);
            AdvectionCS.Dispatch(m_ReverseAdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            // Do ChannelB
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferTargetID, formRTB);
            AdvectionCS.SetTexture(m_ReverseAdvectionKernel, m_WindBufferOutputID, toRTB);
            AdvectionCS.Dispatch(m_ReverseAdvectionKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
        }
    }

    /// <summary>
    /// 合并分离的纹理通道
    /// </summary>
    /// <param name="form"></param>
    private void DoMergeChannel(int form)
    {
        if (MergeChannelCS != null)
        {
            var formRTR = form == 1 ? m_WindBufferChannelR1 : m_WindBufferChannelR2;
            var formRTG = form == 1 ? m_WindBufferChannelG1 : m_WindBufferChannelG2;
            var formRTB = form == 1 ? m_WindBufferChannelB1 : m_WindBufferChannelB2;

            MergeChannelCS.SetInt(m_VolumeSizeXId, m_WindBrandX);
            MergeChannelCS.SetInt(m_VolumeSizeYId, m_WindBrandY);
            MergeChannelCS.SetTexture(m_MergeChannelKernel, m_WindBufferInputXID, formRTR);
            MergeChannelCS.SetTexture(m_MergeChannelKernel, m_WindBufferInputYID, formRTG);
            MergeChannelCS.SetTexture(m_MergeChannelKernel, m_WindBufferInputZID, formRTB);
            MergeChannelCS.SetTexture(m_MergeChannelKernel, m_WindBufferOutputID, m_WindVelocityData);
            MergeChannelCS.SetBuffer(m_MergeChannelKernel, m_WindDataForCPUBufferId, m_WindDataForCPUBuffer);
            m_WindDataForCPUBuffer.SetData(m_WindDataForCPU);
            MergeChannelCS.Dispatch(m_MergeChannelKernel, m_WindBrandX / 4, m_WindBrandY / 4, m_WindBrandZ / 4);
            m_WindDataForCPUBuffer.GetData(m_WindDataForCPU);
        }
    }
    public void AddWindMotor(WindMotor motor)
    {
        m_MotorList.Add(motor);
    }
    
    public void RemoveWindMotor(WindMotor motor)
    {
        m_MotorList.Remove(motor);
    }

    void ClearRednerTexture(ref RenderTexture rt)
    {
        if (rt != null)
        {
            RenderTexture.ReleaseTemporary(rt);
            rt = null;
        }
    }
    
    /// <summary>
    /// 用来给Dynamic Bones用做风场产生的风力，影响部分骨骼的外力，达到随风摆动的效果
    /// （没有细调，我现在整的这个效果有亿点点鬼畜）
    /// </summary>
    /// <param name="pos">世界空间下的坐标位置</param>
    /// <returns></returns>
    public Vector3 GetWindForceByPosAndDeltaTime(Vector3 pos)
    {
        if (!this.gameObject.activeSelf) return Vector3.zero;
        // 计算采样范围，超过范围的不采样
        float posX = pos.x - m_OffsetPos.x;
        float posY = pos.y - m_OffsetPos.y;
        float posZ = pos.z - m_OffsetPos.z;
        if(posX < 0 || posX > m_WindBrandX || posY < 0 || posY > m_WindBrandY || posZ < 0 || posZ > m_WindBrandZ) return Vector3.zero;
        
        // 模拟三线性采样
        int xb = Mathf.FloorToInt(posX);
        int xu = Mathf.CeilToInt(posX);
        int yb = Mathf.FloorToInt(posY);
        int yu = Mathf.CeilToInt(posY);
        int zb = Mathf.FloorToInt(posZ);
        int zu = Mathf.CeilToInt(posZ);

        float lerpX = posX - xb;
        float lerpY = posY - yb;
        float lerpZ = posZ - zb;

        Vector3 data0 = m_WindDataForCPU[xb + yb * m_WindBrandX + zb * m_WindBrandX * m_WindBrandY];
        Vector3 data1 = m_WindDataForCPU[xu + yb * m_WindBrandX + zb * m_WindBrandX * m_WindBrandY];
        Vector3 data2 = m_WindDataForCPU[xb + yu * m_WindBrandX + zb * m_WindBrandX * m_WindBrandY];
        Vector3 data3 = m_WindDataForCPU[xu + yu * m_WindBrandX + zb * m_WindBrandX * m_WindBrandY];
        Vector3 data4 = m_WindDataForCPU[xb + yb * m_WindBrandX + zu * m_WindBrandX * m_WindBrandY];
        Vector3 data5 = m_WindDataForCPU[xu + yb * m_WindBrandX + zu * m_WindBrandX * m_WindBrandY];
        Vector3 data6 = m_WindDataForCPU[xb + yu * m_WindBrandX + zu * m_WindBrandX * m_WindBrandY];
        Vector3 data7 = m_WindDataForCPU[xu + yu * m_WindBrandX + zu * m_WindBrandX * m_WindBrandY];

        Vector3 lerpX0 = Vector3.Lerp(data0, data1, lerpX);
        Vector3 lerpX1 = Vector3.Lerp(data4, data5, lerpX);
        Vector3 lerpX2 = Vector3.Lerp(data2, data3, lerpX);
        Vector3 lerpX3 = Vector3.Lerp(data6, data7, lerpX);

        Vector3 lerpZ0 = Vector3.Lerp(lerpX0, lerpX1, lerpZ);
        Vector3 lerpZ1 = Vector3.Lerp(lerpX2, lerpX3, lerpZ);

        Vector3 lerpY0 = Vector3.Lerp(lerpZ0, lerpZ1, lerpY);

        Vector3 windData = lerpY0;

        // CPU段使用的风力数据是否要叠加全局风力
        if (CPUWindUseGlobalWind)
        {
            Vector3 ambientWindUV = pos + m_WindNoiseOffset;
            ambientWindUV.x *= m_WindNoiseRcpTexSize.x * WindNoiseUVScale.x;
            ambientWindUV.y *= m_WindNoiseRcpTexSize.y * WindNoiseUVScale.y;
            ambientWindUV.z *= m_WindNoiseRcpTexSize.z * WindNoiseUVScale.z;
            Color noiseData = WindNoise.GetPixelBilinear(ambientWindUV.x, ambientWindUV.y, ambientWindUV.z, 0);
            Vector3 windNoise = new Vector3(noiseData.r, noiseData.g, noiseData.b) * 2.0f;
            windNoise.x -= 1f;
            windNoise.y -= 1f;
            windNoise.z -= 1f;
            windData += GlobalAmbientWind + new Vector3(windNoise.x * WindNoiseScale.x, windNoise.y * WindNoiseScale.y, windNoise.z * WindNoiseScale.z);
        }
        
        Vector3 force = windData * OverallPower;
        return force;
    }
    Vector3 ConvertFloatPointToInt(Vector3 v)
    {
        Vector3 o;
        o.x = v.x < 0 ? Mathf.Ceil(v.x) : Mathf.Floor(v.x);
        o.y = v.y < 0 ? Mathf.Ceil(v.y) : Mathf.Floor(v.y);
        o.z = v.z < 0 ? Mathf.Ceil(v.z) : Mathf.Floor(v.z);
        return o;
    }
    public void OnDestroy()
    {
        ClearRednerTexture(ref m_WindBufferChannelR1);
        ClearRednerTexture(ref m_WindBufferChannelR2);
        ClearRednerTexture(ref m_WindBufferChannelG1);
        ClearRednerTexture(ref m_WindBufferChannelG2);
        ClearRednerTexture(ref m_WindBufferChannelB1);
        ClearRednerTexture(ref m_WindBufferChannelB2);
        if (m_DirectionalMotorBuffer != null)
        {
            m_DirectionalMotorBuffer.Release();
            m_DirectionalMotorBuffer = null;
        }
        if (m_OmniMotorBuffer != null)
        {
            m_OmniMotorBuffer.Release();
            m_OmniMotorBuffer = null;
        }
        if (m_VortexMotorBuffer != null)
        {
            m_VortexMotorBuffer.Release();
            m_VortexMotorBuffer = null;
        }
        if (m_MovingMotorBuffer != null)
        {
            m_MovingMotorBuffer.Release();
            m_MovingMotorBuffer = null;
        }
    }
}
