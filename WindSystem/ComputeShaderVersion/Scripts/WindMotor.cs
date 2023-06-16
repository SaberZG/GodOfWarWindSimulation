using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum MotorType
{
    Directional,
    Omni,
    Vortex,
    Moving,
    Cylinder,
    // Pressure,
}

public struct MotorDirectional
{
    public Vector3 position;
    public float radiusSq;
    public Vector3 force;
}

public struct MotorOmni
{
    public Vector3 position;
    public float radiusSq;
    public float force;
}

public struct MotorVortex
{
    public Vector3 position;
    public Vector3 axis;
    public float radiusSq;
    public float force;
}

public struct MotorMoving
{
    public Vector3 prePosition;
    public float moveLen;
    public Vector3 moveDir;
    public float radiusSq;
    public float force;
}

public struct MotorCylinder
{
    public Vector3 position;
    public Vector3 axis;
    public float height;
    public float radiusBottonSq;
    public float radiusTopSq;
    public float force;
}
public class WindMotor : MonoBehaviour
{
    public MotorType MotorType;
    public MotorDirectional motorDirectional;
    public MotorOmni motorOmni;
    public MotorVortex motorVortex;
    public MotorMoving motorMoving;
    public MotorCylinder motorCylinder;

    private static MotorDirectional emptyMotorDirectional = new MotorDirectional();
    private static MotorOmni emptyMotorOmni = new MotorOmni();
    private static MotorVortex emptyMotorVortex = new MotorVortex();
    private static MotorMoving emptyMotorMoving = new MotorMoving();
    private static MotorCylinder emptyMotorCylinder = new MotorCylinder();

    public static MotorDirectional GetEmptyMotorDirectional()
    {
        return emptyMotorDirectional;
    }
    public static MotorOmni GetEmptyMotorOmni()
    {
        return emptyMotorOmni;
    }
    public static MotorVortex GetEmptyMotorVortex()
    {
        return emptyMotorVortex;
    }
    public static MotorMoving GetEmptyMotorMoving()
    {
        return emptyMotorMoving;
    }
    public static MotorCylinder GetEmptyMotorCylinder()
    {
        return emptyMotorCylinder;
    }
    
    /// <summary>
    /// 创建风力发电机的时间，以Time.fixedTime为准
    /// </summary>
    private float m_CreateTime;
    public bool Loop = true;
    public float LifeTime = 5f;
    [Range(0.001f, 100f)]
    public float Radius = 1f;
    public AnimationCurve RadiusCurve = AnimationCurve.Linear(1, 1, 1, 1);
    public Vector3 Asix = Vector3.up;
    [Range(-12f, 12f)]
    public float Force = 1f;
    public AnimationCurve ForceCurve = AnimationCurve.Linear(1, 1, 1, 1);
    public float Duration = 0f;
    public float MoveLength;
    public AnimationCurve MoveLengthCurve = AnimationCurve.Linear(1, 1, 1, 1);

    private Vector3 m_prePosition = Vector3.zero;
    #region BasicFunction

    private void Start()
    {
        m_CreateTime = Time.fixedTime;
    }

    private void OnEnable()
    {
        if (WindManager.Instance == null) return;
        WindManager.Instance.AddWindMotor(this);
        m_CreateTime = Time.fixedTime;
    }

    private void OnDisable()
    {
        if (WindManager.Instance == null) return;
        WindManager.Instance.RemoveWindMotor(this);
    }

    private void OnDestroy()
    {
        if (WindManager.Instance == null) return;
        WindManager.Instance.RemoveWindMotor(this);
    }
    #endregion
    
    #region MainFunction
    /// <summary>
    /// 声明周期结束，直接销毁，后面改成对象池的形式
    /// </summary>
    /// <param name="duration"></param>
    void CheckMotorDead()
    {
        float duration = Time.fixedTime - m_CreateTime;
        if (duration > LifeTime)
        {
            if (Loop)
            {
                m_CreateTime = Time.fixedTime;
            }
            else
            {
                m_CreateTime = 0f;
                WindPool.Instance.PushWindMotor(this.gameObject);
            }
        }
    }
    #endregion
    
    #region UpdateForceAndOtherProperties
    /// <summary>
    /// 调用的时候才更新风的一些变化参数
    /// </summary>
    public void UpdateWindMotor()
    {
        switch (MotorType)
        {
            case MotorType.Directional:
                UpdateDirectionalWind();
                break;
            case MotorType.Omni:
                UpdateOmniWind();
                break;
            case MotorType.Vortex:
                UpdateVortexWind();
                break;
            case MotorType.Moving:
                UpdateMovingWind();
                break;
        }
    }

    private float GetForce(float timePerc)
    {
        return Mathf.Clamp(ForceCurve.Evaluate(timePerc) * Force, -12f, 12f);
    }
    private void UpdateDirectionalWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorDirectional = new MotorDirectional()
        {
            position = transform.position,
            radiusSq = radius * radius,
            force = transform.forward * GetForce(timePerc)
        };
        CheckMotorDead();
    }

    private void UpdateOmniWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorOmni = new MotorOmni()
        {
            position = transform.position,
            radiusSq = radius * radius,
            force = GetForce(timePerc)
        };
        CheckMotorDead();
    }

    private void UpdateVortexWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        motorVortex = new MotorVortex()
        {
            position = transform.position,
            axis = Vector3.Normalize(Asix),
            radiusSq = radius * radius,
            force = GetForce(timePerc)
        };
        CheckMotorDead();
    }

    private void UpdateMovingWind()
    {
        float duration = Time.fixedTime - m_CreateTime;
        float timePerc = duration / LifeTime;
        Duration = timePerc;
        float moveLen = MoveLength * MoveLengthCurve.Evaluate(timePerc);
        float radius = Radius * RadiusCurve.Evaluate(timePerc);
        Vector3 position = transform.position;
        Vector3 prePosition = m_prePosition == Vector3.zero ? position : m_prePosition;
        Vector3 moveDir = position - prePosition;
        motorMoving = new MotorMoving()
        {
            prePosition = prePosition,
            moveLen = moveLen,
            moveDir = moveDir,
            radiusSq = radius * radius,
            force = GetForce(timePerc)
        };
        m_prePosition = position;
        CheckMotorDead();
    }
    #endregion

    public void TransferMotorToECSEntity()
    {
        if (WindMgrECS.Instance == null) return;
        WindMgrECS.Instance.TransferMotorToECSEntity(this);
    }
}
