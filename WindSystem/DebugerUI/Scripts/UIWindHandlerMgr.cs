using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIWindHandlerMgr : MonoBehaviour
{
    private static UIWindHandlerMgr m_Instance;

    public static UIWindHandlerMgr Instance
    {
        get
        {
            return m_Instance;
        }
    }

    private GameObject m_WinWindControlPanel;

    public UIWindHandlerMgr()
    {
    }
    void Awake ()
    {
        m_Instance = this;
    }

    void Start()
    {
        EventManager.AddEvent(EventName.CreateWindMotor, (MotorType motorType) => CreateNewWindMotor(motorType));
        EventManager.AddEvent(EventName.DeleteWindMotor, (WindMotor windMotor) => DeleteWindMotor(windMotor));
    }
    private void CreateNewWindMotor(MotorType motorType)
    {
        GameObject windObj = WindPool.Instance.PopWindMotor();
        var windMotor = windObj != null ? windObj.GetComponent<WindMotor>() : null;
        
        if (windMotor != null)
        {
            windMotor.MotorType = motorType;
            EventManager.DispatchEvent(EventName.UpdateWinWindMotor, windMotor);
            EventManager.DispatchEvent(EventName.UpdateWinWindMotorDetail, windMotor);
        }
    }

    private void DeleteWindMotor(WindMotor windMotor)
    {
        EventManager.DispatchEvent(EventName.DeleteWindHandler, windMotor);
        WindPool.Instance.PushWindMotor(windMotor.gameObject);
    }
}
