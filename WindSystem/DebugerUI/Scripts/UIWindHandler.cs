using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UIWindHandler : MonoBehaviour
{
    public Sprite Directional;
    public Sprite Omni;
    public Sprite Vortex;
    public Sprite Moving;
    [SerializeField]
    private WindMotor m_WindMotor;
    private RectTransform m_Rect;
    private Image m_Image;
    private Button m_Button;
    private bool m_Inited = false;
    private Action m_UpdateHeaderIconFunc;

    private void InitHandler()
    {
        if (!m_Inited)
        {
            m_Rect = transform.GetComponent<RectTransform>();
            m_Image = transform.GetComponent<Image>();
            m_Rect.anchoredPosition = Vector2.zero;
            m_Image.sprite = Directional;
            m_Button = transform.GetComponent<Button>();
            m_Button.onClick.AddListener(() =>
            {
                if (m_WindMotor != null)
                {
                    EventManager.DispatchEvent(EventName.UpdateWinWindMotorDetail, m_WindMotor);
                }
            });
            m_UpdateHeaderIconFunc = ()=>UpdateHandlerIcon();
            EventManager.AddEvent(EventName.UpdateHandlerIcon, m_UpdateHeaderIconFunc);
        }
    }
    public void UpdateWindMotor(WindMotor motor)
    {
        InitHandler();
        m_WindMotor = motor;
        if (motor != null)
        {
            switch (m_WindMotor.MotorType)
            {
                case MotorType.Directional:
                    m_Image.sprite = Directional;
                    break;
                case MotorType.Omni:
                    m_Image.sprite = Omni;
                    break;
                case MotorType.Vortex:
                    m_Image.sprite = Vortex;
                    break;
                case MotorType.Moving:
                    m_Image.sprite = Moving;
                    break;
            }
            UpdateTransformPosition();
        }
    }

    public void UpdateHandlerIcon()
    {
        if (m_WindMotor != null)
        {
            switch (m_WindMotor.MotorType)
            {
                case MotorType.Directional:
                    m_Image.sprite = Directional;
                    break;
                case MotorType.Omni:
                    m_Image.sprite = Omni;
                    break;
                case MotorType.Vortex:
                    m_Image.sprite = Vortex;
                    break;
                case MotorType.Moving:
                    m_Image.sprite = Moving;
                    break;
            }
        }
    }

    public void UpdateTransformPosition()
    {
        if (m_WindMotor != null)
        {
            Vector2 screenPos;
            UIFacade.Instance.GetWorldPosToScreenPos(m_WindMotor.transform.position, out screenPos);
            m_Rect.anchoredPosition = screenPos;
        }
    }

    private void OnDestroy()
    {
        EventManager.RemoveEvent(EventName.UpdateHandlerIcon, m_UpdateHeaderIconFunc);
        m_UpdateHeaderIconFunc = null;
    }
}
